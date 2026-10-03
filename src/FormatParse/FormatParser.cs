using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FormatParse;

/// <summary>Parses input using a compiled pattern and binding plan.</summary>
/// <typeparam name="T">The target type to construct.</typeparam>
/// <remarks>Instances are immutable and may be used concurrently. Providers and user code must also be thread-safe.</remarks>
public sealed class FormatParser<T>
{
    private const int StackCaptureLimit = 128;
    private readonly Pattern _pattern;
    private readonly ObjectParser<T> _parser;

    internal FormatParser(Pattern pattern, ObjectParser<T> parser)
    {
        _pattern = pattern;
        _parser = parser;
    }

    /// <summary>Parses a complete string using the current culture.</summary>
    /// <param name="input">The complete input to parse.</param>
    /// <returns>The constructed target.</returns>
    /// <exception cref="ArgumentNullException">The input is null.</exception>
    /// <exception cref="FormatException">The input does not match or a conversion fails.</exception>
    public T Parse(string input) => Parse(input, null);

    /// <summary>Parses a complete string using the supplied conversion provider.</summary>
    /// <param name="input">The complete input to parse.</param>
    /// <param name="provider">The conversion provider, or null for the current culture.</param>
    /// <returns>The constructed target.</returns>
    /// <exception cref="ArgumentNullException">The input is null.</exception>
    /// <exception cref="FormatException">The input does not match or a conversion fails.</exception>
    public T Parse(string input, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Parse(input.AsSpan(), provider);
    }

    /// <summary>Parses a complete span using the current culture.</summary>
    /// <param name="input">The complete input to parse.</param>
    /// <returns>The constructed target.</returns>
    /// <exception cref="FormatException">The input does not match or a conversion fails.</exception>
    public T Parse(ReadOnlySpan<char> input) => Parse(input, null);

    /// <summary>Parses a complete span using the supplied conversion provider.</summary>
    /// <param name="input">The complete input to parse.</param>
    /// <param name="provider">The conversion provider, or null for the current culture.</param>
    /// <returns>The constructed target.</returns>
    /// <exception cref="FormatException">The input does not match or a conversion fails.</exception>
    public T Parse(ReadOnlySpan<char> input, IFormatProvider? provider)
    {
        if (TryParse(input, provider, out T? result))
        {
            return result;
        }

        throw new FormatException("The input does not match the pattern or a field cannot be converted.");
    }

    /// <summary>Attempts to parse a string using the current culture.</summary>
    /// <param name="input">The input to parse, or null.</param>
    /// <param name="result">The constructed target on success; otherwise, default.</param>
    /// <returns>True on success; false for null input, mismatch, or conversion failure.</returns>
    public bool TryParse(string? input, [MaybeNullWhen(false)] out T result) => TryParse(input, null, out result);

    /// <summary>Attempts to parse a string using the supplied conversion provider.</summary>
    /// <param name="input">The input to parse, or null.</param>
    /// <param name="provider">The conversion provider, or null for the current culture.</param>
    /// <param name="result">The constructed target on success; otherwise, default.</param>
    /// <returns>True on success; false for null input, mismatch, or conversion failure.</returns>
    public bool TryParse(string? input, IFormatProvider? provider, [MaybeNullWhen(false)] out T result)
    {
        if (input is not null)
        {
            return TryParse(input.AsSpan(), provider, out result);
        }

        result = default;
        return false;
    }

    /// <summary>Attempts to parse a span using the current culture.</summary>
    /// <param name="input">The complete input to parse.</param>
    /// <param name="result">The constructed target on success; otherwise, default.</param>
    /// <returns>True on success; false for mismatch or conversion failure.</returns>
    public bool TryParse(ReadOnlySpan<char> input, [MaybeNullWhen(false)] out T result) => TryParse(input, null, out result);

    /// <summary>Attempts to parse a span using the supplied conversion provider.</summary>
    /// <param name="input">The complete input to parse.</param>
    /// <param name="provider">The conversion provider, or null for the current culture.</param>
    /// <param name="result">The constructed target on success; otherwise, default.</param>
    /// <returns>True on success; false for mismatch or conversion failure.</returns>
    public bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, [MaybeNullWhen(false)] out T result)
    {
        int count = _pattern.CaptureCount;
        Range[]? rented = null;
        Span<Range> captures = count <= StackCaptureLimit
            ? stackalloc Range[count]
            : (rented = ArrayPool<Range>.Shared.Rent(count)).AsSpan(0, count);

        try
        {
            if (!PatternMatcher.TryMatch(_pattern, input, captures))
            {
                result = default;
                return false;
            }

            return _parser(input, captures, provider ?? CultureInfo.CurrentCulture, out result);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<Range>.Shared.Return(rented);
            }
        }
    }
}
