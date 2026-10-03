using System.Globalization;
using System.Text.RegularExpressions;

using BenchmarkDotNet.Attributes;

using FormatParse;

namespace FormatParse.Benchmarks;

// Include construction cost here; generated regex construction happens at build time.
[MemoryDiagnoser]
public class OneShotBenchmarks
{
    private const string Input = "User Alice is 42!";

    [GlobalSetup]
    public void Setup()
    {
        RegexParsing.Verify(new(true, new User("Alice", 42)), Regex(), CompiledRegex(), FormatParse());
    }

    [Benchmark(Baseline = true)]
    public ParseResult Regex()
    {
        var regex = new Regex(RegexParsing.Pattern, RegexParsing.Options, TimeSpan.FromSeconds(1));
        return RegexParsing.Parse(regex, Input);
    }

    [Benchmark]
    public ParseResult CompiledRegex()
    {
        var regex = new Regex(RegexParsing.Pattern, RegexParsing.Options | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        return RegexParsing.Parse(regex, Input);
    }

    [Benchmark]
    public ParseResult FormatParse()
    {
        bool success = Parser.TryParse<User>("User {} is {}!", Input, CultureInfo.InvariantCulture, out User value);
        return new ParseResult(success, value);
    }
}
