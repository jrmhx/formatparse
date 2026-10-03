using System.Text;

namespace FormatParse;

internal sealed class Pattern
{
    private Pattern(string[] literals)
    {
        Literals = literals;
    }

    internal string[] Literals { get; }
    internal int CaptureCount => Literals.Length - 1;

    internal static Pattern Parse(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        List<string> literals = [];
        StringBuilder literal = new();

        for (int index = 0; index < pattern.Length; index++)
        {
            char character = pattern[index];
            if (character is not ('{' or '}'))
            {
                literal.Append(character);
                continue;
            }

            if (index + 1 < pattern.Length && pattern[index + 1] == character)
            {
                literal.Append(character);
                index++;
                continue;
            }

            if (character == '{' && index + 1 < pattern.Length && pattern[index + 1] == '}')
            {
                if (literals.Count > 0 && literal.Length == 0)
                {
                    throw InvalidPattern("Adjacent captures are not supported", index);
                }

                literals.Add(literal.ToString());
                literal.Clear();
                index++;
                continue;
            }

            throw InvalidPattern("Expected {}, {{, or }}", index);
        }

        literals.Add(literal.ToString());
        return new Pattern(literals.ToArray());
    }

    private static ArgumentException InvalidPattern(string message, int position)
    {
        return new ArgumentException($"{message} at position {position}.", "pattern");
    }
}
