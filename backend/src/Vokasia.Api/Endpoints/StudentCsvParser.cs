using System.Text;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// Small RFC-4180-compatible row parser for the supported student import contract. It handles
/// quoted commas and escaped quotes without adding a package for a four-column import.
/// </summary>
public static class StudentCsvParser
{
    public static IReadOnlyList<string> ParseLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var closedQuote = false;

        for (var i = 0; i < line.Length; i++)
        {
            var current = line[i];
            if (inQuotes)
            {
                if (current != '"')
                {
                    field.Append(current);
                    continue;
                }

                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = false;
                    closedQuote = true;
                }

                continue;
            }

            if (current == '"')
            {
                if (field.Length != 0 || closedQuote)
                {
                    throw new FormatException("Tanda kutip CSV tidak valid.");
                }

                inQuotes = true;
            }
            else if (current == ',')
            {
                fields.Add(field.ToString().Trim());
                field.Clear();
                closedQuote = false;
            }
            else if (closedQuote)
            {
                if (!char.IsWhiteSpace(current))
                {
                    throw new FormatException("Karakter setelah tanda kutip CSV tidak valid.");
                }
            }
            else
            {
                field.Append(current);
            }
        }

        if (inQuotes)
        {
            throw new FormatException("Tanda kutip CSV belum ditutup.");
        }

        fields.Add(field.ToString().Trim());
        return fields;
    }
}
