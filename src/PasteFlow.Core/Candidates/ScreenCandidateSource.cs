using PasteFlow.Core.Forms;
using PasteFlow.Core.Reference;
using PasteFlow.Core.Screen;

namespace PasteFlow.Core.Candidates;

/// <summary>
/// Offers the pieces of text displayed in the active window and in the windows just behind it, and the values of
/// the filled fields of the windows behind.
/// </summary>
public sealed class ScreenCandidateSource : ICandidateSource
{
    private readonly IWindowTextReader _reader;

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
    public async Task<IReadOnlyList<Candidate>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        var windows = await _reader.ReadWindowsAsync(cancellationToken).ConfigureAwait(false);

        return windows
            .OrderBy(window => window.Rank)
            .SelectMany(window => TextSegmenter.Segment(window.Text).Select(piece => new Candidate
            {
                Value = piece.Value,
                Kind = piece.Kind,
                Label = piece.Label,
                SourceName = window.Title,
                SourceRank = window.Rank,
                IsReadFromImage = window.IsReadFromImage,
            }).Concat(window.Fields.Where(IsFormValue).Select(field => FromField(field, window))))
            .ToList();
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
        };
    }
}
