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

    // process raw pattern string into a string[] literals
    // note that literals will contain an empty head and an empty tail
    // i.e. for pattern: "{}abc{}def{}" -> literals: ["", "abc", "def", ""]
    // this will make it easy to maintain an invariant that Literals.Length == CaptureCount + 1
    internal static Pattern Parse(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        List<string> literals = [];
        StringBuilder literal = new();

        for (int i = 0; i < pattern.Length; i++)
        {
            char character = pattern[i];
            if (character is not ('{' or '}'))
            {
                literal.Append(character);
                continue;
            }

            // {{ or }}
            if (i + 1 < pattern.Length && pattern[i + 1] == character)
            {
                literal.Append(character);
                i++;
                continue;
            }

            // {}
            if (character == '{' && i + 1 < pattern.Length && pattern[i + 1] == '}')
            {
                if (literals.Count > 0 && literal.Length == 0) // {}{} case
                {
                    throw InvalidPattern("Adjacent captures '{}{}' are not supported", i);
                }

                literals.Add(literal.ToString());
                literal.Clear();
                i++;
                continue;
            }

            throw InvalidPattern("Expected {}, {{, or }}", i);
        }

        literals.Add(literal.ToString());
        return new Pattern([.. literals]);
    }

    private static ArgumentException InvalidPattern(string message, int position)
    {
        return new ArgumentException($"{message} at position {position}.", "pattern");
    }
}
