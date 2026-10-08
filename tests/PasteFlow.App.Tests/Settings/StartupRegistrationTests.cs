using PasteFlow.App.Settings;

namespace PasteFlow.App.Tests.Settings;

public sealed class StartupRegistrationTests : IDisposable
{
    private const string ExecutablePath = @"C:\Program Files\PasteFlow\PasteFlow.App.exe";

    // A name of its own, so that the tests never touch the real entry of the application
    private readonly StartupRegistration _registration = new($"PasteFlow.Tests.{Guid.NewGuid():N}");

    [Fact]
    public void TrySet_StartsAndStopsStartingWithWindows()
    {
        Assert.False(_registration.IsEnabled);

        Assert.True(_registration.TrySet(isEnabled: true, ExecutablePath));
        Assert.True(_registration.IsEnabled);

        Assert.True(_registration.TrySet(isEnabled: false, ExecutablePath));
        Assert.False(_registration.IsEnabled);
    }

    [Fact]
    public void TrySet_DisablingWhenNotEnabled_Succeeds()
    {
        Assert.True(_registration.TrySet(isEnabled: false, ExecutablePath));
        Assert.False(_registration.IsEnabled);
    }

    public void Dispose()
    {
        _ = _registration.TrySet(isEnabled: false, ExecutablePath);
    }
}
