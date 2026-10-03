# FormatParse

[![CI](https://github.com/jrmhx/FormatParse/actions/workflows/ci.yml/badge.svg)](https://github.com/jrmhx/FormatParse/actions/workflows/ci.yml)
[![Release](https://github.com/jrmhx/FormatParse/actions/workflows/release.yml/badge.svg)](https://github.com/jrmhx/FormatParse/actions/workflows/release.yml)
[![Coverage](https://img.shields.io/badge/coverage-CI_report-blue)](https://github.com/jrmhx/FormatParse/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8%2B-512BD4)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/FormatParse)](https://www.nuget.org/packages/FormatParse)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)

Parse structured text into typed .NET values using format-like patterns.
Targets .NET 8 or above, with no runtime dependencies.

## Usage

```cs
using FormatParse;
```

## Quick start

### Tuple

```cs
(int num, string name) = Parser.Parse<(int, string)>("#{}, name: {}", "#789, name: Alice");

Console.WriteLine($"name: {name} is #{num}");
// name: Alice is #789

```

### Record positional capture

```cs

// example of positional parsing
User user = Parser.Parse<User>("User {} is {}", "User Alice is 18");
// user.Name == "Alice", user.Age == 18

public record User(string Name, int Age);
```

Each `{}` supplies the next constructor argument. The target supplies its type;
there are no names, types, or format specifiers inside captures.

Use `TryParse` when invalid input is expected:

```cs
if (Parser.TryParse<User>("User {} is {}", input, out User? user))
{
    Console.WriteLine(user.Name);
}
```

### Parse many inputs

Compile once and keep the parser:

```cs
var parser = Parser.Compile<User>("User {} is {}");

User alice = parser.Parse("User Alice is 18");
User bob = parser.Parse("User Bob is 24");
```

Compiled parsers are immutable and can be shared across threads. One-shot
`Parse` and `TryParse` compile on each call; there is no global pattern cache.

### Change the capture order

Bind each capture to a member when the input order differs from the constructor:

```cs
var parser = Parser.For<User>("Age={}; Name={}")
    .Bind(x => x.Age)
    .Bind(x => x.Name)
    .Compile();

User user = parser.Parse("Age=18; Name=Alice");
```

Selectors must reference a direct public instance property or field. Calls,
nested paths, numeric casts and boxing are rejected. Members map to constructor
parameters by name, ignoring case, with exactly matching types. Ambiguous or
duplicate mappings are errors. Member getters are not executed.

A target must be concrete and non-nullable, with exactly one public instance
constructor. Every parameter needs one capture, including optional parameters.
A parameterless target can match a literal-only pattern. Builders are immutable:
each `Bind` returns a new configuration, preserving that field's value type.

## Patterns

| Syntax | Meaning |
| --- | --- |
| `{}` | Capture a value |
| `{{` | Literal opening brace |
| `}}` | Literal closing brace |
| Other text | Match exactly, including whitespace |

Matching is ordinal, case-sensitive and covers the entire input. Adjacent
captures (`{}{}`), malformed braces and named captures (`{Name}`) are errors.

A non-final capture ends at the first occurrence of its following literal.
The final capture ends before the trailing literal, anchored at the input's
end, or consumes the remainder when there is no trailing literal. There is no
backtracking: `{}:{}` parses `a:b:c` as `a` and `b:c`.
`{{{}}}` parses a value surrounded by literal braces, such as `{42}`.

## Values and culture

Supported fields include string, char, bool, numeric types, Guid, DateTime,
DateTimeOffset, TimeSpan, DateOnly, TimeOnly, enums, and types implementing
`ISpanParsable<TSelf>` or `IParsable<TSelf>`. Span parsing takes precedence.
The target object itself needs no parsing interface.

Strings preserve the captured text. Nullable value fields treat an empty
capture as null; whitespace and the text `null` are passed to the underlying
parser. Enums follow case-sensitive `Enum.TryParse` rules, including numeric
values and comma-separated names; undefined numeric values are not rejected.

Conversions use the current culture at call time unless you supply a provider:

```csharp
Order order = Parser.Parse<Order>(
    "Order #{}: ${}", "Order #42: $12.50", CultureInfo.InvariantCulture);

public record Order(int Id, decimal Price);
```

This example requires `using System.Globalization;`. BCL conversions retain
their normal whitespace and numeric rules; FormatParse does not trim captures.

Both static and compiled APIs also accept `ReadOnlySpan<char>`:

```csharp
ReadOnlySpan<char> input = "[User Alice is 18]".AsSpan(1, 16);
User user = Parser.Parse<User>("User {} is {}", input);
```

Slicing does not copy the input. Result strings and types with only string-based
parsing may allocate.

## Errors

| Condition | Behavior |
| --- | --- |
| Null pattern, selector, or Parse input | ArgumentNullException |
| Invalid pattern, binding, target, or unsupported field type | ArgumentException |
| Input mismatch or normal conversion failure | Parse throws FormatException; TryParse returns false and default |
| Null TryParse input | false and default |
| User constructor or custom parser throws | The original exception propagates |

Compilation validates configuration. One-shot APIs compile before inspecting
input. A successful parse constructs the target only after all conversions
succeed; failed calls return no partial result.

## API and design

| Entry point | Purpose |
| --- | --- |
| `Parser.Parse<T>` / `TryParse<T>` | Parse once |
| `Parser.Compile<T>` | Compile positional constructor binding |
| `Parser.For<T>` | Start explicit binding |
| `Bind<TValue>(Expression<Func<T, TValue>>)` | Select the next capture's destination |
| `Compile()` | Finish explicit binding |
| `FormatParser<T>.Parse` / `TryParse` | Reuse a compiled parser |

`Parse` and `TryParse` accept `string` or `span` input, with provider overloads.
For `TryParse`, the provider precedes the out result. Complete signatures and
parameter documentation are available through the library's XML documentation.

Pattern defines text structure; `T` defines the target and value types; bindings
select constructor arguments. Typed field contexts leave room for future
parsing and validation policies. Format, Validate and ParseWith APIs are not
currently implemented. Source generators, setter-based construction and nested binding
are also outside this release.

Compiled plans use spans, local capture ranges and typed construction delegates.
There is no per-input mutable state on the parser, reflection invocation during
parsing, or boxing of numeric fields. Compilation allocates; retained parsers
avoid that repeated cost. User providers and parsing code must be thread-safe
when shared. Runtime code generation is required; Native AOT and trimming are
not supported.

FormatParse is for structured text, not general serialization or a universal
inverse of .NET formatting. The API may change during 0.x releases.

See [the runnable examples](examples/FormatParse.Example/UsageExamples.cs).
The [design](DESIGN.md) describes the complete API and roadmap.
See [benchmarks](benchmarks/README.md) for Regex comparisons and
[release setup](temp.md) for CI and NuGet publishing.
To build and test from source with a .NET 8-compatible SDK and runtime:

```sh
dotnet build src/FormatParse/FormatParse.csproj
dotnet test tests/FormatParse.Tests/FormatParse.Tests.csproj --collect:"XPlat Code Coverage"
dotnet run --project examples/FormatParse.Example/FormatParse.Example.csproj
```
