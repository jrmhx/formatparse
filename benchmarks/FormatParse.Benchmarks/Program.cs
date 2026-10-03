using BenchmarkDotNet.Running;

using FormatParse.Benchmarks;

if (args is ["--validate"])
{
    foreach (InputCase inputCase in Enum.GetValues<InputCase>())
    {
        foreach (int nameLength in new[] { 8, 128 })
        {
            new ParsingBenchmarks { Case = inputCase, NameLength = nameLength }.Setup();
        }
    }

    new OneShotBenchmarks().Setup();
    Console.WriteLine("All benchmark parsers agree on success, failure, and typed results.");
    return;
}

var summaries = BenchmarkSwitcher.FromAssembly(typeof(ParsingBenchmarks).Assembly).Run(args).ToArray();
if (summaries.Length == 0 || summaries.Any(summary =>
        summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success)))
{
    Environment.ExitCode = 1;
}
