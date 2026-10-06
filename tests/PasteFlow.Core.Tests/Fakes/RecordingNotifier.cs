using PasteFlow.Core.Forms;
using PasteFlow.Core.Notifications;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Notifier that shows its own recap of fills, as the Windows application does, and records what it receives.
/// </summary>
internal sealed class RecordingNotifier : IUserNotifier
{
    private readonly List<string> _messages = new();
    private readonly List<FillReport> _reports = new();

    public IReadOnlyList<string> Messages => _messages;

    public IReadOnlyList<FillReport> Reports => _reports;

    public void Notify(string message)
    {
        _messages.Add(message);
    }

    public void NotifyFill(FillReport report)
    {
        _reports.Add(report);
    }
}
