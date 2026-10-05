using PasteFlow.Core.Screen;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Window text reader that returns predefined windows.
/// </summary>
internal sealed class FakeWindowTextReader : IWindowTextReader
{
    public IReadOnlyList<WindowText> Windows { get; init; } = Array.Empty<WindowText>();

    public Task<IReadOnlyList<WindowText>> ReadWindowsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Windows);
    }
}
