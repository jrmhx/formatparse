# Initial results

Measured on October 4, 2026 v0.1.0 with BenchmarkDotNet 0.15.8, .NET 8.0.31,
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

```sh
// * Summary *

BenchmarkDotNet v0.15.8, Linux Debian GNU/Linux 13 (trixie)
12th Gen Intel Core i7-12700KF 0.80GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 8.0.31 (8.0.31, 8.0.3126.42015), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 8.0.31 (8.0.31, 8.0.3126.42015), X64 RyuJIT x86-64-v3


| Method              | Case              | NameLength | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |------------------ |----------- |-------------:|-----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| Regex               | Success           | 8          |   440.153 ns |  4.2304 ns |  3.9571 ns |  1.00 |    0.01 | 0.0415 |     544 B |        1.00 |
| CompiledRegex       | Success           | 8          |   243.811 ns |  2.9743 ns |  2.6367 ns |  0.55 |    0.01 | 0.0415 |     544 B |        1.00 |
| GeneratedRegex      | Success           | 8          |   164.896 ns |  1.3626 ns |  1.2746 ns |  0.37 |    0.00 | 0.0415 |     544 B |        1.00 |
| FormatParseCompiled | Success           | 8          |    29.146 ns |  0.1948 ns |  0.1822 ns |  0.07 |    0.00 | 0.0030 |      40 B |        0.07 |
|                     |                   |            |              |            |            |       |         |        |           |             |
| Regex               | Success           | 128        | 3,195.210 ns | 21.0231 ns | 19.6650 ns |  1.00 |    0.01 | 0.0572 |     784 B |        1.00 |
| CompiledRegex       | Success           | 128        | 1,393.631 ns | 16.3862 ns | 15.3277 ns |  0.44 |    0.01 | 0.0591 |     784 B |        1.00 |
| GeneratedRegex      | Success           | 128        |   174.630 ns |  2.3122 ns |  1.9308 ns |  0.05 |    0.00 | 0.0598 |     784 B |        1.00 |
| FormatParseCompiled | Success           | 128        |    43.932 ns |  0.9076 ns |  2.1392 ns |  0.01 |    0.00 | 0.0214 |     280 B |        0.36 |
|                     |                   |            |              |            |            |       |         |        |           |             |
| Regex               | LiteralMismatch   | 8          |    56.671 ns |  0.5967 ns |  0.5581 ns |  1.00 |    0.01 |      - |         - |          NA |
| CompiledRegex       | LiteralMismatch   | 8          |    21.307 ns |  0.0903 ns |  0.0754 ns |  0.38 |    0.00 |      - |         - |          NA |
| GeneratedRegex      | LiteralMismatch   | 8          |    18.305 ns |  0.2116 ns |  0.1979 ns |  0.32 |    0.00 |      - |         - |          NA |
| FormatParseCompiled | LiteralMismatch   | 8          |     5.457 ns |  0.0471 ns |  0.0418 ns |  0.10 |    0.00 |      - |         - |          NA |
|                     |                   |            |              |            |            |       |         |        |           |             |
| Regex               | LiteralMismatch   | 128        |    55.653 ns |  0.4702 ns |  0.4168 ns |  1.00 |    0.01 |      - |         - |          NA |
| CompiledRegex       | LiteralMismatch   | 128        |    21.705 ns |  0.1376 ns |  0.1287 ns |  0.39 |    0.00 |      - |         - |          NA |
| GeneratedRegex      | LiteralMismatch   | 128        |    19.521 ns |  0.1942 ns |  0.1817 ns |  0.35 |    0.00 |      - |         - |          NA |
| FormatParseCompiled | LiteralMismatch   | 128        |     5.431 ns |  0.0679 ns |  0.0635 ns |  0.10 |    0.00 |      - |         - |          NA |
|                     |                   |            |              |            |            |       |         |        |           |             |
| Regex               | ConversionFailure | 8          |   387.191 ns |  7.4062 ns |  7.6056 ns |  1.00 |    0.03 | 0.0381 |     504 B |        1.00 |
| CompiledRegex       | ConversionFailure | 8          |   206.090 ns |  1.6519 ns |  1.4644 ns |  0.53 |    0.01 | 0.0384 |     504 B |        1.00 |
| GeneratedRegex      | ConversionFailure | 8          |   138.859 ns |  1.2230 ns |  1.0213 ns |  0.36 |    0.01 | 0.0384 |     504 B |        1.00 |
| FormatParseCompiled | ConversionFailure | 8          |    20.201 ns |  0.2002 ns |  0.1873 ns |  0.05 |    0.00 |      - |         - |        0.00 |
|                     |                   |            |              |            |            |       |         |        |           |             |
| Regex               | ConversionFailure | 128        | 3,001.907 ns | 29.8110 ns | 26.4267 ns | 1.000 |    0.01 | 0.0381 |     504 B |        1.00 |
| CompiledRegex       | ConversionFailure | 128        | 1,334.340 ns |  6.6833 ns |  5.5809 ns | 0.445 |    0.00 | 0.0381 |     504 B |        1.00 |
| GeneratedRegex      | ConversionFailure | 128        |   146.613 ns |  2.9042 ns |  3.2280 ns | 0.049 |    0.00 | 0.0384 |     504 B |        1.00 |
| FormatParseCompiled | ConversionFailure | 128        |    24.620 ns |  0.3177 ns |  0.2816 ns | 0.008 |    0.00 |      - |         - |        0.00 |

// * Hints *
Outliers
  ParsingBenchmarks.CompiledRegex: Default       -> 1 outlier  was  removed (257.39 ns)
  ParsingBenchmarks.GeneratedRegex: Default      -> 2 outliers were removed (182.81 ns, 183.01 ns)
  ParsingBenchmarks.FormatParseCompiled: Default -> 1 outlier  was  removed (53.82 ns)
  ParsingBenchmarks.CompiledRegex: Default       -> 2 outliers were removed, 3 outliers were detected (22.59 ns, 23.00 ns, 23.27 ns)
  ParsingBenchmarks.FormatParseCompiled: Default -> 1 outlier  was  removed (7.11 ns)
  ParsingBenchmarks.Regex: Default               -> 1 outlier  was  removed (58.80 ns)
  ParsingBenchmarks.CompiledRegex: Default       -> 1 outlier  was  removed (217.43 ns)
  ParsingBenchmarks.GeneratedRegex: Default      -> 2 outliers were removed (148.34 ns, 149.97 ns)
  ParsingBenchmarks.Regex: Default               -> 1 outlier  was  removed (3.14 μs)
  ParsingBenchmarks.CompiledRegex: Default       -> 2 outliers were removed (1.36 μs, 1.37 μs)
  ParsingBenchmarks.FormatParseCompiled: Default -> 1 outlier  was  removed (29.04 ns)

// * Legends *
  Case        : Value of the 'Case' parameter
  NameLength  : Value of the 'NameLength' parameter
  Mean        : Arithmetic mean of all measurements
  Error       : Half of 99.9% confidence interval
  StdDev      : Standard deviation of all measurements
  Ratio       : Mean of the ratio distribution ([Current]/[Baseline])
  RatioSD     : Standard deviation of the ratio distribution ([Current]/[Baseline])
  Gen0        : GC Generation 0 collects per 1000 operations
  Allocated   : Allocated memory per single operation (managed only, inclusive, 1KB = 1024B)
  Alloc Ratio : Allocated memory ratio distribution ([Current]/[Baseline])
  1 ns        : 1 Nanosecond (0.000000001 sec)

// * Diagnostic Output - MemoryDiagnoser *


// ***** BenchmarkRunner: End *****
Run time: 00:08:51 (531.25 sec), executed benchmarks: 24

Global total time: 00:09:46 (586.84 sec), executed benchmarks: 27
// * Artifacts cleanup *
Artifacts cleanup is finished


```

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

