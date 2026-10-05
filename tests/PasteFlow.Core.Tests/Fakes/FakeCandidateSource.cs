using PasteFlow.Core.Candidates;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Candidate source that returns predefined candidates.
/// </summary>
internal sealed class FakeCandidateSource : ICandidateSource
{
    public IReadOnlyList<Candidate> Candidates { get; set; } = Array.Empty<Candidate>();

    public int ReadCount { get; private set; }

    public Task<IReadOnlyList<Candidate>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        ReadCount++;
        return Task.FromResult(Candidates);
    }
}
