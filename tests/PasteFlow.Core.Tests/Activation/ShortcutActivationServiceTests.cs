using PasteFlow.Core.Activation;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Activation;

public sealed class ShortcutActivationServiceTests
{
    [Fact]
    public void Start_WhenRegistrationSucceeds_ListensToHotkey()
    {
        var listener = new FakeHotkeyListener();
        using var service = new ShortcutActivationService(listener, new FakeUserNotifier());

        var isStarted = service.Start(Hotkey.Default);

        Assert.True(isStarted);
        Assert.Equal(Hotkey.Default, listener.RegisteredHotkey);
        Assert.Equal(Hotkey.Default, service.ActiveHotkey);
    }

    [Fact]
    public void Start_WhenHotkeyIsAlreadyUsed_ReturnsFalse()
    {
        var listener = new FakeHotkeyListener { AcceptsRegistration = false };
        using var service = new ShortcutActivationService(listener, new FakeUserNotifier());

        var isStarted = service.Start(Hotkey.Default);

        Assert.False(isStarted);
        Assert.Null(service.ActiveHotkey);
    }

    [Fact]
    public void Start_WhenAlreadyStarted_ThrowsInvalidOperationException()
    {
        using var service = new ShortcutActivationService(new FakeHotkeyListener(), new FakeUserNotifier());
        service.Start(Hotkey.Default);

        Assert.Throws<InvalidOperationException>(() => service.Start(Hotkey.Default));
    }

    [Fact]
    public void Start_AfterDispose_ThrowsObjectDisposedException()
    {
        var service = new ShortcutActivationService(new FakeHotkeyListener(), new FakeUserNotifier());
        service.Dispose();

        Assert.Throws<ObjectDisposedException>(() => service.Start(Hotkey.Default));
    }

    [Fact]
    public void HotkeyPressed_AfterStart_NotifiesUserWithShortcut()
    {
        var listener = new FakeHotkeyListener();
        var notifier = new FakeUserNotifier();
        using var service = new ShortcutActivationService(listener, notifier);
        service.Start(Hotkey.Default);

        listener.SimulatePress();

        var message = Assert.Single(notifier.Messages);
        Assert.Contains("Ctrl+Alt+V", message, StringComparison.Ordinal);
    }

    [Fact]
    public void HotkeyPressed_BeforeStart_DoesNotNotifyUser()
    {
        var listener = new FakeHotkeyListener();
        var notifier = new FakeUserNotifier();
        using var service = new ShortcutActivationService(listener, notifier);

        listener.SimulatePress();

        Assert.Empty(notifier.Messages);
    }

    [Fact]
    public void HotkeyPressed_AfterDispose_DoesNotNotifyUser()
    {
        var listener = new FakeHotkeyListener();
        var notifier = new FakeUserNotifier();
        var service = new ShortcutActivationService(listener, notifier);
        service.Start(Hotkey.Default);
        service.Dispose();

        listener.SimulatePress();

        Assert.Empty(notifier.Messages);
    }

    [Fact]
    public void Dispose_DoesNotDisposeListenerOwnedByCaller()
    {
        var listener = new FakeHotkeyListener();
        var service = new ShortcutActivationService(listener, new FakeUserNotifier());

        service.Dispose();

        Assert.False(listener.IsDisposed);
    }
}
