using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FormatParse.Tests;

public sealed class FieldParserTests
{
    [Fact]
    public void ExactFormatsUseTheSupportedBclTypes()
    {
        AssertExact("yyyy-MM-dd", "2026-10-06", new DateTime(2026, 10, 6), "06/10/2026");
        AssertExact("yyyy-MM-dd HH:mm zzz", "2026-10-06 13:14 +02:00",
            new DateTimeOffset(2026, 10, 6, 13, 14, 0, TimeSpan.FromHours(2)), "2026-10-06");
        AssertExact("yyyy-MM-dd", "2026-10-06", new DateOnly(2026, 10, 6), "2026-02-30");
        AssertExact("HH:mm:ss", "13:14:15", new TimeOnly(13, 14, 15), "1:14:15 PM");
        AssertExact("c", "01:02:03", new TimeSpan(1, 2, 3), "one hour");
        AssertExact(@"hh\:mm", "01:02", new TimeSpan(1, 2, 0), "1:2");
        Guid id = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        AssertExact("D", id.ToString("D"), id, id.ToString("N"));
    }

    [Fact]
    public void ExactFormatsPropagateTheProviderAndResolveCurrentCulturePerCall()
    {
        var parser = Parser.For<Value<DateTime>>("{}").Bind(x => x.Item, "dd MMMM yyyy").Compile();
        CultureInfo french = CultureInfo.GetCultureInfo("fr-FR");
        Assert.Equal(new DateTime(2026, 10, 6), parser.Parse("06 octobre 2026", french).Item);
        Assert.False(parser.TryParse("06 octobre 2026", CultureInfo.InvariantCulture, out _));

        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = french;
            Assert.Equal(new DateTime(2026, 10, 6), parser.Parse("06 octobre 2026").Item);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.False(parser.TryParse("06 octobre 2026", out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        CultureInfo separators = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        separators.DateTimeFormat.DateSeparator = "#";
        var date = Parser.For<Value<DateOnly>>("{}").Bind(x => x.Item, "yyyy/MM/dd").Compile();
        Assert.Equal(new DateOnly(2026, 10, 6), date.Parse("2026#10#06", separators).Item);
    }

    [Fact]
    public void NullableExactFieldsUseEmptyCapturesForNull()
    {
        AssertNullableExact("yyyy-MM-dd", "2026-10-06", new DateTime(2026, 10, 6));
        AssertNullableExact("yyyy-MM-dd HH:mm zzz", "2026-10-06 13:14 +02:00",
            new DateTimeOffset(2026, 10, 6, 13, 14, 0, TimeSpan.FromHours(2)));
        AssertNullableExact("yyyy-MM-dd", "2026-10-06", new DateOnly(2026, 10, 6));
        AssertNullableExact("HH:mm:ss", "13:14:15", new TimeOnly(13, 14, 15));
        AssertNullableExact("c", "01:02:03", new TimeSpan(1, 2, 3));
        AssertNullableExact("D", Guid.Empty.ToString("D"), Guid.Empty);
    }

    [Fact]
    public void UnsupportedAndInvalidFormatsAreConfigurationErrors()
    {
        foreach (string format in new[] { "F2", "N2", "C2" })
        {
            Assert.Throws<ArgumentException>(() => Parser.For<Value<decimal>>("{}").Bind(x => x.Item, format));
        }

        Assert.Throws<ArgumentException>(() => Parser.For<Value<int?>>("{}").Bind(x => x.Item, "D"));
        Assert.Throws<ArgumentException>(() => Parser.For<Value<string>>("{}").Bind(x => x.Item, "D"));
        Assert.Throws<ArgumentException>(() => Parser.For<Value<DateTime>>("{}").Bind(x => x.Item, ""));
        Assert.Throws<ArgumentException>(() => Parser.For<Value<DateTime>>("{}").Bind(x => x.Item, "q"));
        Assert.Throws<ArgumentException>(() => Parser.For<Value<DateTime>>("{}").Bind(x => x.Item, "yyyy-MM-dd'"));
        Assert.Throws<ArgumentException>(() => Parser.For<Value<DateOnly>>("{}").Bind(x => x.Item, "HH:mm"));
        Assert.Throws<ArgumentException>(() => Parser.For<Value<TimeOnly>>("{}").Bind(x => x.Item, "yyyy-MM-dd"));
        Assert.Throws<ArgumentException>(() => Parser.For<Value<TimeSpan>>("{}").Bind(x => x.Item, "F2"));
        Assert.Throws<ArgumentException>(() => Parser.For<Value<Guid>>("{}").Bind(x => x.Item, "Q"));
    }

    [Fact]
    public void CustomParsersReceiveCapturedSpansAndTheResolvedProvider()
    {
        var fieldParser = new DecimalParser();
        var parser = Parser.For<Value<decimal>>("[{}]").Bind(x => x.Item, fieldParser).Compile();
        CultureInfo french = CultureInfo.GetCultureInfo("fr-FR");
        const string input = "prefix[12,5]suffix";
        Assert.True(parser.TryParse(input.AsSpan(6, 6), french, out Value<decimal>? value));
        Assert.Equal(12.5m, value.Item);
        Assert.Same(french, fieldParser.LastProvider);
        Assert.False(parser.TryParse("[invalid]", french, out value));
        Assert.Null(value);
    }

    [Fact]
    public void CompiledParsersComposeAndNestedFailuresPropagate()
    {
        IValueParser<Point> point = Parser.Compile<Point>("({}, {})");
        var parser = Parser.For<Event>("{} at {} on {}")
            .Bind(x => x.Name)
            .Bind(x => x.Position, point)
            .Bind(x => x.Date, "yyyy-MM-dd")
            .Compile();

        Assert.Equal(new Event("Alice", new Point(10, 20), new DateOnly(2026, 10, 6)),
            parser.Parse("Alice at (10, 20) on 2026-10-06", CultureInfo.InvariantCulture));
        foreach (string input in new[]
        {
            "Alice at (bad, 20) on 2026-10-06", "Alice at 10, 20 on 2026-10-06",
            "Alice at (10, 20) on 06/10/2026", "Alice at (10, 20) on 2026-10-06 extra",
        })
        {
            Assert.False(parser.TryParse(input, out Event? value));
            Assert.Null(value);
            Assert.Throws<FormatException>(() => parser.Parse(input));
        }

        Parallel.For(0, 100, index =>
        {
            Assert.Equal(new Point(index, 20), parser.Parse($"Alice at ({index}, 20) on 2026-10-06").Position);
        });
    }

    [Fact]
    public void NestedParsersInheritTheOuterProvider()
    {
        var nested = Parser.Compile<Value<decimal>>("({})");
        var parser = Parser.For<Value<Value<decimal>>>("{}").Bind(x => x.Item, nested).Compile();
        Assert.Equal(12.5m, parser.Parse("(12,5)", CultureInfo.GetCultureInfo("fr-FR")).Item.Item);
        Assert.Equal(12.5m, parser.Parse("(12.5)", CultureInfo.InvariantCulture).Item.Item);
    }

    [Fact]
    public void NewOverloadsPreserveValidationWithoutAppendingFailedBindings()
    {
        var root = Parser.For<Mixed>("{}:{}");
        Assert.Throws<ArgumentNullException>(() => root.Bind(x => x.Code, parser: null!));
        Assert.Throws<ArgumentNullException>(() => root.Bind(x => x.Date, format: null!));
        Assert.Throws<ArgumentNullException>(() => root.Bind<DateTime>(null!, "yyyyMMdd"));
        Assert.Throws<ArgumentNullException>(() => root.Bind<int>(null!, new HexParser()));
        Assert.Throws<ArgumentException>(() => root.Bind(x => x.Date.AddDays(1), "yyyyMMdd"));
        Assert.Throws<ArgumentException>(() => root.Bind(x => x.Code + 1, new HexParser()));
        Assert.Throws<ArgumentException>(() => root.Bind(x => x.Code, "F2"));
        Assert.Throws<ArgumentException>(root.Compile);

        var date = root.Bind(x => x.Date, "yyyyMMdd");
        Assert.Throws<ArgumentException>(() => date.Bind(x => x.Date, "yyyy-MM-dd"));
        date.Bind(x => x.Code, new HexParser());
        Assert.Throws<ArgumentException>(() => date.Bind(x => x.Date, "yyyyMMdd"));
        Assert.Throws<ArgumentException>(() => date.Bind(x => x.Code, new HexParser()));
        Assert.Equal(new Mixed(42, new DateTime(2026, 10, 6)), root.Compile().Parse("20261006:2A"));
    }

    [Fact]
    public void ForksRetainParserConfigurationAndAllowIndependentNewFormats()
    {
        var root = Parser.For<Mixed>("{}:{}");
        var common = root.Bind(x => x.Code, new HexParser());
        var compact = common.Fork().Bind(x => x.Date, "yyyyMMdd").Compile();
        var dashed = common.Fork().Bind(x => x.Date, "yyyy-MM-dd").Compile();
        Assert.Throws<ArgumentException>(common.Compile);
        common.Bind(x => x.Date, "dd/MM/yyyy");
        var original = root.Compile();

        Mixed expected = new(42, new DateTime(2026, 10, 6));
        Assert.Equal(expected, compact.Parse("2A:20261006", CultureInfo.InvariantCulture));
        Assert.Equal(expected, dashed.Parse("2A:2026-10-06", CultureInfo.InvariantCulture));
        Assert.Equal(expected, original.Parse("2A:06/10/2026", CultureInfo.InvariantCulture));
        Assert.Equal(expected, common.Fork().Compile().Parse("2A:06/10/2026", CultureInfo.InvariantCulture));
        Assert.False(compact.TryParse("2A:2026-10-06", out _));
    }

    [Fact]
    public async Task ForkedParsersRunConcurrentlyWithASharedNestedParser()
    {
        string[] formats = ["yyyy-MM-dd", "yyyyMMdd", "dd/MM/yyyy"];
        using Barrier firstCalls = new(formats.Length);
        var point = new FirstCallBarrierParser<Point>(Parser.Compile<Point>("({}, {})"), firstCalls);
        var common = Parser.For<Event>("{} at {} on {}")
            .Bind(x => x.Name)
            .Bind(x => x.Position, point);

        // Configuration is serial; only the compiled branches are used concurrently.
        FormatParser<Event>[] branches = formats
            .Select(format => common.Fork().Bind(x => x.Date, format).Compile())
            .ToArray();
        Assert.Throws<ArgumentException>(common.Compile);

        Task[] workers = Enumerable.Range(0, branches.Length).Select(branch => Task.Factory.StartNew(() =>
        {
            FormatParser<Event> parser = branches[branch];
            for (int index = 0; index < 500; index++)
            {
                Event expected = new($"User-{branch}-{index}", new Point(branch * 1_000 + index, -index),
                    new DateOnly(2026, 1, 1).AddDays(branch * 500 + index));
                string date = expected.Date.ToString(formats[branch], CultureInfo.InvariantCulture);
                string input = $"{expected.Name} at ({expected.Position.X}, {expected.Position.Y}) on {date}";

                Assert.Equal(expected, parser.Parse(input, CultureInfo.InvariantCulture));
                Assert.True(parser.TryParse(input.AsSpan(), CultureInfo.InvariantCulture, out Event? actual));
                Assert.Equal(expected, actual);

                string otherFormat = expected.Date.ToString(formats[(branch + 1) % formats.Length],
                    CultureInfo.InvariantCulture);
                string invalid = (index % 3) switch
                {
                    0 => $"{expected.Name} at ({expected.Position.X}, {expected.Position.Y}) on {otherFormat}",
                    1 => $"{expected.Name} at (bad, {expected.Position.Y}) on {date}",
                    _ => "missing literals",
                };
                Assert.False(parser.TryParse(invalid, CultureInfo.InvariantCulture, out actual));
                Assert.Null(actual);
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

        await Task.WhenAll(workers);
    }

    [Fact]
    public void CustomFailureValuesAreDiscardedAndExceptionsPropagate()
    {
        var rejected = Parser.For<Value<string>>("{}").Bind(x => x.Item, new RejectingParser()).Compile();
        Assert.False(rejected.TryParse("anything", out Value<string>? result));
        Assert.Null(result);
        Assert.Throws<FormatException>(() => rejected.Parse("anything"));

        var throwing = Parser.For<Number>("{}").Bind(x => x.Value, new ThrowingParser()).Compile();
        Assert.Throws<InvalidOperationException>(() => throwing.TryParse("42", out _));
    }

    [Fact]
    public void CustomNullableParsersOwnTheirEmptyInputPolicy()
    {
        var defaults = Parser.For<Value<int?>>("{}").Bind(x => x.Item).Compile();
        Assert.Null(defaults.Parse("").Item);
        var custom = Parser.For<Value<int?>>("{}").Bind(x => x.Item, new NullableNumberParser()).Compile();
        Assert.Equal(42, custom.Parse("42").Item);
        Assert.False(custom.TryParse("", out _));
    }

    [Fact]
    public void TypedCustomSpanParsingDoesNotAllocatePerInput()
    {
        var parser = Parser.For<Number>("{}").Bind(x => x.Value, new HexParser()).Compile();
        for (int index = 0; index < 100; index++)
        {
            parser.TryParse("2A".AsSpan(), out _);
        }

        int sum = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1_000; index++)
        {
            if (parser.TryParse("2A".AsSpan(), out Number value))
            {
                sum += value.Value;
            }
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(42_000, sum);
        Assert.Equal(0, allocated);
    }

    private static void AssertExact<T>(string format, string input, T expected, string invalid)
    {
        var parser = Parser.For<Value<T>>("{}").Bind(x => x.Item, format).Compile();
        Assert.Equal(expected, parser.Parse(input.AsSpan(), CultureInfo.InvariantCulture).Item);
        Assert.False(parser.TryParse(invalid, CultureInfo.InvariantCulture, out Value<T>? result));
        Assert.Null(result);
    }

    private static void AssertNullableExact<T>(string format, string input, T expected) where T : struct
    {
        var parser = Parser.For<Value<T?>>("{}").Bind(x => x.Item, format).Compile();
        Assert.Null(parser.Parse("").Item);
        Assert.Equal(expected, parser.Parse(input, CultureInfo.InvariantCulture).Item);
        Assert.False(parser.TryParse(" ", CultureInfo.InvariantCulture, out _));
        Assert.False(parser.TryParse("null", CultureInfo.InvariantCulture, out _));
    }

    private sealed record Value<T>(T Item);
    private readonly record struct Number(int Value);
    private sealed record Point(int X, int Y);
    private sealed record Event(string Name, Point Position, DateOnly Date);
    private sealed record Mixed(int Code, DateTime Date);

    private sealed class HexParser : IValueParser<int>
    {
        public bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, out int value)
        {
            return int.TryParse(input, NumberStyles.HexNumber, provider, out value);
        }
    }

    private sealed class FirstCallBarrierParser<T> : IValueParser<T>
    {
        private readonly IValueParser<T> _inner;
        private readonly Barrier _firstCalls;
        private int _calls;

        public FirstCallBarrierParser(IValueParser<T> inner, Barrier firstCalls)
        {
            _inner = inner;
            _firstCalls = firstCalls;
        }

        public bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, [MaybeNullWhen(false)] out T value)
        {
            // All branches must enter this shared parser before the first calls can finish.
            if (Interlocked.Increment(ref _calls) <= _firstCalls.ParticipantCount &&
                !_firstCalls.SignalAndWait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("The forked branches did not enter the shared parser concurrently.");
            }

            return _inner.TryParse(input, provider, out value);
        }
    }

    private sealed class DecimalParser : IValueParser<decimal>
    {
        public IFormatProvider? LastProvider { get; private set; }

        public bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, out decimal value)
        {
            LastProvider = provider;
            return decimal.TryParse(input, NumberStyles.Number, provider, out value);
        }
    }

    private sealed class RejectingParser : IValueParser<string>
    {
        public bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, [MaybeNullWhen(false)] out string value)
        {
            value = "must not escape";
            return false;
        }
    }

    private sealed class ThrowingParser : IValueParser<int>
    {
        public bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, out int value)
        {
            throw new InvalidOperationException("Custom parser failure.");
        }
    }

    private sealed class NullableNumberParser : IValueParser<int?>
    {
        public bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, out int? value)
        {
            bool success = int.TryParse(input, NumberStyles.Integer, provider, out int parsed);
            value = success ? parsed : null;
            return success;
        }
    }
}