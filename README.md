# FormatParse

[![CI](https://github.com/jrmhx/FormatParse/actions/workflows/ci.yml/badge.svg)](https://github.com/jrmhx/FormatParse/actions/workflows/ci.yml)
[![Release](https://github.com/jrmhx/FormatParse/actions/workflows/release.yml/badge.svg)](https://github.com/jrmhx/FormatParse/actions/workflows/release.yml)
[![Coverage](https://img.shields.io/badge/coverage-CI_report-blue)](https://github.com/jrmhx/FormatParse/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8%2B-512BD4)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/FormatParse)](https://www.nuget.org/packages/FormatParse)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)

FormatParse is a lightweight, strongly typed parser for structured text in C#, inspired by Python library [parse](https://github.com/r1chardj0n3s/parse).

<details>
  <summary> see an example compare with Regex</summary>

  ```csharp
  // Convert a log message to a strongly typed record.

  // [2026-10-04T10:42:31] [INFO] User alice completed request 550e8400-e29b-41d4-a716-446655440000

  public record LogEntry(
      DateTime Timestamp,
      string Level,
      string User,
      Guid RequestId
  );
  ```

  **GeneratedRegex**

  ```csharp
  using System.Globalization;
  using System.Text.RegularExpressions;

  public static partial class RegexLogParser
  {
      [GeneratedRegex(
          @"^\[(?<timestamp>.+?)\] \[(?<level>.+?)\] User (?<user>.+?) completed request (?<requestId>.+)$")]
      private static partial Regex Pattern();

      public static LogEntry Parse(string input)
      {
          var match = Pattern().Match(input);

          if (!match.Success)
              throw new FormatException("Invalid log entry.");

          return new LogEntry(
              DateTime.Parse(
                match.Groups["timestamp"].Value,
                CultureInfo.InvariantCulture),
                match.Groups["level"].Value,
                match.Groups["user"].Value,
                Guid.Parse(match.Groups["requestId"].Value),
              );
      }
  }

  LogEntry entry = RegexLogParser.Parse(
      "[2026-10-04T10:42:31] [INFO] User alice completed request 550e8400-e29b-41d4-a716-446655440000"
  );
  ```

  **FormatParse**

  ```csharp
  using FormatParse;

  var parser = Parser.Compile<LogEntry>(
      "[{}] [{}] User {} completed request {}");

  LogEntry entry = parser.Parse(
      "[2026-10-04T10:42:31] [INFO] User alice completed request 550e8400-e29b-41d4-a716-446655440000"
  );
  ```

</details>

## Features

- **Span-based parsing** — operate on captured input spans without copy overhead.
- **Simple `{}` patterns** — describe the input structure without embedding type info in the pattern.
- **Type-driven conversion** — infer conversions from constructor parameters and members.
- **Strongly typed results** — parse directly into records, classes, and tuples.
- **LINQ-style fluent builder** — configure and reorder field bindings with strongly typed member selectors through a method-chaining API.
- **Compiled parsers** — compile a pattern once and reuse it for repeated parsing.
- **Culture-aware conversion** — control parsing behavior with `IFormatProvider`.
- **Zero runtime dependencies** — built on .NET APIs with no additional runtime packages.
- **.NET 8+** — targets applications using .NET 8 or above.

## Supported types

Every field uses `{}`. The destination type selects its conversion; types are not written inside the pattern.

| Destination type | Captured text | Parsed value | Binding |
| --- | --- | --- | --- |
| `string` | `Alice` | `"Alice"` | Default |
| `char` | `A` | `'A'` | Default |
| `bool` | `true` | `true` | Default |
| Integers (`int`, `long`, `Int128`, `BigInteger`, etc.) | `42` | `42` | Default |
| Numbers (`float`, `double`, `decimal`, `Half`) | `3.14` | `3.14` | Default, culture-aware |
| Enum | `Warning` | `Level.Warning` | Default, case-sensitive |
| Nullable value type (`int?`, etc.) | Empty field | `null` | Default |
| `DateTime` | `2026-10-06 13:14:15` | Date and time | Default or exact `"yyyy-MM-dd HH:mm:ss"` |
| `DateTimeOffset` | `2026-10-06T13:14:15.0000000+02:00` | Date, time and offset | Default or exact `"O"` |
| `DateOnly` | `2026-10-06` | Date | Default or exact `"yyyy-MM-dd"` |
| `TimeOnly` | `13:14:15` | Time | Default or exact `"HH:mm:ss"` |
| `TimeSpan` | `01:02:03` | Duration | Default or exact `"c"` |
| `Guid` | `550e8400-e29b-41d4-a716-446655440000` | GUID | Default or exact `"D"` |
| Custom `HexId` | `0xff` | `HexId(255)` | Custom `HexIdParser` |
| `List<int>` | `[10,20,30]` | Three integers | Custom JSON adapter |
| `Dictionary<string, List<DateOnly>>` | `{"team":["2026-10-06"]}` | Dictionary of date lists | Custom JSON adapter |
| Custom `Address` class | `City=Sydney Postcode=2000` | Typed `Address` | Nested compiled parser |

Default conversion also supports types implementing `ISpanParsable<T>` or `IParsable<T>`.
Use `.Bind(x => x.Date, "yyyy-MM-dd")` for an exact format, or
`.Bind(x => x.Scores, customParser)` for an `IValueParser<List<int>>`.
A compiled `FormatParser<Address>` can be passed to `.Bind(x => x.Address, addressParser)`.
Collections and arbitrary classes are **not** automatically deserialized: the JSON and hexadecimal adapters above are
[example implementations](examples/FormatParse.Example/ExampleParsers.cs), not package APIs.
See [the examples guide](examples/README.md) for the complete runnable showcase and concurrent CSV processing.

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

### Fluent Builder Pattern

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
A parameterless target can match a literal-only pattern. Builders are mutable
and not thread-safe: each `Bind` appends to the same configuration and returns
a typed field context. Use `Fork()` when you need an independent branch:

```cs
var root = Parser.For<User>("{}:{}");
var byName = root.Fork().Bind(x => x.Name).Bind(x => x.Age).Compile();
var byAge = root.Fork().Bind(x => x.Age).Bind(x => x.Name).Compile();
```

`Fork()` is also available on field contexts and copies all current bindings.
`Compile()` creates a stable parser; later builder operations cannot change it.

### Field parsers

Choose how each capture becomes a typed value:

| Binding | Parsing behavior |
| --- | --- |
| `Bind(x => x.Field)` | Default conversion for the member's type |
| `Bind(x => x.Field, "format")` | BCL-backed `TryParseExact` |
| `Bind(x => x.Field, parser)` | An explicit `IValueParser<TValue>` |

Compiled parsers implement `IValueParser<T>`, so they compose naturally:

```cs
var point = Parser.Compile<Point>("({}, {})");
var parser = Parser.For<Event>("{} at {} on {}")
    .Bind(x => x.Name)
    .Bind(x => x.Position, point)
    .Bind(x => x.Date, "yyyy-MM-dd")
    .Compile();

Event value = parser.Parse("Alice at (10, 20) on 2026-10-06");

public record Point(int X, int Y);
public record Event(string Name, Point Position, DateOnly Date);
```

Exact formats support `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`,
`TimeSpan`, `Guid`, and their nullable forms. Date/time styles are `None`;
the call's provider is passed through where the BCL supports it. Nullable exact
fields use an empty capture for null, like default nullable conversion.

A format is **not** a general inverse of `ToString(format)`. Numeric formats
such as decimal `"F2"`, `"N2"`, and `"C2"` are not supported; supply a custom
parser instead. Unsupported types and detectably invalid format configurations
throw during binding. Input mismatches return false from `TryParse`.

Implement `IValueParser<T>` for custom span-based conversion. Custom parsers
receive the complete capture and the resolved provider, including inside nested
parsers. They control their own empty/null policy. Exceptions propagate.
Parser instances are retained, not cloned: keep their behavior stable and make
them thread-safe when sharing a compiled parser or a builder fork.
For null-argument checks, use `format: null` or `parser: null` to select an overload.

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
| `Bind(selector, format)` | Use BCL exact-format conversion |
| `Bind(selector, IValueParser<TValue>)` | Use a custom or composed field parser |
| `Fork()` | Copy the current builder configuration into an independent branch |
| `Compile()` | Finish explicit binding |
| `FormatParser<T>.Parse` / `TryParse` | Reuse a compiled parser |

`Parse` and `TryParse` accept `string` or `span` input, with provider overloads.
For `TryParse`, the provider precedes the out result. Complete signatures and
parameter documentation are available through the library's XML documentation.

Pattern defines text structure; `T` defines the target and value types; bindings
select constructor arguments. Typed field contexts leave room for future
parsing and validation policies. Fluent Format, Validate and ParseWith policy APIs
are not currently implemented; exact formats and custom parsers use Bind overloads.
Source generators, setter-based construction and nested member selectors
are also outside this release.

Compiled plans use spans, local capture ranges and typed construction delegates.
There is no per-input mutable state on the parser, reflection invocation during
parsing, or boxing of numeric fields. Compilation allocates; retained parsers
avoid that repeated cost. User providers and parsing code must be thread-safe
when shared. Runtime code generation is required; Native AOT and trimming are
not supported.

FormatParse is for structured text, not general serialization or a universal
inverse of .NET formatting. The API may change during 0.x releases.

Run the focused examples or the timed concurrent CSV pipeline from the repository root:

```sh
dotnet run -c Release --project examples/FormatParse.Example
dotnet run -c Release --project examples/FormatParse.Example -- --csv examples/FormatParse.Example/sample-5mb.csv 4
```

See [the examples guide](examples/README.md) for custom collection parsers, nested classes and worker options.
The [design](DESIGN.md) describes the complete API and roadmap.
See [benchmarks](benchmarks/README.md) for Regex comparisons and the
