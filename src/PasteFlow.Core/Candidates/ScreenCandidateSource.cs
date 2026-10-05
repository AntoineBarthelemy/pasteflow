using PasteFlow.Core.Screen;

namespace PasteFlow.Core.Candidates;

/// <summary>
/// Offers the pieces of text displayed in the active window and in the windows just behind it.
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
                Value = piece,
                SourceName = window.Title,
                SourceRank = window.Rank,
            }))
            .ToList();
    }
}
