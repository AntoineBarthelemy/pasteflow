using PasteFlow.Core.Activation;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Activation;

public sealed class ShortcutActivationServiceTests
{
    [Fact]
    public void Start_WhenRegistrationSucceeds_ListensToHotkey()
    {
        var listener = new FakeHotkeyListener();
        using var service = CreateService(listener);

        var isStarted = service.Start(Hotkey.Default);

        Assert.True(isStarted);
        Assert.Equal(Hotkey.Default, listener.RegisteredHotkey);
        Assert.Equal(Hotkey.Default, service.ActiveHotkey);
    }

    [Fact]
    public void Start_WhenHotkeyIsAlreadyUsed_ReturnsFalse()
    {
        var listener = new FakeHotkeyListener { AcceptsRegistration = false };
        using var service = CreateService(listener);

        var isStarted = service.Start(Hotkey.Default);

        Assert.False(isStarted);
        Assert.Null(service.ActiveHotkey);
    }

    [Fact]
    public void Start_WhenAlreadyStarted_ThrowsInvalidOperationException()
    {
        using var service = CreateService(new FakeHotkeyListener());
        service.Start(Hotkey.Default);

        Assert.Throws<InvalidOperationException>(() => service.Start(Hotkey.Default));
    }

    [Fact]
    public void Start_AfterDispose_ThrowsObjectDisposedException()
    {
        var service = CreateService(new FakeHotkeyListener());
        service.Dispose();

        Assert.Throws<ObjectDisposedException>(() => service.Start(Hotkey.Default));
    }

    [Fact]
    public void HotkeyPressed_AfterStart_ReadsActiveFormAndNotifiesSummary()
    {
        var listener = new FakeHotkeyListener();
        var formReader = new FakeFormReader
        {
            Fields = new[]
            {
                new FormField { Label = "Nom", Kind = FieldKind.Text, HasValue = false },
                new FormField { Label = "Email", Kind = FieldKind.Text, HasValue = true },
            },
        };
        var notifier = new FakeUserNotifier();
        using var service = new ShortcutActivationService(listener, formReader, notifier);
        service.Start(Hotkey.Default);

        listener.SimulatePress();

        Assert.Equal(1, formReader.ReadCount);
        var message = Assert.Single(notifier.Messages);
        Assert.StartsWith("2 champs trouvés : Nom (vide), Email (rempli).", message, StringComparison.Ordinal);
    }

    [Fact]
    public void HotkeyPressed_BeforeStart_DoesNothing()
    {
        var listener = new FakeHotkeyListener();
        var formReader = new FakeFormReader();
        var notifier = new FakeUserNotifier();
        using var service = new ShortcutActivationService(listener, formReader, notifier);

        listener.SimulatePress();

        Assert.Equal(0, formReader.ReadCount);
        Assert.Empty(notifier.Messages);
    }

    [Fact]
    public void HotkeyPressed_AfterDispose_DoesNothing()
    {
        var listener = new FakeHotkeyListener();
        var formReader = new FakeFormReader();
        var notifier = new FakeUserNotifier();
        var service = new ShortcutActivationService(listener, formReader, notifier);
        service.Start(Hotkey.Default);
        service.Dispose();

        listener.SimulatePress();

        Assert.Equal(0, formReader.ReadCount);
        Assert.Empty(notifier.Messages);
    }

    [Fact]
    public void Dispose_DoesNotDisposeListenerOwnedByCaller()
    {
        var listener = new FakeHotkeyListener();
        var service = CreateService(listener);

        service.Dispose();

        Assert.False(listener.IsDisposed);
    }

    private static ShortcutActivationService CreateService(FakeHotkeyListener listener)
    {
        return new ShortcutActivationService(listener, new FakeFormReader(), new FakeUserNotifier());
    }
}
