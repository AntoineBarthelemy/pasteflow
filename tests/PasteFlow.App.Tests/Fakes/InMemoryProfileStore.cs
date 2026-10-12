using PasteFlow.Core.Profile;

namespace PasteFlow.App.Tests.Fakes;

/// <summary>
/// Profile store kept in memory.
/// </summary>
internal sealed class InMemoryProfileStore : IProfileStore
{
    private IReadOnlyList<ProfileValue> _values;

    public InMemoryProfileStore(params ProfileValue[] values)
    {
        _values = values;
    }

    public IReadOnlyList<ProfileValue> Saved => _values;

    public Task<IReadOnlyList<ProfileValue>> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_values);
    }

    public Task<bool> TrySaveAsync(IReadOnlyList<ProfileValue> values, CancellationToken cancellationToken)
    {
        _values = values.ToArray();
        return Task.FromResult(true);
    }
}
