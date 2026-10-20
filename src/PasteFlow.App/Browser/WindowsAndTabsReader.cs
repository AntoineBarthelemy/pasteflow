using PasteFlow.Core.Browser;
using PasteFlow.Core.Screen;

namespace PasteFlow.App.Browser;

/// <summary>
/// Reads the windows on screen and, at the same time, the recent tabs of the browsers through the extension: the tabs
/// hidden behind another tab come after the windows.
/// </summary>
internal sealed class WindowsAndTabsReader : IWindowTextReader
{
    private static readonly TimeSpan TabsTimeout = TimeSpan.FromMilliseconds(900);

    private readonly IWindowTextReader _screen;
    private readonly BrowserBridge _bridge;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsAndTabsReader"/> class.
    /// </summary>
    /// <param name="screen">The reading of the windows on screen.</param>
    /// <param name="bridge">The link with the extensions of the browsers.</param>
    public WindowsAndTabsReader(IWindowTextReader screen, BrowserBridge bridge)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(bridge);

        _screen = screen;
        _bridge = bridge;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WindowText>> ReadWindowsAsync(CancellationToken cancellationToken)
    {
        var tabs = _bridge.ReadTabsAsync(TabsTimeout, cancellationToken);
        var windows = await _screen.ReadWindowsAsync(cancellationToken).ConfigureAwait(false);
        return BrowserTabs.Merge(windows, await tabs.ConfigureAwait(false));
    }
}
