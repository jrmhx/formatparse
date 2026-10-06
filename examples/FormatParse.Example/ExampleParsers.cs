using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FormatParse.Example;

internal readonly record struct HexId(int Value);

internal sealed class HexIdParser : IValueParser<HexId>
{
    public bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, out HexId value)
    {
        value = default;
        if (!input.StartsWith("0x", StringComparison.Ordinal)
            || !int.TryParse(input[2..], NumberStyles.AllowHexSpecifier, provider, out int number))
            return false;

        value = new HexId(number);
        return true;
    }
}

// Stateless and safe to share. JSON uses its own culture-independent syntax.
internal sealed class JsonValueParser<T> : IValueParser<T> where T : class
{
    public bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, [MaybeNullWhen(false)] out T value)
    {
        // System.Text.Json consumes UTF-8. Transcode into temporary storage, not a copied string.
        int capacity = Encoding.UTF8.GetMaxByteCount(input.Length);
        byte[]? rented = null;
        Span<byte> buffer = capacity <= 512
            ? stackalloc byte[capacity]
            : (rented = ArrayPool<byte>.Shared.Rent(capacity)).AsSpan(0, capacity);
        try
        {
            int written = Encoding.UTF8.GetBytes(input, buffer);
            value = JsonSerializer.Deserialize<T>(buffer[..written]);
            return value is not null;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }
}