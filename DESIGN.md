# FormatParse design

Status: implemented core, pre-1.0 API. This document describes the current
code. The roadmap describes proposals, not available APIs.

## Purpose

Parse predictable text into typed .NET values with a small format-like grammar.
The runtime library targets net8.0 and has no NuGet dependencies. The repository
uses the .NET 10 SDK in global.json; examples and tooling can target newer runtimes.

Four responsibilities stay separate:

| Responsibility | Meaning |
| --- | --- |
| Pattern | Literal text and capture boundaries |
| Target T | Constructor shape and value types |
| Binding | Which constructor argument receives each capture |
| Field context | A typed place for future parsing and validation policies |

This is not a general grammar, serialization format, or universal inverse of
.NET formatting. The target T does not need to implement a parsing interface.

## Public API

The namespace is FormatParse and the static entry point is Parser. Import
`using FormatParse;` and call `Parser.Parse<T>()`, `Parser.Compile<T>()`, or
`Parser.For<T>()` without a type alias. All public classes below are sealed except the static entry
point. None of the parser or builder classes has a public constructor.

The following is a signature reference; bodies are omitted.

```csharp
public static class Parser
{
    public static FormatParser<T> Compile<T>(string pattern);
    public static FormatParseBuilder<T> For<T>(string pattern);

    public static T Parse<T>(string pattern, string input);
    public static T Parse<T>(string pattern, string input, IFormatProvider? provider);
    public static T Parse<T>(string pattern, ReadOnlySpan<char> input);
    public static T Parse<T>(string pattern, ReadOnlySpan<char> input, IFormatProvider? provider);

    public static bool TryParse<T>(string pattern, string? input, [MaybeNullWhen(false)] out T result);
    public static bool TryParse<T>(string pattern, string? input, IFormatProvider? provider, [MaybeNullWhen(false)] out T result);
    public static bool TryParse<T>(string pattern, ReadOnlySpan<char> input, [MaybeNullWhen(false)] out T result);
    public static bool TryParse<T>(string pattern, ReadOnlySpan<char> input, IFormatProvider? provider, [MaybeNullWhen(false)] out T result);
}

public sealed class FormatParser<T>
{
    public T Parse(string input);
    public T Parse(string input, IFormatProvider? provider);
    public T Parse(ReadOnlySpan<char> input);
    public T Parse(ReadOnlySpan<char> input, IFormatProvider? provider);

    public bool TryParse(string? input, [MaybeNullWhen(false)] out T result);
    public bool TryParse(string? input, IFormatProvider? provider, [MaybeNullWhen(false)] out T result);
    public bool TryParse(ReadOnlySpan<char> input, [MaybeNullWhen(false)] out T result);
    public bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, [MaybeNullWhen(false)] out T result);
}

public sealed class FormatParseBuilder<T>
{
    public FieldBindingBuilder<T, TValue> Bind<TValue>(Expression<Func<T, TValue>> selector);
    public FormatParser<T> Compile();
}

public sealed class FieldBindingBuilder<T, TValue>
{
    public FieldBindingBuilder<T, TNext> Bind<TNext>(Expression<Func<T, TNext>> selector);
    public FormatParser<T> Compile();
}
```

The annotations come from System.Diagnostics.CodeAnalysis; selectors use
System.Linq.Expressions. A nullable receiving variable is appropriate for
reference results; the successful TryParse branch knows the result is non-null.

### Three entry points

```csharp
public record User(string Name, int Age);

// One input: compile, parse, then let the temporary parser go.
User user = Parser.Parse<User>("User {} is {}", "User Alice is 18");

// Repeated inputs: the caller owns the parser and its lifetime.
var parser = Parser.Compile<User>("User {} is {}");

// Different capture order: each Bind consumes the next capture.
var reordered = Parser.For<User>("Age={}; Name={}")
    .Bind(x => x.Age)
    .Bind(x => x.Name)
    .Compile();
```