```sh
// * Summary *

BenchmarkDotNet v0.15.8, Linux Debian GNU/Linux 13 (trixie)
12th Gen Intel Core i7-12700KF 0.80GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 8.0.31 (8.0.31, 8.0.3126.42015), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 8.0.31 (8.0.31, 8.0.3126.42015), X64 RyuJIT x86-64-v3


| Method        | Mean         | Error     | StdDev    | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------- |-------------:|----------:|----------:|-------:|--------:|-------:|----------:|------------:|
| Regex         |     1.879 μs | 0.0271 μs | 0.0254 μs |   1.00 |    0.02 | 0.3662 |   4.75 KB |        1.00 |
| CompiledRegex | 1,341.479 μs | 3.9401 μs | 3.6856 μs | 713.99 |    9.52 |      - |  15.56 KB |        3.28 |
| FormatParse   |   413.134 μs | 5.6935 μs | 5.3257 μs | 219.89 |    3.97 | 0.4883 |  11.95 KB |        2.52 |

// * Hints *
Outliers
  OneShotBenchmarks.FormatParse: Default -> 1 outlier  was  detected (403.10 μs)

// * Legends *
  Mean        : Arithmetic mean of all measurements
  Error       : Half of 99.9% confidence interval
  StdDev      : Standard deviation of all measurements
  Ratio       : Mean of the ratio distribution ([Current]/[Baseline])
  RatioSD     : Standard deviation of the ratio distribution ([Current]/[Baseline])
  Gen0        : GC Generation 0 collects per 1000 operations
  Allocated   : Allocated memory per single operation (managed only, inclusive, 1KB = 1024B)
  Alloc Ratio : Allocated memory ratio distribution ([Current]/[Baseline])
  1 μs        : 1 Microsecond (0.000001 sec)

// * Diagnostic Output - MemoryDiagnoser *


// ***** BenchmarkRunner: End *****
Run time: 00:00:50 (50.48 sec), executed benchmarks: 3
```


To reproduce this short run:

```sh
dotnet run -c Release --project benchmarks/FormatParse.Benchmarks -- --job Short --filter '*' --exporters json
```

BenchmarkDotNet writes the complete tables, confidence intervals, environment
details, and JSON reports to `BenchmarkDotNet.Artifacts/results`.
