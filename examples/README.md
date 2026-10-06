# Examples

Run from the repository root with the .NET 10 SDK:

```sh
dotnet run -c Release --project examples/FormatParse.Example
```

[UsageExamples.cs](FormatParse.Example/UsageExamples.cs) contains four focused examples:

| Example | Features |
| --- | --- |
| Tuple | Positional capture and escaped literal braces |
| Audit records | Compile once, enum, GUID, boolean, nullable values, culture and `TryParse` |
| Time window | Reordered bindings, exact date/time/GUID formats and independent `Fork()` branches |
| Complex report | Custom hexadecimal type, JSON list, dictionary of date lists, nested class and borrowed span slice |

[ExampleParsers.cs](FormatParse.Example/ExampleParsers.cs) defines the custom adapters.
The JSON adapter transcodes spans into stack or pooled UTF-8 storage and uses `System.Text.Json`.
It rejects malformed JSON and a `null` root, accepts empty collections, and ignores the culture provider because JSON has fixed syntax.
JSON dates use ISO text. These adapters are sample code, not built-in library features.
The nested `Address` parser uses a class with one public constructor; it does not require JSON.
Captured delimiters still follow the outer pattern: JSON quoting does not make delimiters invisible to FormatParse.

## Concurrent CSV processing

[CsvExample.cs](FormatParse.Example/CsvExample.cs) processes the existing
[sample-5mb.csv](FormatParse.Example/sample-5mb.csv) (64,659 data rows, approximately 5 MB).
The file is copied beside the executable, so the default command finds it:

```sh
dotnet run -c Release --project examples/FormatParse.Example -- --csv

# Choose an input file and worker count; compare one worker with four.
dotnet run -c Release --project examples/FormatParse.Example -- --csv examples/FormatParse.Example/sample-5mb.csv 1
dotnet run -c Release --project examples/FormatParse.Example -- --csv examples/FormatParse.Example/sample-5mb.csv 4
```

One `Task.Run()` producer reads lines into batches of 512. A bounded channel feeds a fixed number of
`Task.Run()` workers sharing one compiled parser. Each worker keeps its own row counts, ID sum and salary total;
results are combined after all tasks finish. There is no task per row or whole-file materialization.
The default worker count is the CPU count clamped to 2–8; an explicit count may be 1–64.

Output separates compilation time from end-to-end reading, queueing, parsing and aggregation time.
For the supplied file, expect 64,659 parsed rows, zero rejected rows, ID sum `2090425470` and salary total `7106873539.93`.
It also prints rejected rows and per-worker row counts. Compare totals, not worker distribution: scheduling is nondeterministic.
Invalid rows are counted and skipped; header, I/O or unexpected worker failures cancel the pipeline and return a nonzero exit code.

This is a demonstration of the sample's **unquoted, comma-separated** layout, not a general CSV reader.
Quoted rows are rejected; embedded commas and multiline fields are unsupported. Dates must match `yyyy-MM-dd`,
and numbers use invariant culture. Shared custom parsers and providers must also be thread-safe.
The timer includes I/O, JIT and scheduling; additional workers are not guaranteed to be faster.
Use [BenchmarkDotNet benchmarks](../benchmarks/README.md) for controlled parser comparisons.
