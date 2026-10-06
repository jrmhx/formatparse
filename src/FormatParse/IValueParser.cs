using System.Diagnostics.CodeAnalysis;

namespace FormatParse;

/// <summary>Converts a captured span into a typed value.</summary>
/// <typeparam name="T">The parsed value type.</typeparam>
/// <remarks>Implementations used by shared compiled parsers must support concurrent calls.</remarks>
public interface IValueParser<T>
{
    /// <summary>Attempts to parse a complete span using the supplied provider.</summary>
    /// <param name="input">The complete captured text.</param>
    /// <param name="provider">The conversion provider, or null for the current culture.</param>
    /// <param name="value">The parsed value on success; otherwise, default.</param>
    /// <returns>True on success; false for ordinary input failures.</returns>
    bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, [MaybeNullWhen(false)] out T value);
}