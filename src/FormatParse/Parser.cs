using System.Diagnostics.CodeAnalysis;

namespace FormatParse;

/// <summary>Parses structured text into typed values using positional captures.</summary>
/// <remarks>One-shot calls compile each pattern. Retain a compiled parser for repeated use.</remarks>
public static class Parser
{
    /// <summary>Compiles a pattern with captures bound in constructor parameter order.</summary>
    /// <typeparam name="T">A concrete target with one public instance constructor.</typeparam>
    /// <param name="pattern">A pattern containing literals, {}, and escaped braces.</param>
    /// <returns>An immutable, reusable parser.</returns>
    /// <exception cref="ArgumentNullException">The pattern is null.</exception>
    /// <exception cref="ArgumentException">The pattern, target, capture count, or conversion is unsupported.</exception>
    public static FormatParser<T> Compile<T>(string pattern)
    {
        Pattern parsed = Pattern.Parse(pattern);
        return new FormatParser<T>(parsed, TypeBinding<T>.Create(parsed.CaptureCount));
    }

    /// <summary>Starts an explicitly bound parser configuration.</summary>
    /// <typeparam name="T">The target type to construct.</typeparam>
    /// <param name="pattern">A pattern containing literals, {}, and escaped braces.</param>
    /// <returns>An immutable builder whose Bind calls select capture destinations.</returns>
    /// <exception cref="ArgumentNullException">The pattern is null.</exception>
    /// <exception cref="ArgumentException">The pattern is invalid.</exception>
    public static FormatParseBuilder<T> For<T>(string pattern)
    {
        return new FormatParseBuilder<T>(Pattern.Parse(pattern));
    }

    /// <summary>Parses a string using positional constructor binding.</summary>
    /// <typeparam name="T">The target type to construct.</typeparam>
    /// <param name="pattern">A pattern containing literals and positional captures.</param>
    /// <param name="input">The complete input to parse.</param>
    /// <returns>The constructed target.</returns>
    /// <exception cref="ArgumentNullException">The pattern or input is null.</exception>
    /// <exception cref="ArgumentException">The pattern or target configuration is invalid.</exception>
    /// <exception cref="FormatException">The input does not match or a conversion fails.</exception>
    public static T Parse<T>(string pattern, string input)
    {
        return Parse<T>(pattern, input, null);
    }

    /// <summary>Parses a string using positional constructor binding.</summary>
    /// <typeparam name="T">The target type to construct.</typeparam>
    /// <param name="pattern">A pattern containing literals and positional captures.</param>
    /// <param name="input">The complete input to parse.</param>
    /// <param name="provider">The conversion provider, or null for the current culture.</param>
    /// <returns>The constructed target.</returns>
    /// <exception cref="ArgumentNullException">The pattern or input is null.</exception>
    /// <exception cref="ArgumentException">The pattern or target configuration is invalid.</exception>
    /// <exception cref="FormatException">The input does not match or a conversion fails.</exception>
    public static T Parse<T>(string pattern, string input, IFormatProvider? provider)
    {
        return Compile<T>(pattern).Parse(input, provider);
    }

    /// <summary>Parses a span using positional constructor binding.</summary>
    /// <typeparam name="T">The target type to construct.</typeparam>
    /// <param name="pattern">A pattern containing literals and positional captures.</param>
    /// <param name="input">The complete input to parse.</param>
    /// <returns>The constructed target.</returns>
    /// <exception cref="ArgumentNullException">The pattern is null.</exception>
    /// <exception cref="ArgumentException">The pattern or target configuration is invalid.</exception>
    /// <exception cref="FormatException">The input does not match or a conversion fails.</exception>
    public static T Parse<T>(string pattern, ReadOnlySpan<char> input)
    {
        return Parse<T>(pattern, input, null);
    }

