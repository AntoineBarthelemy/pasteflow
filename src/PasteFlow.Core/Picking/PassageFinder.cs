using System.Text;
using System.Text.RegularExpressions;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Reference;
using PasteFlow.Core.Screen;

namespace PasteFlow.Core.Picking;

/// <summary>
/// Finds, in the text of the windows read, the passages the user is typing a few letters of, cut where it makes sense.
/// </summary>
/// <remarks>
/// <para>Typing "agenda d'hu" finds "agenda d'Hugo": the passage starts where the typed letters start a word and
/// ends at the end of the last word. It never cuts a value in two (an email, a phone number, a date…) and keeps a
/// number with its unit ("sous 48 h"). It can then be widened to the sentence or the line, or adjusted word by word.</para>
/// <para>Case, accents and the kind of apostrophe are ignored: "lea" finds "Léa".</para>
/// </remarks>
public sealed partial class PassageFinder
{
    /// <summary>
    /// The number of passages offered at once.
    /// </summary>
    public const int MaxResults = 5;

    private const int MaxBlocks = 5000;
    private const int RegexTimeoutMilliseconds = 1000;

    private static readonly char[] LineBreaks = { '\r', '\n' };

    private readonly List<Block> _blocks;

    /// <summary>
    /// Initializes a new instance of the <see cref="PassageFinder"/> class.
    /// </summary>
    /// <param name="windows">The windows read, with their text and filled fields.</param>
    /// <param name="values">The values recognized in them, never cut in two.</param>
    public PassageFinder(IReadOnlyList<WindowText> windows, IReadOnlyList<Candidate> values)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(values);

        var recognized = values
            .Where(value => value.Kind != CandidateKind.Text && value.Value.Length > 1)
            .Select(value => value.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        _blocks = windows
            .OrderBy(window => window.Rank)
            .SelectMany(window => Lines(window.Text).Concat(window.Fields.SelectMany(field => Lines(field.Value)))
                .Select(line => new Block(line, Fold(line), window.Title, window.Rank, ValueSpans(line, recognized))))
            .Take(MaxBlocks)
            .ToList();
    }

    /// <summary>
    /// Gets a finder that finds nothing.
    /// </summary>
    public static PassageFinder Empty { get; } = new(Array.Empty<WindowText>(), Array.Empty<Candidate>());

