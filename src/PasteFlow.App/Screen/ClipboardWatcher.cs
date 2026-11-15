using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PasteFlow.App.Interop;

namespace PasteFlow.App.Screen;

/// <summary>
/// Keeps in memory the last text the user copied (Ctrl+C) in any program, with the time of the copy: copying is the
/// one gesture every program knows, and it gives the exact text, even where the screen cannot be read (decision of
/// 15/11/2026). Nothing is written on the disk.
/// </summary>
/// <remarks>
/// A copy marked sensitive (a password manager, an IBAN pasted by PasteFlow) and the copies PasteFlow makes itself are
/// never kept.
/// </remarks>
internal sealed class ClipboardWatcher : IDisposable
{
    /// <summary>
    /// The format PasteFlow adds to its own copies, so that a value it pasted does not come back as a copy of the user.
    /// </summary>
    internal const string OwnCopyFormat = "PasteFlowCopy";

    /// <summary>
    /// How long a copy stays a source: a text copied an hour ago and forgotten must not fill a form.
    /// </summary>
    internal static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

    private const int WmClipboardUpdate = 0x031D;
    private const int MaxLength = 50_000;
    private static readonly IntPtr MessageOnlyWindowParent = new(-3);

    // The formats Windows and password managers use to keep a copy out of the history and of other programs
    private static readonly string[] SensitiveFormats =
    {
        "ExcludeClipboardContentFromMonitorProcessing",
        "CanIncludeInClipboardHistory",
        "Clipboard Viewer Ignore",
    };

    private readonly HwndSource _messageWindow;
    private readonly bool _isListening;
    private ClipboardCopy? _last;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipboardWatcher"/> class, on the user interface thread.
    /// </summary>
    public ClipboardWatcher()
    {
        _messageWindow = new HwndSource(new HwndSourceParameters("PasteFlow.ClipboardWatcher") { ParentWindow = MessageOnlyWindowParent, WindowStyle = 0 });
        _messageWindow.AddHook(WndProc);
        _isListening = NativeMethods.AddClipboardFormatListener(_messageWindow.Handle);
    }

    /// <summary>
    /// Gets the text copied by the user less than <see cref="MaxAge"/> ago, or <see langword="null"/>.
    /// </summary>
    /// <returns>The copy, or <see langword="null"/>.</returns>
    public ClipboardCopy? Recent()
    {
        return RecentAt(Volatile.Read(ref _last), DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_isListening)
        {
            _ = NativeMethods.RemoveClipboardFormatListener(_messageWindow.Handle);
        }

        _messageWindow.RemoveHook(WndProc);
        _messageWindow.Dispose();
        Volatile.Write(ref _last, null);
        _isDisposed = true;
    }

    /// <summary>
    /// Gives a copy if it is recent enough to be a source.
    /// </summary>
    /// <param name="copy">The last copy kept.</param>
    /// <param name="now">The time now, in UTC.</param>
    /// <returns>The copy, or <see langword="null"/> when there is none or it is too old.</returns>
    internal static ClipboardCopy? RecentAt(ClipboardCopy? copy, DateTime now)
    {
        return copy is not null && now - copy.CopiedAt <= MaxAge ? copy : null;
    }

    /// <summary>
    /// Determines whether a copy may be kept as a source, from the formats it holds.
    /// </summary>
    /// <param name="formats">The formats of the clipboard.</param>
    /// <returns><see langword="true"/> for a text copied by the user; otherwise <see langword="false"/>.</returns>
    internal static bool IsKept(IReadOnlyCollection<string> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);

        return formats.Contains(DataFormats.UnicodeText)
            && !formats.Contains(OwnCopyFormat)
            && !formats.Any(format => SensitiveFormats.Contains(format, StringComparer.OrdinalIgnoreCase));
    }

    private static ClipboardCopy? Capture()
    {
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null || !IsKept(data.GetFormats()))
            {
                return null;
            }

            var text = data.GetData(DataFormats.UnicodeText) as string;
            return string.IsNullOrWhiteSpace(text) || text.Length > MaxLength
                ? null
                : new ClipboardCopy { Text = text, CopiedAt = DateTime.UtcNow };
        }
        catch (Exception exception) when (exception is ExternalException or OutOfMemoryException)
        {
            // Another program holds the clipboard: this copy is not kept
            return null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmClipboardUpdate)
        {
            // A new copy replaces the previous one, even when it is not kept: the old text is no longer what the user copied
            Volatile.Write(ref _last, Capture());
        }

        return IntPtr.Zero;
    }
}