There is no hidden pattern cache or global compiled binding cache. Each Compile
builds a new plan. One-shot Parse/TryParse compile on each invocation.

## Grammar and boundaries

```text
pattern = zero or more literals, captures, or escaped braces
capture = {}
escape  = {{ or }}
```

Literals use ordinal, case-sensitive comparison. Whitespace is significant.
Named fields, type names, format specifiers, alignment, and adjacent captures
are rejected. Errors include the pattern position.

The first literal matches the input prefix. Each non-final capture ends at
the first occurrence of its following literal. The final capture ends before
the trailing literal anchored at the input end, or consumes the remainder.
There is no backtracking or conversion-driven boundary search.

| Pattern | Input | Captures |
| --- | --- | --- |
| {}:{}:{} | 1:2:3 | 1, 2, 3 |
| {}:{} | a:b:c | a, b:c |
| [{}] | [a]b] | a]b |
| {{{}}} | {42} | 42 |

The entire input must match. Empty captures are allowed; the value conversion
decides whether they are meaningful.

## Construction and binding

T must be concrete, non-nullable, and have exactly one public instance
constructor. All constructor parameters need captures, even optional parameters.
A public parameterless constructor supports a literal-only or empty pattern.
Ref, pointer, and byref-like parameters are unsupported.

Positional binding uses constructor declaration order and does not depend on
parameter names. Ordinary scalar ValueTuple constructors also follow this rule;
larger tuples containing a nested Rest value are not a scalar conversion feature.

Explicit binding accepts direct public readable instance properties and public
instance fields. It maps member names to constructor parameters using ordinal
comparison ignoring case, and requires the same CLR type. Ambiguous matches and
duplicate destinations fail. Each capture and parameter must be bound once.

A selector is inspected as metadata, not executed. Member getters, nested
access, calls, constants, arithmetic, boxing, numeric casts, and user-defined
conversions are not used as binding logic. Identity conversions can be unwrapped.
The parser calls the constructor; it does not assign properties afterward.

Builders are immutable snapshots. Each Bind creates a new ordered binding list
and returns FieldBindingBuilder<T, TValue>. The typed field node survives in
that list, so later field policies can retain TValue without routing values
through object. Adding another Bind or compiling a branch does not mutate others.

Skipping a capture or leaving a constructor parameter unbound is currently an
error. Nullable fields permit a null *value*, not omission of the capture.

## Default conversion

| Type | Behavior |
| --- | --- |
| string | Preserve the captured text in a result string |
| enum | Case-sensitive Enum.TryParse; numeric values and flags names are accepted |
| Nullable<V> | Empty capture becomes null; otherwise use V's parser |
| ISpanParsable<V> | Call the standard span TryParse |
| IParsable<V> only | Create a string and call standard TryParse |
| Other field types | Configuration error |

BCL scalar types use their standard protocols: numeric types, char, bool, Guid,
DateTime, DateTimeOffset, DateOnly, TimeOnly, TimeSpan, and compatible framework
types such as IPAddress. Custom types implementing the same contracts work too.
Span parsing takes precedence when both interfaces are available.

A null provider resolves to CultureInfo.CurrentCulture for that call. A supplied
provider is passed to supported conversions. Pattern matching and member
comparison are culture-independent. BCL numeric whitespace and style rules
remain in effect. The matcher itself does not trim.

For nullable fields, whitespace and the text null are passed to the underlying
parser; they are not special null markers. Enum conversion does not reject
undefined numeric values. These are parsing rules, not extra validation.

## Failure and concurrency

| Condition | Result |
| --- | --- |
| Null pattern, selector, or throwing Parse input | ArgumentNullException |
| Invalid grammar, target, binding, or conversion configuration | ArgumentException |
| Normal mismatch or conversion failure | TryParse returns false and default; Parse throws FormatException |
| Null string input to TryParse | false and default |
| User parser or constructor throws | Original exception propagates |

For validates grammar, Bind validates selectors, and Compile validates the
complete construction plan. One-shot APIs compile before checking input.
Expected failures do not use exceptions as normal control flow. A target is
constructed only after every conversion succeeds.

