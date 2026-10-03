# Initial results

Measured on October 4, 2026 with BenchmarkDotNet 0.15.8, .NET 8.0.31,
Debian 13, and an Intel Core i7-12700KF. The container reported one available
CPU. ShortRun used one launch, three warmups, and three measured iterations.
All 27 benchmark cases completed; result equivalence was checked before timing.

These are exploratory results for `User {} is {}!`, not a general performance
guarantee. The runner could not enable high process priority. Re-run the full
default job on your target machine before drawing production conclusions.

## Reused parsers: successful input

Times are means per parse; allocations include the returned name string.
Compilation and setup are excluded for every method.

| Method | 8-character name | Allocated | 128-character name | Allocated |
| --- | ---: | ---: | ---: | ---: |
| Regex | 431.1 ns | 544 B | 2,941.0 ns | 784 B |
| CompiledRegex | 251.6 ns | 544 B | 1,417.1 ns | 784 B |
| GeneratedRegex | 165.8 ns | 544 B | 184.3 ns | 784 B |
| FormatParseCompiled | 29.7 ns | 40 B | 42.4 ns | 280 B |

For failed integer conversion with an 8-character name, FormatParseCompiled
took 20.0 ns with no managed allocation; GeneratedRegex took 144.9 ns and
allocated 504 B. Both returned failure without constructing the result string.
Regex still allocated its match/capture objects.

## One-shot: construction and successful parsing

| Method | Mean | Allocated |
| --- | ---: | ---: |
| New Regex | 1.916 us | 4.75 KB |
| New compiled Regex | 1,359.440 us | 15.55 KB |
| Static Parser.TryParse | 404.670 us | 11.95 KB |

FormatParse's runtime expression compilation is expensive in this workload.
Hold on to the result of `Compile<T>()` when parsing repeatedly. GeneratedRegex
is excluded here because its code generation happens during the build.

The regex comparison uses Match and named groups to produce the same typed
result. It does not measure every possible regex-based parsing strategy, and
the two engines are not equivalent for every ambiguous delimiter pattern.
See [the benchmark setup](README.md) and source for the exact workload.

To reproduce this short run:

```sh
dotnet run -c Release --project benchmarks/FormatParse.Benchmarks -- --job Short --filter '*' --exporters json
```

BenchmarkDotNet writes the complete tables, confidence intervals, environment
details, and JSON reports to `BenchmarkDotNet.Artifacts/results`.
