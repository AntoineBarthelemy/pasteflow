using PasteFlow.Core.Forms;
using PasteFlow.Core.Reference;
using PasteFlow.Core.Screen;

namespace PasteFlow.Core.Candidates;

/// <summary>
/// Offers the pieces of text displayed in the active window and in the windows just behind it, and the values of
/// the filled fields of the windows behind.
/// </summary>
public sealed class ScreenCandidateSource : ICandidateSource, IScreenTextSource
{
    /// <summary>
    /// The farthest a value can be below the name of its owner, as the email at the bottom of a signature.
    /// </summary>
    private const int MaxLinesFromOwner = 8;

    private static readonly char[] LineBreaks = { '\r', '\n' };
    private static readonly char[] NameSeparators = { ' ', ',', ';', ':', '(', ')' };

    private static readonly HashSet<CandidateKind> OwnerKinds = new()
    {
        CandidateKind.FullName, CandidateKind.FirstName, CandidateKind.LastName, CandidateKind.Company,
    };

    private readonly IWindowTextReader _reader;
    private IReadOnlyList<WindowText> _lastWindows = Array.Empty<WindowText>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ScreenCandidateSource"/> class.
    /// </summary>
    /// <param name="reader">The component that reads the text of the windows.</param>
    public ScreenCandidateSource(IWindowTextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _reader = reader;
    }

    /// <inheritdoc/>
    public IReadOnlyList<WindowText> LastWindows => Volatile.Read(ref _lastWindows);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Candidate>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        var windows = await _reader.ReadWindowsAsync(cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _lastWindows, windows.OrderBy(window => window.Rank).ToList());

        return windows
            .OrderBy(window => window.Rank)
            .SelectMany(window => FromText(window).Concat(FromFields(window)))
            .ToList();
    }

    /// <summary>
    /// Cuts the text of a window into values, each tied to the person or company it belongs to.
    /// </summary>
    /// <param name="window">The window read.</param>
    /// <returns>The values of the text.</returns>
    private static IEnumerable<Candidate> FromText(WindowText window)
    {
        var pieces = TextSegmenter.Segment(window.Text);
        var lines = window.Text.Split(LineBreaks, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var pieceLines = LocateLines(pieces, lines);
        var ownerLines = pieces
            .Select((piece, index) => (piece.Kind, Line: pieceLines[index]))
            .Where(located => OwnerKinds.Contains(located.Kind) && located.Line >= 0)
            .Select(located => located.Line)
            .Concat(Enumerable.Range(0, lines.Length).Where(line => StartsWithPersonName(lines[line])))
            .ToHashSet();

        return pieces.Select((piece, index) => new Candidate
        {
            Value = piece.Value,
            Kind = piece.Kind,
            Label = piece.Label,
            SourceName = window.Title,
            SourceRank = window.Rank,
            IsReadFromImage = window.IsReadFromImage,
            Context = OwnerLine(pieceLines[index], ownerLines, lines),
        });
    }

    /// <summary>
    /// Finds the line of each piece, in order: the pieces come in their order of appearance.
    /// </summary>
    /// <param name="pieces">The pieces of the text.</param>
    /// <param name="lines">The lines of the text.</param>
    /// <returns>The index of the line of each piece, or -1 when it cannot be found.</returns>
    private static int[] LocateLines(IReadOnlyList<TextPiece> pieces, string[] lines)
    {
        var located = new int[pieces.Count];
        var cursor = 0;
        for (var index = 0; index < pieces.Count; index++)
        {
            var line = FindLine(pieces[index].Value, lines, cursor);
            if (line < 0)
            {
                line = FindLine(pieces[index].Value, lines, 0);
            }

            located[index] = line;
            cursor = Math.Max(line, cursor);
        }

        return located;
    }

    /// <summary>
    /// Determines whether a line starts with what looks like the name of a person, such as "Léa Martin, responsable
    /// communication": a known first name then a capitalized word. Even when the name is too ambiguous to be offered
    /// as a value ("Martin" is also a first name), it tells whose values follow.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns><see langword="true"/> if the line starts with a name; otherwise <see langword="false"/>.</returns>
    private static bool StartsWithPersonName(string line)
    {
        var words = line.Split(NameSeparators, 3, StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 2
            && char.IsUpper(words[0][0])
            && char.IsUpper(words[1][0])
            && words[1].Length > 1
            && ReferenceData.Shared.IsFirstName(words[0]);
    }

    private static int FindLine(string value, string[] lines, int start)
    {
        for (var line = start; line < lines.Length; line++)
        {
            if (lines[line].Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                return line;
            }
        }

        return -1;
    }

    /// <summary>
    /// Finds the person or company a value belongs to: the closest line naming one, on the line of the value or just
    /// above it, as in a signature or a list of contacts where the name comes first.
    /// </summary>
    /// <param name="line">The line of the value.</param>
    /// <param name="ownerLines">The lines naming a person or a company.</param>
    /// <param name="lines">The lines of the text.</param>
    /// <returns>The line of the owner, or an empty string when none is close enough.</returns>
    private static string OwnerLine(int line, HashSet<int> ownerLines, string[] lines)
    {
        for (var candidate = line; candidate >= 0 && line - candidate <= MaxLinesFromOwner; candidate--)
        {
            if (ownerLines.Contains(candidate))
            {
                return lines[candidate];
            }
        }

        return string.Empty;
    }

    private static IEnumerable<Candidate> FromFields(WindowText window)
    {
        return window.Fields.Where(IsFormValue).Select(field => FromField(field, window));
    }

    /// <summary>
    /// Determines whether a filled field holds a value of its form: what was typed in a search box is not one.
    /// </summary>
    /// <param name="field">The filled field.</param>
    /// <returns><see langword="true"/> if the value can be offered; otherwise <see langword="false"/>.</returns>
    private static bool IsFormValue(FieldText field)
    {
        return !string.IsNullOrWhiteSpace(field.Value)
            && !FillRules.IsSearchBox(new FormField { Id = string.Empty, Label = field.Label, Kind = FieldKind.Text, HasValue = true });
    }

    /// <summary>
    /// Offers the value of a filled field of another form, whole, with the label of its field: a form copied into
    /// another finds each value under the same label.
    /// </summary>
    /// <param name="field">The filled field.</param>
    /// <param name="window">The window of the field.</param>
    /// <returns>The candidate.</returns>
    private static Candidate FromField(FieldText field, WindowText window)
    {
        var value = field.Value.Trim();
        var shape = TextSegmenter.Segment(value, ReferenceData.Empty)
            .FirstOrDefault(piece => string.Equals(piece.Value, value, StringComparison.Ordinal))?.Kind ?? CandidateKind.Text;
        return new Candidate
        {
            Value = value,
            Kind = shape,
            Label = field.Label.Trim(),
            SourceName = window.Title,
            SourceRank = window.Rank,
            IsTypedInField = true,
        };
    }
}
