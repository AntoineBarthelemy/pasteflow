using System.Windows.Threading;
using PasteFlow.Core.Logins;

namespace PasteFlow.App.Logins;

/// <summary>
/// Lets the user choose an account in a <see cref="LoginPickerWindow"/>, shown on the user interface thread.
/// </summary>
internal sealed class WindowLoginPicker : ILoginPicker
{
    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowLoginPicker"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher of the user interface thread.</param>
    public WindowLoginPicker(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatcher = dispatcher;
    }

    /// <inheritdoc/>
    public Task<VaultLogin?> ChooseAsync(IReadOnlyList<VaultLogin> logins, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logins);

        return _dispatcher.InvokeAsync(
            () =>
            {
                var window = new LoginPickerWindow(logins);
                return window.ShowDialog() == true ? window.Chosen : null;
            },
            DispatcherPriority.Normal,
            cancellationToken).Task;
    }
}
