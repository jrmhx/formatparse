using System.Globalization;
using System.Text.RegularExpressions;

namespace FormatParse.Benchmarks;

public readonly record struct User(string Name, int Age);
public readonly record struct ParseResult(bool Success, User Value);
public enum InputCase { Success, LiteralMismatch, ConversionFailure }

internal static partial class RegexParsing
{
    internal const string Pattern = @"\AUser (?<name>.*?) is (?<age>.*)!\z";
    internal const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.Singleline;

    [GeneratedRegex(Pattern, Options, matchTimeoutMilliseconds: 1000)]
    internal static partial Regex Generated();

    internal static ParseResult Parse(Regex regex, string input)
    {
        Match match = regex.Match(input);
        if (!match.Success || !int.TryParse(match.Groups["age"].ValueSpan,
                NumberStyles.Integer, CultureInfo.InvariantCulture, out int age))
        {
            return default;
        }

        // Copy only the returned name, after numeric conversion succeeds.
        return new ParseResult(true, new User(match.Groups["name"].ValueSpan.ToString(), age));
    }

    internal static void Verify(ParseResult expected, params ParseResult[] results)
    {
        foreach (ParseResult result in results)
        {
            if (result != expected)
            {
                throw new InvalidOperationException($"Benchmark result mismatch: expected {expected}, got {result}.");
            }
        }
    }
}