    /// <summary>Parses a span using positional constructor binding.</summary>
    /// <typeparam name="T">The target type to construct.</typeparam>
    /// <param name="pattern">A pattern containing literals and positional captures.</param>
    /// <param name="input">The complete input to parse.</param>
    /// <param name="provider">The conversion provider, or null for the current culture.</param>
    /// <returns>The constructed target.</returns>
    /// <exception cref="ArgumentNullException">The pattern is null.</exception>
    /// <exception cref="ArgumentException">The pattern or target configuration is invalid.</exception>
    /// <exception cref="FormatException">The input does not match or a conversion fails.</exception>
    public static T Parse<T>(string pattern, ReadOnlySpan<char> input, IFormatProvider? provider)
    {
        return Compile<T>(pattern).Parse(input, provider);
    }

    /// <summary>Attempts to parse a string using positional constructor binding.</summary>
    /// <typeparam name="T">The target type to construct.</typeparam>
    /// <param name="pattern">A pattern containing literals and positional captures.</param>
    /// <param name="input">The complete input to parse, or null.</param>
    /// <param name="result">The constructed target on success; otherwise, default.</param>
    /// <returns>True on success; false for input mismatch or conversion failure.</returns>
    /// <exception cref="ArgumentNullException">The pattern is null.</exception>
    /// <exception cref="ArgumentException">The pattern or target configuration is invalid.</exception>
    public static bool TryParse<T>(string pattern, string? input, [MaybeNullWhen(false)] out T result)
    {
        return TryParse(pattern, input, null, out result);
    }

    /// <summary>Attempts to parse a string using positional constructor binding.</summary>
    /// <typeparam name="T">The target type to construct.</typeparam>
    /// <param name="pattern">A pattern containing literals and positional captures.</param>
    /// <param name="input">The complete input to parse, or null.</param>
    /// <param name="provider">The conversion provider, or null for the current culture.</param>
    /// <param name="result">The constructed target on success; otherwise, default.</param>
    /// <returns>True on success; false for input mismatch or conversion failure.</returns>
    /// <exception cref="ArgumentNullException">The pattern is null.</exception>
    /// <exception cref="ArgumentException">The pattern or target configuration is invalid.</exception>
    public static bool TryParse<T>(string pattern, string? input, IFormatProvider? provider, [MaybeNullWhen(false)] out T result)
    {
        return Compile<T>(pattern).TryParse(input, provider, out result);
    }

    /// <summary>Attempts to parse a span using positional constructor binding.</summary>
    /// <typeparam name="T">The target type to construct.</typeparam>
    /// <param name="pattern">A pattern containing literals and positional captures.</param>
    /// <param name="input">The complete input to parse.</param>
    /// <param name="result">The constructed target on success; otherwise, default.</param>
    /// <returns>True on success; false for input mismatch or conversion failure.</returns>
    /// <exception cref="ArgumentNullException">The pattern is null.</exception>
    /// <exception cref="ArgumentException">The pattern or target configuration is invalid.</exception>
    public static bool TryParse<T>(string pattern, ReadOnlySpan<char> input, [MaybeNullWhen(false)] out T result)
    {
        return TryParse(pattern, input, null, out result);
    }

    /// <summary>Attempts to parse a span using positional constructor binding.</summary>
    /// <typeparam name="T">The target type to construct.</typeparam>
    /// <param name="pattern">A pattern containing literals and positional captures.</param>
    /// <param name="input">The complete input to parse.</param>
    /// <param name="provider">The conversion provider, or null for the current culture.</param>
    /// <param name="result">The constructed target on success; otherwise, default.</param>
    /// <returns>True on success; false for input mismatch or conversion failure.</returns>
    /// <exception cref="ArgumentNullException">The pattern is null.</exception>
    /// <exception cref="ArgumentException">The pattern or target configuration is invalid.</exception>
    public static bool TryParse<T>(string pattern, ReadOnlySpan<char> input, IFormatProvider? provider, [MaybeNullWhen(false)] out T result)
    {
        return Compile<T>(pattern).TryParse(input, provider, out result);
    }
}
