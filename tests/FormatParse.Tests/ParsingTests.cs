using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Numerics;

using FormatParse;

namespace FormatParse.Tests;

public sealed class ParsingTests
{
    [Fact]
    public void PositionalCapturesFollowConstructorOrder()
    {
        User expected = new("Alice", 18);
        foreach ((string pattern, string input) in new[]
        {
            ("User {} is {}", "User Alice is 18"),
            ("{} is {} years old", "Alice is 18 years old"),
            ("{}:{}", "Alice:18"),
        })
        {
            FormatParser<User> parser = Parser.Compile<User>(pattern);
            Assert.Equal(expected, parser.Parse(input));
            Assert.True(parser.TryParse(input, out User? result));
            Assert.Equal(expected, result);
        }

        Assert.Equal(new Triple(1, 2, 3), Parser.Parse<Triple>("{}:{}:{}", "1:2:3"));
    }

    [Fact]
    public void FirstDelimiterAndAnchoredSuffixDefineBoundaries()
    {
        FormatParser<Pair> parser = Parser.Compile<Pair>("{}:{}");
        Assert.Equal(new Pair("a", "b:c"), parser.Parse("a:b:c"));
        Assert.Equal(new Pair("", "b"), parser.Parse(":b"));
        Assert.Equal(new Pair("a", ""), parser.Parse("a:"));
        Assert.False(parser.TryParse("missing delimiter", out _));

        Assert.Equal("a]b", Parser.Parse<Text>("[{}]", "[a]b]").Value);
        Assert.Equal(" Alice ", Parser.Parse<Text>("[{}]", "[ Alice ]").Value);
        Assert.False(Parser.Compile<User>("{} is {}").TryParse("Alice is Bob is 18", out _));
    }

    [Fact]
    public void LiteralsEscapesAndEmptyCapturesHaveExactSemantics()
    {
        foreach ((string pattern, string input) in new[]
        {
            ("", ""), ("ready", "ready"), ("{{ready}}", "{ready}"), ("{{}}", "{}"),
        })
        {
            FormatParser<Empty> parser = Parser.Compile<Empty>(pattern);
            Assert.NotNull(parser.Parse(input));
            Assert.False(parser.TryParse(input + "extra", out _));
        }

        foreach ((string pattern, string input) in new[]
        {
            ("{}", "42"), ("ID={}", "ID=42"), ("{{{}}}", "{42}"), ("{{{{{}}}}}", "{{42}}"),
        })
        {
            Assert.Equal(new Number(42), Parser.Parse<Number>(pattern, input));
        }

        Assert.Equal("", Parser.Parse<Text>("{}", ReadOnlySpan<char>.Empty).Value);
        Assert.Equal("null", Parser.Parse<Text>("{}", "null").Value);
    }

    [Fact]
    public void InputFailuresReturnDefaultAndParseThrows()
    {
        FormatParser<User> parser = Parser.Compile<User>("User {} is {}!");
        foreach (string input in new[]
        {
            "user Alice is 18!", "prefix User Alice is 18!", "User Alice is 18!suffix",
            "User Alice is many!", "User Alice is 9999999999999999999!", "User Alice is 18",
        })
        {
            Assert.False(parser.TryParse(input, out User? result));
            Assert.Null(result);
            Assert.Throws<FormatException>(() => parser.Parse(input));
        }

        FormatParser<Number> numeric = Parser.Compile<Number>("{}");
        Assert.False(numeric.TryParse("bad", out Number number));
        Assert.Equal(default, number);
        Assert.False(numeric.TryParse(ReadOnlySpan<char>.Empty, out _));
        Assert.False(Parser.Compile<Empty>(" ready ").TryParse("ready", out _));
    }

    [Fact]
    public void InvalidGrammarAndCaptureCountsFailBeforeInput()
    {
        foreach (string pattern in new[] { "{", "}", "{Name}", "{int}", "{:F2}", "{,10}", "{}{}", "{}x}", "{{}" })
        {
            ArgumentException error = Assert.Throws<ArgumentException>(() => Parser.Compile<Number>(pattern));
            Assert.Equal("pattern", error.ParamName);
            Assert.Contains("position", error.Message, StringComparison.Ordinal);
            Assert.Throws<ArgumentException>(() => Parser.TryParse<Number>(pattern, (string?)null, out _));
        }

        Assert.Throws<ArgumentException>(() => Parser.Compile<User>("{}"));
        Assert.Throws<ArgumentException>(() => Parser.Compile<Number>("{}:{}"));
        Assert.Throws<ArgumentException>(() => Parser.Compile<Number>("ready"));
    }

