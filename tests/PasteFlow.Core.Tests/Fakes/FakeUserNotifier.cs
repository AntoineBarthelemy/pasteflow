using PasteFlow.Core.Notifications;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Notifier that records the messages instead of displaying them.
/// </summary>
internal sealed class FakeUserNotifier : IUserNotifier
{
    private readonly List<string> _messages = new();

    public IReadOnlyList<string> Messages => _messages;

    public void Notify(string message)
    {
        _messages.Add(message);
    }
}
