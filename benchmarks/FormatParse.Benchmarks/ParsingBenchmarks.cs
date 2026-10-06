using System.Globalization;
using System.Text.RegularExpressions;

using BenchmarkDotNet.Attributes;

namespace FormatParse.Benchmarks;

[MemoryDiagnoser]
public class ParsingBenchmarks
{
    private FormatParser<User> _parser = null!;
    private Regex _regex = null!;
    private Regex _compiledRegex = null!;
    private string _input = null!;

    [Params(InputCase.Success, InputCase.LiteralMismatch, InputCase.ConversionFailure)]
    public InputCase Case { get; set; }

    [Params(8, 128)] public int NameLength { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _parser = Parser.Compile<User>("User {} is {}!");
        _regex = new Regex(RegexParsing.Pattern, RegexParsing.Options, TimeSpan.FromSeconds(1));
        _compiledRegex = new Regex(RegexParsing.Pattern, RegexParsing.Options | RegexOptions.Compiled,
            TimeSpan.FromSeconds(1));
        string name = new('A', NameLength);
        _input = Case switch
        {
            InputCase.Success => $"User {name} is 42!",
            InputCase.LiteralMismatch => $"user {name} is 42!",
            InputCase.ConversionFailure => $"User {name} is many!",
            _ => throw new ArgumentOutOfRangeException(nameof(Case)),
        };

        ParseResult expected = Case == InputCase.Success ? new(true, new User(name, 42)) : default;
        RegexParsing.Verify(expected, Regex(), CompiledRegex(), GeneratedRegex(), FormatParseCompiled());

        // Check edge-case equivalence outside the measured workload.
        foreach ((string input, ParseResult result) in new[]
                 {
                     ("User Alice is Bob is 42!", default(ParseResult)),
                     ("User Alice is 42!suffix", default(ParseResult)),
                     ("User Alice is 42!!", default(ParseResult)),
                     ("User Alice\nBob is 42!", new ParseResult(true, new User("Alice\nBob", 42))),
                     ("User  is 42!", new ParseResult(true, new User("", 42))),
                 })
        {
            bool success = _parser.TryParse(input, CultureInfo.InvariantCulture, out User value);
            RegexParsing.Verify(result, new ParseResult(success, value),
                RegexParsing.Parse(_regex, input), RegexParsing.Parse(_compiledRegex, input),
                RegexParsing.Parse(RegexParsing.Generated(), input));
        }
    }

    [Benchmark(Baseline = true)]
    public ParseResult Regex() => RegexParsing.Parse(_regex, _input);

    [Benchmark]
    public ParseResult CompiledRegex() => RegexParsing.Parse(_compiledRegex, _input);

    [Benchmark]
    public ParseResult GeneratedRegex() => RegexParsing.Parse(RegexParsing.Generated(), _input);

    [Benchmark]
    public ParseResult FormatParseCompiled()
    {
        bool success = _parser.TryParse(_input.AsSpan(), CultureInfo.InvariantCulture, out User value);
        return new ParseResult(success, value);
    }
}
