namespace FormatParse;

internal static class PatternMatcher
{
    internal static bool TryMatch(Pattern pattern, ReadOnlySpan<char> input, Span<Range> captures)
    {
        ReadOnlySpan<char> prefix = pattern.Literals[0];
        if (!input.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (pattern.CaptureCount == 0)
        {
            return input.Length == prefix.Length;
        }

        int offset = prefix.Length;
        for (int captureIndex = 0; captureIndex < pattern.CaptureCount; captureIndex++)
        {
            ReadOnlySpan<char> remaining = input[offset..];
            ReadOnlySpan<char> separator = pattern.Literals[captureIndex + 1];
            int length;

            if (captureIndex == pattern.CaptureCount - 1)
            {
                // final literal fixes the boundary at the end of the input.
                if (!remaining.EndsWith(separator, StringComparison.Ordinal))
                {
                    return false;
                }

                length = remaining.Length - separator.Length;
            }
            else
            {
                // first occurrence determines the boundary; conversion never backtracks.
                length = remaining.IndexOf(separator, StringComparison.Ordinal);
                if (length < 0)
                {
                    return false;
                }
            }

            captures[captureIndex] = new Range(offset, offset + length);
            offset += length + separator.Length;
        }

        return offset == input.Length;
    }
}
