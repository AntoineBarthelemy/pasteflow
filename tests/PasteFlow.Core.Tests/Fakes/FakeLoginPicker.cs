using PasteFlow.Core.Logins;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Chooses an account as the test decided, and remembers what it was shown.
/// </summary>
internal sealed class FakeLoginPicker : ILoginPicker
{
    public Func<IReadOnlyList<VaultLogin>, VaultLogin?> Choose { get; set; } = logins => logins[0];

    public IReadOnlyList<VaultLogin>? Shown { get; private set; }

    public Task<VaultLogin?> ChooseAsync(IReadOnlyList<VaultLogin> logins, CancellationToken cancellationToken)
    {
        Shown = logins;
        return Task.FromResult(Choose(logins));
    }
}
