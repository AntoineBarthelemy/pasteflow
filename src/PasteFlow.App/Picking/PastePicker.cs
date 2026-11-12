using PasteFlow.App.Interop;
using PasteFlow.Core.Picking;

namespace PasteFlow.App.Picking;

/// <summary>
/// Shows the list at the cursor and gives the keyboard back to the window being written in once the choice is made.
/// </summary>
internal sealed class PastePicker : IPastePicker
{
    private static readonly TimeSpan ForegroundTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ForegroundPollDelay = TimeSpan.FromMilliseconds(20);

    // A window given the keyboard back puts it in its own box a moment later, Windows Terminal especially
    private static readonly TimeSpan FocusSettleDelay = TimeSpan.FromMilliseconds(150);

    /// <inheritdoc/>
    public async Task<string?> PickAsync(PickRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Read before the list takes the keyboard, from the window the user is writing in
        var target = NativeMethods.GetForegroundWindow();
        var caret = await Task.Run(CaretLocator.Locate, cancellationToken);

        var window = new PastePickerWindow(request);
        using var registration = cancellationToken.Register(() => window.Dispatcher.Invoke(() => window.Finish(null)));
        window.ShowAt(caret);
        var chosen = await window.Result;

        // The text is typed into the window the user was writing in, whatever was chosen
        await GiveBackKeyboardAsync(target, cancellationToken);
        return chosen;
    }

    private static async Task GiveBackKeyboardAsync(IntPtr target, CancellationToken cancellationToken)
    {
        if (target == IntPtr.Zero)
        {
            return;
        }

        _ = NativeMethods.SetForegroundWindow(target);
        var deadline = DateTime.UtcNow + ForegroundTimeout;
        while (NativeMethods.GetForegroundWindow() != target && DateTime.UtcNow < deadline)
        {
            await Task.Delay(ForegroundPollDelay, cancellationToken);
        }

        await Task.Delay(FocusSettleDelay, cancellationToken);
    }
}
