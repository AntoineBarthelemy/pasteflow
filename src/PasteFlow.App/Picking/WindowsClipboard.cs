using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using PasteFlow.Core.Picking;

namespace PasteFlow.App.Picking;

/// <summary>
/// Puts the values pasted at the cursor in the clipboard of Windows, as a copy by the user would: Ctrl+V pastes them
/// again, and the history (Win+V) keeps them.
/// </summary>
/// <remarks>
/// A sensitive value, such as an IBAN, goes in the clipboard with the formats that Windows reads to keep it out of the
/// history and of the synchronization between devices.
/// </remarks>
internal sealed class WindowsClipboard : IClipboardWriter
{
    private const int Attempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(40);

    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsClipboard"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher of the user interface thread, the only one allowed to use the clipboard.</param>
    public WindowsClipboard(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <inheritdoc/>
    public async Task<bool> CopyAsync(string text, bool isSensitive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return false;
        }

        // Another program may hold the clipboard for a moment: a few tries, then the value stays pasted only
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            if (await _dispatcher.InvokeAsync(() => TrySet(text, isSensitive), DispatcherPriority.Normal, cancellationToken))
            {
                return true;
            }

            await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(true);
        }

        return false;
    }

    /// <summary>
    /// Builds what goes in the clipboard: the text, and for a sensitive value the formats that keep it out of the
    /// history.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="isSensitive">Whether the text is sensitive.</param>
    /// <returns>The data for the clipboard.</returns>
    internal static DataObject DataFor(string text, bool isSensitive)
    {
        var data = new DataObject();
        data.SetText(text, TextDataFormat.UnicodeText);
        if (isSensitive)
        {
            data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[] { 0 }));
            data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)));
            data.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)));
        }

        return data;
    }

    private static bool TrySet(string text, bool isSensitive)
    {
        try
        {
            Clipboard.SetDataObject(DataFor(text, isSensitive), copy: true);
            return true;
        }
        catch (ExternalException)
        {
            // Another program holds the clipboard
            return false;
        }
    }
}
