# Benchmarks

Run from the repository root in Release mode:

```sh
dotnet run -c Release --project benchmarks/FormatParse.Benchmarks -- --filter '*'
```

Use `--job short` for a shorter measured run or `--job dry` for a smoke check.
Use `-- --validate` to check result equivalence without timing.

## What is measured

| Suite | Comparison |
| --- | --- |
| ParsingBenchmarks | Reused Regex, RegexOptions.Compiled, GeneratedRegex, and FormatParser<T> |
| OneShotBenchmarks | A new Regex, a new compiled Regex, and static Parser.TryParse on each call |

Repeated parsing covers valid input, literal mismatch, and numeric conversion
failure, with short and long names. All methods return the same typed record
struct. Regex conversions use Group.ValueSpan and copy only the final string
field. GlobalSetup checks success and typed results before measurement.

The regex is anchored, uses a lazy first capture and a greedy final capture,
and treats newlines as ordinary characters. Numeric conversion occurs after
matching, with invariant culture, just as in the FormatParse workload.
All regex variants share a one-second timeout.

GeneratedRegex is generated during compilation. Its generation cost is not
comparable to runtime parser compilation, so it appears only in the repeated
parsing suite. Construction and parsing are included in the one-shot suite.

MemoryDiagnoser reports allocations as well as elapsed time. Results include
runtime and machine details in BenchmarkDotNet.Artifacts/results. Shared CI
runners are noisy: use the manual Benchmarks workflow for exploration and a
dedicated machine for performance conclusions. Do not turn small timing changes
into CI pass/fail thresholds.

BenchmarkDotNet is a development dependency of this project only. It is not a
runtime or transitive dependency of the FormatParse NuGet package.

See [the initial results](RESULTS.md) for one measured run and its limitations.
