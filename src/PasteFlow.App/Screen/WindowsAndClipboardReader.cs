using System.Globalization;
using PasteFlow.Core.Screen;

namespace PasteFlow.App.Screen;

/// <summary>
/// Reads the windows, then adds what the user copied a moment ago as one more source, right behind the form: the
/// exact text of any program, even one that gives no text to Windows (decision of 15/11/2026).
/// </summary>
internal sealed class WindowsAndClipboardReader : IWindowTextReader
{
    /// <summary>
    /// The beginning of the name of the clipboard as a source, shown in the notifications and the list.
    /// </summary>
    internal const string SourceName = "Copié";

    private readonly IWindowTextReader _windows;
    private readonly Func<ClipboardCopy?> _recentCopy;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsAndClipboardReader"/> class.
    /// </summary>
    /// <param name="windows">The reading of the windows.</param>
    /// <param name="recentCopy">Gives the text copied a moment ago, or <see langword="null"/>.</param>
    public WindowsAndClipboardReader(IWindowTextReader windows, Func<ClipboardCopy?> recentCopy)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(recentCopy);

        _windows = windows;
        _recentCopy = recentCopy;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WindowText>> ReadWindowsAsync(CancellationToken cancellationToken)
    {
        var windows = await _windows.ReadWindowsAsync(cancellationToken).ConfigureAwait(false);
        if (_recentCopy() is not { } copy)
        {
            return windows;
        }

        // Right behind the form, as the window the text was copied from usually is
        var copied = new WindowText
        {
            Title = string.Create(CultureInfo.InvariantCulture, $"{SourceName} à {copy.CopiedAt.ToLocalTime():HH\\hmm}"),
            Text = copy.Text,
            Rank = 1,
        };
        return windows.Append(copied).ToList();
    }
}