    /// <summary>
    /// Adds the next word at the end of a passage.
    /// </summary>
    /// <param name="passage">The passage.</param>
    /// <returns>The longer passage, or the same one at the end of its line.</returns>
    public static Passage ExtendEnd(Passage passage)
    {
        ArgumentNullException.ThrowIfNull(passage);

        var text = passage.Block;
        var end = passage.End;
        while (end < text.Length && char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        while (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        return passage.WithBounds(passage.Start, end);
    }

    /// <summary>
    /// Removes the last word of a passage, keeping at least one word.
    /// </summary>
    /// <param name="passage">The passage.</param>
    /// <returns>The shorter passage, or the same one when it has one word left.</returns>
    public static Passage ShrinkEnd(Passage passage)
    {
        ArgumentNullException.ThrowIfNull(passage);

        var text = passage.Block;
        var end = passage.End;
        while (end > passage.Start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        while (end > passage.Start && !char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        while (end > passage.Start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return end > passage.Start ? passage.WithBounds(passage.Start, end) : passage;
    }

    /// <summary>
    /// Adds the previous word at the start of a passage.
    /// </summary>
    /// <param name="passage">The passage.</param>
    /// <returns>The longer passage, or the same one at the start of its line.</returns>
    public static Passage ExtendStart(Passage passage)
    {
        ArgumentNullException.ThrowIfNull(passage);

        var text = passage.Block;
        var start = passage.Start;
        while (start > 0 && char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        return passage.WithBounds(start, passage.End);
    }

    /// <summary>
    /// Removes the first word of a passage, keeping at least one word.
    /// </summary>
    /// <param name="passage">The passage.</param>
    /// <returns>The shorter passage, or the same one when it has one word left.</returns>
    public static Passage ShrinkStart(Passage passage)
    {
        ArgumentNullException.ThrowIfNull(passage);

        var text = passage.Block;
        var start = passage.Start;
        while (start < passage.End && !char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (start < passage.End && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        return start < passage.End ? passage.WithBounds(start, passage.End) : passage;
    }

    /// <summary>
    /// Finds the passages that contain the typed letters at the start of a word, closest windows first.
    /// </summary>
    /// <param name="query">The letters typed, such as "agenda d'hu".</param>
    /// <param name="cut">How much text around the letters to take.</param>
    /// <returns>Up to <see cref="MaxResults"/> distinct passages.</returns>
    public IReadOnlyList<Passage> Find(string query, PassageCut cut)
    {
        ArgumentNullException.ThrowIfNull(query);

        var folded = FoldQuery(query);
        var passages = new List<Passage>();
        if (folded.Length == 0)
        {
            return passages;
        }

        for (var blockIndex = 0; blockIndex < _blocks.Count; blockIndex++)
        {
            var block = _blocks[blockIndex];
            for (var index = block.Folded.IndexOf(folded, StringComparison.Ordinal);
                index >= 0;
                index = block.Folded.IndexOf(folded, index + 1, StringComparison.Ordinal))
            {
                if (index > 0 && IsWordCharacter(block.Folded[index - 1]))
                {
                    // Typed letters in the middle of a word are not what the user means
                    continue;
                }

                var passage = cut == PassageCut.List
                    ? ListAround(blockIndex) ?? Cut(block, index, index + folded.Length, PassageCut.Line)
                    : Cut(block, index, index + folded.Length, cut);
                if (passage.Text.Length > 0 && !passages.Exists(found => string.Equals(found.Text, passage.Text, StringComparison.Ordinal)))
                {
                    passages.Add(passage);
                }

                if (passages.Count == MaxResults)
                {
                    return passages;
                }
            }
        }

        return passages;
    }

    /// <summary>
    /// Folds a text for comparison, one character for one: lower case, no accent, one kind of apostrophe and space.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The folded text, of the same length.</returns>
    internal static string Fold(string text)
    {
        var folded = new char[text.Length];
        for (var index = 0; index < text.Length; index++)
        {
            folded[index] = FoldCharacter(text[index]);
        }

        return new string(folded);
    }

    /// <summary>
    /// Folds what the user typed, as <see cref="Fold"/> does, with single spaces between the words.
    /// </summary>
    /// <param name="query">The words typed.</param>
    /// <returns>The folded words.</returns>
    internal static string FoldQuery(string query)
    {
        return string.Join(' ', Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static char FoldCharacter(char character)
    {
        if (character < 128)
        {
            return char.IsWhiteSpace(character) ? ' ' : char.ToLowerInvariant(character);
        }

        if (character is '’' or '‘' or 'ʼ')
        {
            return '\'';
        }

        if (char.IsWhiteSpace(character))
        {
            return ' ';
        }

        // Half of an emoji cannot be decomposed alone: it stays as it is, the text keeps its length
        if (char.IsSurrogate(character))
        {
            return character;
        }

        var decomposed = ReferenceText.NormalizeUnicode(character.ToString(), NormalizationForm.FormD);
        return char.ToLowerInvariant(decomposed[0]);
    }

    private static string[] Lines(string text)
    {
        return text.Split(LineBreaks, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static List<(int Start, int End)> ValueSpans(string line, List<string> values)
    {
        var spans = new List<(int Start, int End)>();
        foreach (var value in values)
        {
            for (var index = line.IndexOf(value, StringComparison.OrdinalIgnoreCase);
                index >= 0;
                index = line.IndexOf(value, index + value.Length, StringComparison.OrdinalIgnoreCase))
            {
                spans.Add((index, index + value.Length));
            }
        }

        return spans;
    }

    private static bool IsWordCharacter(char character)
    {
        return char.IsLetterOrDigit(character);
    }

    private static bool IsJoiner(char character)
    {
        return character is '\'' or '’' or '-';
    }

    private static bool IsListItem(string line)
    {
        return ListItemRegex().IsMatch(line);
    }

    private static Passage Cut(Block block, int matchStart, int matchEnd, PassageCut cut)
    {
        var text = block.Text;
        int start;
        int end;
        switch (cut)
        {
            case PassageCut.Line:
                start = 0;
                end = text.Length;
                break;
            case PassageCut.Sentence:
                start = SentenceStart(text, matchStart);
                end = SentenceEnd(text, matchEnd);
                break;
            default:
                (start, end) = SnapToValues(block, matchStart, WordEnd(text, matchEnd));
                end = WithUnit(text, end);
                break;
        }

        return new Passage
        {
            Text = text[start..end].Trim(),
            SourceName = block.SourceName,
            SourceRank = block.SourceRank,
            Block = text,
            Start = start,
            End = end,
        };
    }

    // Ends a passage at the end of its last word: "d'hu" becomes "d'Hugo"
    private static int WordEnd(string text, int end)
    {
        while (end < text.Length
            && (IsWordCharacter(text[end]) || (IsJoiner(text[end]) && end + 1 < text.Length && IsWordCharacter(text[end + 1]))))
        {
            end++;
        }

        return end;
    }

    // Widens a passage to every value it touches, so that an email or a phone number is never cut in two
    private static (int Start, int End) SnapToValues(Block block, int start, int end)
    {
        foreach (var (valueStart, valueEnd) in block.Values)
        {
            if (valueStart < end && valueEnd > start)
            {
                start = Math.Min(start, valueStart);
                end = Math.Max(end, valueEnd);
            }
        }

        return (start, end);
    }

    // Keeps a number with its unit: "sous 48" becomes "sous 48 h"
    private static int WithUnit(string text, int end)
    {
        if (end == 0 || end >= text.Length || !char.IsDigit(text[end - 1]))
        {
            return end;
        }

        var unit = UnitRegex().Match(text[end..]);
        return unit.Success ? end + unit.Length : end;
    }

    private static int SentenceStart(string text, int index)
    {
        for (var position = index - 1; position > 0; position--)
        {
            if (IsSentenceEnd(text, position))
            {
                return position + 1;
            }
        }

        return 0;
    }

    private static int SentenceEnd(string text, int index)
    {
        for (var position = Math.Max(index - 1, 0); position < text.Length; position++)
        {
            if (IsSentenceEnd(text, position))
            {
                return position + 1;
            }
        }

        return text.Length;
    }

    private static bool IsSentenceEnd(string text, int position)
    {
        return text[position] is '.' or '!' or '?' && (position + 1 == text.Length || char.IsWhiteSpace(text[position + 1]));
    }

    [GeneratedRegex(@"^(?:[-•*–·▪◦]|\d{1,3}[.)])\s+\S", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(
        @"^\s?(?:h|min|s|€|%|km|kg|g|cm|mm|m|ans?|jours?|mois|heures?|euros?|HT|TTC)(?![\p{L}\p{N}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeoutMilliseconds)]
    private static partial Regex UnitRegex();

    // The whole list around an item: the items next to it in the same window, as « 1. … », « 2) … », « - … » or « • … »
    private Passage? ListAround(int blockIndex)
    {
        var block = _blocks[blockIndex];
        if (!IsListItem(block.Text))
        {
            return null;
        }

        bool SameList(int index) => index >= 0 && index < _blocks.Count
            && _blocks[index].SourceRank == block.SourceRank
            && string.Equals(_blocks[index].SourceName, block.SourceName, StringComparison.Ordinal)
            && IsListItem(_blocks[index].Text);

        var first = blockIndex;
        while (SameList(first - 1))
        {
            first--;
        }

        var last = blockIndex;
        while (SameList(last + 1))
        {
            last++;
        }

        var text = string.Join('\n', _blocks.Skip(first).Take(last - first + 1).Select(item => item.Text));
        return new Passage
        {
            Text = text,
            SourceName = block.SourceName,
            SourceRank = block.SourceRank,
            Block = text,
            Start = 0,
            End = text.Length,
        };
    }

    private sealed record Block(string Text, string Folded, string SourceName, int SourceRank, List<(int Start, int End)> Values);
}
