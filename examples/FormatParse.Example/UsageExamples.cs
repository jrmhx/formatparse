using System.Globalization;

using FormatParse;

namespace FormatParse.Example;

internal static class UsageExamples
{
    private sealed record User(string Name, int Age);

    private sealed record Order(int Id, decimal Price);

    private readonly record struct Number(int Value);

    public static void ParseTuple()
    {
        (int num, string name) = Parser.Parse<(int, string)>("#{}, name: {}", "#789, name: Alice");
        Console.WriteLine($"name: {name} is #{num}");
    }

    public static void ParseRecord()
    {
        // Captures follow the constructor's parameter order: Name, then Age.
        User user = Parser.Parse<User>("User {} is {}", "User Alice is 18");
        Console.WriteLine($"{user.Name}: {user.Age}");
    }

    public static void TryParseRecord()
    {
        // Expected input failures return false and do not construct a partial result.
        if (Parser.TryParse<User>("User {} is {}", "User Alice is 18", out User? user))
        {
            Console.WriteLine(user.Name);
        }

        Console.WriteLine(Parser.TryParse<User>("User {} is {}", "User Alice is many", out _));
    }

    public static void CompileOnce()
    {
        // Keep this parser when processing many inputs. There is no hidden pattern cache.
        FormatParser<User> parser = Parser.Compile<User>("User {} is {}");
        Console.WriteLine(parser.Parse("User Alice is 18"));
        Console.WriteLine(parser.Parse("User Bob is 24"));
    }

    public static void BindMembers()
    {
        // Each Bind selects the destination for the next capture.
        // The contexts preserve int for Age and string for Name.
        FormatParser<User> parser = Parser.For<User>("Age={}; Name={}")
            .Bind(x => x.Age)
            .Bind(x => x.Name)
            .Compile();

        Console.WriteLine(parser.Parse("Age=18; Name=Alice"));
    }

    public static void ParseWithProvider()
    {
        FormatParser<Order> parser = Parser.Compile<Order>("Order #{}: ${}");
        Order order = parser.Parse("Order #42: $12.50", CultureInfo.InvariantCulture);
        Console.WriteLine(order.Price.ToString(CultureInfo.InvariantCulture));
    }

    public static void ParseSpan()
    {
        const string text = "[User Alice is 18]";
        ReadOnlySpan<char> input = text.AsSpan(1, text.Length - 2);
        FormatParser<User> parser = Parser.Compile<User>("User {} is {}");

        // Slicing borrows the input. The returned Name string still needs storage.
        Console.WriteLine(parser.Parse(input, CultureInfo.InvariantCulture));
    }

    public static void ParseEscapedBraces()
    {
        // The first and last brace pairs are literals; the middle pair is a capture.
        Console.WriteLine(Parser.Parse<Number>("{{{}}}", "{42}").Value);
    }
}