Compiled parsers have immutable pattern data and delegates. Input ranges,
position, parsed values, and rented buffers belong to the current call.
Instances may be shared concurrently, provided custom parsers, constructors,
and supplied providers also support concurrent use.

## Internal execution

```text
Compile:
pattern -> Pattern -> TypeBinding<T> -> typed ObjectParser<T> -> FormatParser<T>

Parse:
input span -> PatternMatcher -> capture ranges -> typed conversions -> new T
```

Pattern stores decoded literals and capture count. PatternMatcher only locates
ranges; it does not know the target type. TypeBinding builds a constructor plan
with capture indices and conversion expressions. Explicit selectors affect that
plan without changing pattern grammar. ValueConversion supplies the default
typed conversion expressions.

Reflection and expression compilation occur while building the plan. The
generated delegate uses typed locals and a direct constructor call, avoiding
object arrays, per-input reflection invocation, and numeric boxing.

Up to 128 capture ranges use stack storage; larger calls rent from ArrayPool
and return the buffer in a finally block. Input spans are borrowed only for the
call. Returned strings copy their content; results never retain borrowed spans.
String fields are created after other conversions succeed.

Compilation, reference results, string fields, and string-only converters can
allocate. Selected compiled numeric paths can be allocation-free; this is not
a blanket zero-allocation or throughput guarantee. Runtime code generation and
constructor metadata are required. Native AOT and trimming are not supported.

## Verification and performance

Tests cover pattern boundaries, configuration failures, constructor binding,
typed selectors, enum/nullable policies, conversion success/failure, culture,
exceptions, immutable builder branches, concurrent reuse, pooled captures,
and allocations on selected compiled paths.

CI runs on Linux and Windows. It builds with warnings treated as errors, checks
coverage (at least 90% lines and 85% branches), validates examples and benchmark
equivalence, and inspects the package's files and dependency policy. Coverage
is evidence of execution, not proof of correct behavior.

The benchmark project compares typed parsing with reused Regex, compiled Regex,
GeneratedRegex, and FormatParser<T>. A separate suite includes one-shot
construction costs. See [benchmarks/README.md](benchmarks/README.md).
CI timing is exploratory, not a release gate.

## Roadmap

These features are planned or candidates; no placeholder APIs are shipped.

| Feature | Direction and open questions |
| --- | --- |
| Field Format policies | Exact dates, numeric styles and representation rules; argument types and reverse semantics are undecided |
| Field Validate policies | Separate value checks from source-text checks, such as fractional digits |
| Field ParseWith policies | A typed span-based TryParse delegate retaining TValue |
| Per-field provider/options | Culture and NumberStyles belong to field configuration |
| Skipped captures and defaults | Define ignored input separately from unbound constructor parameters; decide optional/default-value precedence before adding APIs |
| Transformations | Keep rounding and normalization explicit and separate from parsing |
| Additional construction | Setter binding and multiple-constructor selection need deterministic contracts |
| Extended tuples | Define nested Rest handling without changing scalar conversion |
| Diagnostics | Consider richer errors only when callers need them |
| Source generation / Native AOT | Preserve the same observable contracts while replacing runtime reflection/code generation |

Future field configuration should read naturally:

```csharp
// Proposed shape only; these policy methods do not exist yet.
Parser.For<Invoice>("AMOUNT={}; DATE={}")
    .Bind(x => x.Amount)
        .Format(...)
        .Validate(...)
    .Bind(x => x.Date)
        .ParseWith(...)
    .Compile();
```

Parsing, validation, and transformation remain distinct. Format policies must
not silently round values, and grammar must not accumulate names or .NET types.

## Release process

Versioned tags trigger build, test, package inspection, NuGet publishing and
GitHub Release publication. Tags must match the project's Version. Release
workflows revalidate the tagged commit rather than trusting another branch's CI.
See [temp.md](temp.md) for configuration, publication, retries, and benchmark use.
