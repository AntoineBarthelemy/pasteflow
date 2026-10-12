using PasteFlow.Core.Profile;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Profile store kept in memory.
/// </summary>
internal sealed class FakeProfileStore : IProfileStore
{
    public IReadOnlyList<ProfileValue> Saved { get; private set; } = Array.Empty<ProfileValue>();

    public int LoadCount { get; private set; }

    public bool FailsToSave { get; set; }

    public Task<IReadOnlyList<ProfileValue>> LoadAsync(CancellationToken cancellationToken)
    {
        LoadCount++;
        return Task.FromResult(Saved);
    }

    public Task<bool> TrySaveAsync(IReadOnlyList<ProfileValue> values, CancellationToken cancellationToken)
    {
        if (FailsToSave)
        {
            return Task.FromResult(false);
        }

        Saved = values.ToArray();
        return Task.FromResult(true);
    }

    public void Preload(params ProfileValue[] values)
    {
        Saved = values;
    }
}
