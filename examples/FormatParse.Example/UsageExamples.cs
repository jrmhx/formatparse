using System.Globalization;

namespace FormatParse.Example;

internal static class UsageExamples
{
    public static void Run()
    {
        Console.WriteLine("1. Positional tuples and literal braces");
        (int id, string name) = Parser.Parse<(int, string)>("#{{{}}}: {}", "#{42}: Alice");
        Console.WriteLine($"Id={id}, Name={name}");

        Console.WriteLine("2. Reuse, built-in types, culture and invalid input");
        ParseAuditLines();
        Console.WriteLine("3. Explicit bindings, exact formats and independent forks");
        ParseExactFormats();
        Console.WriteLine("4. Collections, nested classes, custom parsers and span slices");
        ParseComplexValues();
    }

    private static void ParseAuditLines()
    {
        // Positional captures follow the constructor. Retain the parser for repeated input.
        var parser = Parser.Compile<Audit>("Id={}; Level={}; Success={}; Retry={}; Price={}");
        Console.WriteLine(parser.Parse(
            "Id=550e8400-e29b-41d4-a716-446655440000; Level=Info; Success=true; Retry=; Price=12.50",
            CultureInfo.InvariantCulture));
        Console.WriteLine(parser.Parse(
            "Id=550e8400-e29b-41d4-a716-446655440000; Level=Warning; Success=false; Retry=2; Price=12,50",
            CultureInfo.GetCultureInfo("fr-FR")));

        // Empty nullable fields become null; invalid values return false without a partial result.
        bool accepted = parser.TryParse(
            "Id=invalid; Level=Info; Success=true; Retry=; Price=12.50",
            CultureInfo.InvariantCulture, out _);
        Require(!accepted, "Invalid GUIDs must be rejected.");
        Console.WriteLine($"Invalid input accepted: {accepted}");
    }

    private static void ParseExactFormats()
    {
        // Bind order follows input, not constructor order. Formats belong to the BCL types.
        var common = Parser.For<Window>("{} | {} | {} | {} | {} | {}")
            .Bind(x => x.Timestamp, "yyyy-MM-dd HH:mm:ss")
            .Bind(x => x.Offset, "O")
            .Bind(x => x.Date, "yyyy-MM-dd")
            .Bind(x => x.Time, "HH:mm:ss")
            .Bind(x => x.Duration, "c");

        // Fork before completing the last binding. Each branch has its own configuration.
        var dashed = common.Fork().Bind(x => x.Id, "D").Compile();
        var compact = common.Fork().Bind(x => x.Id, "N").Compile();
        const string prefix = "2026-10-06 13:14:15 | 2026-10-06T13:14:15.0000000+02:00 | 2026-10-06 | 13:14:15 | 01:02:03 | ";
        Window first = dashed.Parse(prefix + "550e8400-e29b-41d4-a716-446655440000", CultureInfo.InvariantCulture);
        Window second = compact.Parse(prefix + "550e8400e29b41d4a716446655440000", CultureInfo.InvariantCulture);
        Require(first == second, "Both branches must produce the same value.");
        Require(!dashed.TryParse(prefix + "550e8400e29b41d4a716446655440000", CultureInfo.InvariantCulture, out _),
            "The dashed branch must retain its exact format.");
        Console.WriteLine(first);
    }

    private static void ParseComplexValues()
    {
        // These adapters are example code, not built-in JSON or hexadecimal bindings.
        var scores = new JsonValueParser<List<int>>();
        var meetings = new JsonValueParser<Dictionary<string, List<DateOnly>>>();
        var address = Parser.Compile<Address>("City={} Postcode={}");
        var parser = Parser.For<Report>("id={} | scores={} | meetings={} | address={}")
            .Bind(x => x.Id, new HexIdParser())
            .Bind(x => x.Scores, scores)
            .Bind(x => x.Meetings, meetings)
            .Bind(x => x.Address, address)
            .Compile();

        const string text = "[id=0xff | scores=[10,20,30] | meetings={\"team\":[\"2026-10-06\",\"2026-11-01\"]} | address=City=Sydney Postcode=2000]";
        // Slice the envelope without copying it. Returned strings and collections still allocate.
        Report report = parser.Parse(text.AsSpan(1, text.Length - 2), CultureInfo.InvariantCulture);
        Require(report.Id.Value == 255 && report.Scores.SequenceEqual(new[] { 10, 20, 30 })
            && report.Meetings["team"][0] == new DateOnly(2026, 10, 6)
            && report.Address.Postcode == 2000, "Complex fields must retain their typed values.");
        Console.WriteLine($"Id={report.Id.Value}, Scores=[{string.Join(", ", report.Scores)}], Meetings={report.Meetings.Count}, City={report.Address.City}");

        Require(!parser.TryParse("id=0xff | scores=[bad] | meetings={} | address=City=Sydney Postcode=2000", out _),
            "Malformed JSON must be rejected.");
        Require(!scores.TryParse("null", null, out _), "This adapter rejects a null root.");
        Require(scores.TryParse("[]", null, out var empty) && empty.Count == 0, "Empty collections are valid.");
        string large = "[" + string.Join(",", Enumerable.Range(0, 1000)) + "]";
        Require(scores.TryParse(large, null, out var many) && many.Count == 1000, "Large JSON must use the pooled path correctly.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private enum Level { Info, Warning }
    private sealed record Audit(Guid Id, Level Level, bool Success, int? Retry, decimal Price);
    private sealed record Window(Guid Id, DateTime Timestamp, DateTimeOffset Offset, DateOnly Date, TimeOnly Time, TimeSpan Duration);
    private sealed record Report(HexId Id, List<int> Scores, Dictionary<string, List<DateOnly>> Meetings, Address Address);

    private sealed class Address
    {
        public Address(string city, int postcode)
        {
            City = city;
            Postcode = postcode;
        }

        public string City { get; }
        public int Postcode { get; }
    }
}