using PasteFlow.Core.Notifications;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Notifier that records the messages instead of displaying them.
/// </summary>
internal sealed class FakeUserNotifier : IUserNotifier
{
    private readonly List<string> _messages = new();
    private readonly TaskCompletionSource<string> _firstMessage = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IReadOnlyList<string> Messages => _messages;

    /// <summary>
    /// Gets a task completed by the first message, to wait for work that ends asynchronously.
    /// </summary>
    public Task<string> FirstMessage => _firstMessage.Task;

    public void Notify(string message)
    {
        _messages.Add(message);
        _ = _firstMessage.TrySetResult(message);
    }
}