    [Fact]
    public void AllStringSpanAndProviderOverloadsAgree()
    {
        const string pattern = "ID={}";
        const string input = "ID=42";
        ReadOnlySpan<char> slice = "[ID=42]".AsSpan(1, input.Length);
        IFormatProvider provider = CultureInfo.InvariantCulture;
        Number expected = new(42);
        FormatParser<Number> parser = Parser.Compile<Number>(pattern);

        Assert.Equal(expected, Parser.Parse<Number>(pattern, input));
        Assert.Equal(expected, Parser.Parse<Number>(pattern, input, provider));
        Assert.Equal(expected, Parser.Parse<Number>(pattern, slice));
        Assert.Equal(expected, Parser.Parse<Number>(pattern, slice, provider));
        Assert.True(Parser.TryParse<Number>(pattern, input, out Number a));
        Assert.True(Parser.TryParse<Number>(pattern, input, provider, out Number b));
        Assert.True(Parser.TryParse<Number>(pattern, slice, out Number c));
        Assert.True(Parser.TryParse<Number>(pattern, slice, provider, out Number d));

        Assert.Equal(expected, parser.Parse(input));
        Assert.Equal(expected, parser.Parse(input, provider));
        Assert.Equal(expected, parser.Parse(slice));
        Assert.Equal(expected, parser.Parse(slice, provider));
        Assert.True(parser.TryParse(input, out Number e));
        Assert.True(parser.TryParse(input, provider, out Number f));
        Assert.True(parser.TryParse(slice, out Number g));
        Assert.True(parser.TryParse(slice, provider, out Number h));
        foreach (Number actual in new[] { a, b, c, d, e, f, g, h })
        {
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void NullArgumentsRemainDistinctFromEmptyCaptures()
    {
        Assert.Throws<ArgumentNullException>(() => Parser.Compile<Number>(null!));
        Assert.Throws<ArgumentNullException>(() => Parser.For<Number>(null!));
        Assert.Throws<ArgumentNullException>(() => Parser.Parse<Number>(null!, "42"));
        Assert.Throws<ArgumentNullException>(() => Parser.Parse<Number>("{}", (string)null!));
        Assert.Throws<ArgumentNullException>(() => Parser.TryParse<Number>(null!, (string?)null, out _));
        Assert.False(Parser.TryParse<Number>("{}", (string?)null, out _));

        FormatParser<Text> parser = Parser.Compile<Text>("{}");
        Assert.Throws<ArgumentNullException>(() => parser.Parse((string)null!));
        Assert.False(parser.TryParse((string?)null, out Text? result));
        Assert.Null(result);
        Assert.Equal("", parser.Parse(ReadOnlySpan<char>.Empty).Value);
    }

    [Fact]
    public void CultureIsResolvedPerCallAndCanBeOverridden()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        FormatParser<Value<decimal>> parser = Parser.Compile<Value<decimal>>("{}");
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(12.5m, parser.Parse("12,5").Item);
            Assert.Equal(12.5m, parser.Parse("12.5", CultureInfo.InvariantCulture).Item);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal(12.5m, parser.Parse("12.5").Item);
            Assert.True(parser.TryParse("12,5", CultureInfo.GetCultureInfo("fr-FR"), out Value<decimal> value));
            Assert.Equal(12.5m, value.Item);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void BuiltInScalarsHaveSuccessAndFailureCoverage()
    {
        AssertValue("255", byte.MaxValue);
        AssertValue("-128", sbyte.MinValue);
        AssertValue("-32768", short.MinValue);
        AssertValue("65535", ushort.MaxValue);
        AssertValue("-2147483648", int.MinValue);
        AssertValue("4294967295", uint.MaxValue);
        AssertValue("-9223372036854775808", long.MinValue);
        AssertValue("18446744073709551615", ulong.MaxValue);
        AssertValue("42", (nint)42);
        AssertValue("42", (nuint)42);
        AssertValue("42", (Int128)42);
        AssertValue("42", (UInt128)42);
        AssertValue("12.5", (Half)12.5);
        AssertValue("12.5", 12.5f);
        AssertValue("12.5", 12.5d);
        AssertValue("12.5", 12.5m);
        AssertValue("123456789012345678901234567890", BigInteger.Parse("123456789012345678901234567890", CultureInfo.InvariantCulture));
        AssertValue("true", true);
        AssertValue("A", 'A');
        AssertValue("a83feaf3-207d-47de-9f34-590daf297cc5", Guid.Parse("a83feaf3-207d-47de-9f34-590daf297cc5"));
        AssertValue("2026-10-04", new DateOnly(2026, 10, 4));
        AssertValue("12:34:56", new TimeOnly(12, 34, 56));
        AssertValue("01:02:03", new TimeSpan(1, 2, 3));
        AssertValue("2026-10-04T12:34:56", new DateTime(2026, 10, 4, 12, 34, 56));
        AssertValue("2026-10-04T12:34:56+02:00", new DateTimeOffset(2026, 10, 4, 12, 34, 56, TimeSpan.FromHours(2)));
        AssertValue("192.168.1.20", IPAddress.Parse("192.168.1.20"));
    }

    [Fact]
    public void EnumsUseCaseSensitiveBclRulesWithoutExtraValidation()
    {
        FormatParser<Value<Access>> parser = Parser.Compile<Value<Access>>("{}");
        Assert.Equal(Access.Read, parser.Parse("Read").Item);
        Assert.Equal(Access.Read | Access.Write, parser.Parse("Read, Write").Item);
        Assert.Equal((Access)8, parser.Parse("8").Item);
        Assert.False(parser.TryParse("read", out _));
        Assert.False(parser.TryParse("Unknown", out _));
        Assert.False(parser.TryParse("999999999999999999999", out _));
    }

    [Fact]
    public void NullableScalarsUseOnlyEmptyCapturesForNull()
    {
        FormatParser<Value<int?>> parser = Parser.Compile<Value<int?>>("[{}]");
        Assert.Null(parser.Parse("[]").Item);
        Assert.Equal(42, parser.Parse("[42]").Item);
        foreach (string input in new[] { "[ ]", "[null]", "[bad]" })
        {
            Assert.False(parser.TryParse(input, out _));
        }

        Assert.Null(Parser.Parse<Value<Access?>>("{}", "").Item);
        Assert.Equal(Access.Read, Parser.Parse<Value<Access?>>("{}", "Read").Item);
        Assert.False(Parser.Compile<Value<Access?>>("{}").TryParse("read", out _));
        Assert.Equal(' ', Parser.Parse<Value<char?>>("{}", " ").Item);
        Assert.Equal(12.5m, Parser.Parse<Value<decimal?>>("{}", "12,5", CultureInfo.GetCultureInfo("fr-FR")).Item);
        Assert.Null(Parser.Parse<Value<SpanId?>>("{}", "").Item);
        Assert.Equal(new SpanId(42), Parser.Parse<Value<SpanId?>>("{}", "42").Item);
        Assert.Throws<ArgumentException>(() => Parser.Compile<Value<Unsupported?>>("{}"));
    }

    [Fact]
    public void CustomParsingPrefersSpanAndPreservesFallbackAndExceptions()
    {
        AssertValue("42", new SpanId(42));
        AssertValue("42", new StringId(42));
        FormatParser<Value<ThrowingValue>> converter = Parser.Compile<Value<ThrowingValue>>("{}");
        Assert.Throws<InvalidOperationException>(() => converter.TryParse("42", out _));
        FormatParser<ThrowingConstructor> constructor = Parser.Compile<ThrowingConstructor>("{}");
        Assert.Throws<InvalidOperationException>(() => constructor.TryParse("42", out _));
        Assert.False(constructor.TryParse("bad", out _));
    }

    [Fact]
    public void UnsupportedTargetsAndConversionsFailAtCompilation()
    {
        Assert.Throws<ArgumentException>(() => Parser.Compile<MultipleConstructors>("{}"));
        Assert.Throws<ArgumentException>(() => Parser.Compile<PrivateConstructor>(""));
        Assert.Throws<ArgumentException>(() => Parser.Compile<AbstractTarget>("{}"));
        Assert.Throws<ArgumentException>(() => Parser.Compile<int?>("{}"));
        Assert.Throws<ArgumentException>(() => Parser.Compile<Value<object>>("{}"));
        Assert.Throws<ArgumentException>(() => Parser.Compile<ByRefConstructor>("{}"));
        Assert.Throws<ArgumentException>(() => Parser.Compile<OptionalConstructor>(""));
        Assert.Equal(42, Parser.Compile<OptionalConstructor>("{}").Parse("42").Value);
    }

    [Fact]
    public void CompiledParserIsReusableAndSafeForConcurrentCalls()
    {
        FormatParser<User> parser = Parser.Compile<User>("User {} is {}");
        Assert.Equal(new User("Alice", 18), parser.Parse("User Alice is 18"));
        Assert.False(parser.TryParse("bad", out _));
        Assert.Equal(new User("Bob", 24), parser.Parse("User Bob is 24"));
        Parallel.For(0, 1000, index =>
        {
            Assert.Equal(new User("User", index), parser.Parse($"User User is {index}"));
        });
    }

    [Fact]
    public void CompiledNumericParsingAndFailuresBeforeStringsDoNotAllocate()
    {
        FormatParser<Number> numeric = Parser.Compile<Number>("ID={}");
        FormatParser<User> user = Parser.Compile<User>("{} is {}");
        IFormatProvider provider = CultureInfo.InvariantCulture;
        for (int index = 0; index < 100; index++)
        {
            numeric.TryParse("ID=42".AsSpan(), provider, out _);
            numeric.TryParse("ID=bad".AsSpan(), provider, out _);
            user.TryParse("Alice is bad".AsSpan(), provider, out _);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        int total = 0;
        for (int index = 0; index < 1000; index++)
        {
            if (numeric.TryParse("ID=42".AsSpan(), provider, out Number number))
            {
                total += number.Item;
            }

            if (numeric.TryParse("ID=bad".AsSpan(), provider, out _) ||
                user.TryParse("Alice is bad".AsSpan(), provider, out _))
            {
                total = -1;
            }
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(42000, total);
        Assert.Equal(0, allocated);
    }

    private static void AssertValue<T>(string input, T expected)
    {
        FormatParser<Value<T>> parser = Parser.Compile<Value<T>>("{}");
        Assert.Equal(expected, parser.Parse(input, CultureInfo.InvariantCulture).Item);
        Assert.False(parser.TryParse("invalid", CultureInfo.InvariantCulture, out _));
    }

    private sealed record User(string Name, int Age);
    private readonly record struct Number(int Item);
    private readonly record struct Value<T>(T Item);
    private sealed record Text(string Value);
    private sealed record Pair(string Left, string Right);
    private readonly record struct Triple(int A, int B, int C);
    private sealed record Empty;
    private readonly record struct Unsupported;

    [Flags]
    private enum Access { Read = 1, Write = 2 }

    private sealed class MultipleConstructors
    {
        public MultipleConstructors() { }
        public MultipleConstructors(int value) { }
    }

    private sealed class PrivateConstructor
    {
        private PrivateConstructor() { }
    }

    private abstract class AbstractTarget
    {
        public AbstractTarget(int value) { }
    }

    private sealed class ByRefConstructor
    {
        public ByRefConstructor(ref int value) { }
    }

    private sealed class OptionalConstructor
    {
        public OptionalConstructor(int value = 7) { Value = value; }
        public int Value { get; }
    }

    private sealed class ThrowingConstructor
    {
        public ThrowingConstructor(int value) => throw new InvalidOperationException("Constructor failure.");
    }

    private readonly record struct SpanId(int Value) : ISpanParsable<SpanId>
    {
        public static SpanId Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(int.Parse(s, provider));
        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SpanId result)
        {
            bool success = int.TryParse(s, provider, out int value);
            result = new SpanId(value);
            return success;
        }

        public static SpanId Parse(string s, IFormatProvider? provider) => throw new InvalidOperationException("The span overload should be used.");
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SpanId result) => throw new InvalidOperationException("The span overload should be used.");
    }

    private readonly record struct StringId(int Value) : IParsable<StringId>
    {
        public static StringId Parse(string s, IFormatProvider? provider) => new(int.Parse(s, provider));
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out StringId result)
        {
            bool success = int.TryParse(s, provider, out int value);
            result = new StringId(value);
            return success;
        }
    }

    private readonly record struct ThrowingValue : IParsable<ThrowingValue>
    {
        public static ThrowingValue Parse(string s, IFormatProvider? provider) => throw new InvalidOperationException("Converter failure.");
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ThrowingValue result) => throw new InvalidOperationException("Converter failure.");
    }
}
