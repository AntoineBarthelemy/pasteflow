using System.Text;
using PasteFlow.Core.Reference;

namespace PasteFlow.DataBuilder;

/// <summary>
/// A delimited text file read in memory, with its header, whatever its separator (";" or ",").
/// </summary>
internal sealed class CsvTable
{
    private readonly string[] _header;
    private readonly List<string[]> _rows;

    private CsvTable(string[] header, List<string[]> rows)
    {
        _header = header;
        _rows = rows;
    }

    /// <summary>
    /// Gets the rows, without the header.
    /// </summary>
    public IReadOnlyList<string[]> Rows => _rows;

    /// <summary>
    /// Reads a delimited text whose first line is the header.
    /// </summary>
    /// <param name="text">The content of the file.</param>
    /// <returns>The table.</returns>
    public static CsvTable Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = text.TrimStart('﻿').Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0).ToList();
        if (lines.Count == 0)
        {
            return new CsvTable(Array.Empty<string>(), new List<string[]>());
        }

        var separator = lines[0].Count(character => character == ';') >= lines[0].Count(character => character == ',') ? ';' : ',';
        var header = Split(lines[0], separator);
        var rows = lines.Skip(1).Select(line => Split(line, separator)).ToList();
        return new CsvTable(header, rows);
    }

    /// <summary>
    /// Finds the position of the first column whose name contains one of the given words, ignoring case and accents.
    /// </summary>
    /// <param name="words">Words that identify the column, such as "code_postal".</param>
    /// <returns>The position of the column.</returns>
    /// <exception cref="InvalidDataException">Thrown when no column matches: the format of the source changed.</exception>
    public int ColumnOf(params string[] words)
    {
        ArgumentNullException.ThrowIfNull(words);

        var index = FindColumn(words);
        return index >= 0
            ? index
            : throw new InvalidDataException($"No column named like {string.Join(" or ", words)} in: {string.Join(", ", _header)}");
    }

    /// <summary>
    /// Finds the position of the first column whose name contains one of the given words, if any.
    /// </summary>
    /// <param name="words">Words that identify the column.</param>
    /// <returns>The position of the column, or -1 when there is none.</returns>
    public int FindColumn(params string[] words)
    {
        ArgumentNullException.ThrowIfNull(words);

        for (var index = 0; index < _header.Length; index++)
        {
            var name = Simplify(_header[index]);
            if (words.Any(word => name.Contains(Simplify(word), StringComparison.Ordinal)))
            {
                return index;
            }
        }

        return -1;
    }

    private static string Simplify(string name)
    {
        return ReferenceText.RemoveAccents(name).ToUpperInvariant().Replace(' ', '_');
    }

    private static string[] Split(string line, char separator)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var isQuoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"' && isQuoted && index + 1 < line.Length && line[index + 1] == '"')
            {
                _ = field.Append('"');
                index++;
            }
            else if (character == '"')
            {
                isQuoted = !isQuoted;
            }
            else if (character == separator && !isQuoted)
            {
                fields.Add(field.ToString().Trim());
                _ = field.Clear();
            }
            else
            {
                _ = field.Append(character);
            }
        }

        fields.Add(field.ToString().Trim());
        return fields.ToArray();
    }
}
