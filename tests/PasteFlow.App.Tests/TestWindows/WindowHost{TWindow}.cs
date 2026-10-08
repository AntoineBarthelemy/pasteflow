using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PasteFlow.App.Tests.TestWindows;

/// <summary>
/// Shows a WPF window on its own user interface thread, so the test thread can act on it through UI Automation
/// like another application would.
/// </summary>
/// <typeparam name="TWindow">The type of window to show.</typeparam>
internal sealed class WindowHost<TWindow> : IDisposable
    where TWindow : Window
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);

    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new();
    private TWindow? _window;
    private ExceptionDispatchInfo? _startError;
    private bool _isDisposed;
    private bool _isClosed;

    public WindowHost(Func<TWindow> createWindow)
    {
        _thread = new Thread(() => Run(createWindow)) { IsBackground = true };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        if (!_started.Wait(StartTimeout))
        {
            throw new TimeoutException("The test window did not start in time.");
        }

        _startError?.Throw();
        Handle = Invoke(window => new WindowInteropHelper(window).Handle);
    }

    public IntPtr Handle { get; }

    /// <summary>
    /// Gets a value indicating whether the window was closed, by itself or by the test.
    /// </summary>
    public bool IsClosed => _isClosed;

    public T Invoke<T>(Func<TWindow, T> action)
    {
        var window = _window ?? throw new InvalidOperationException("The test window is not started.");
        return window.Dispatcher.Invoke(() => action(window));
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        if (_window is not null)
        {
            // A window may have closed itself, as a settings window does once saved
            _window.Dispatcher.Invoke(() =>
            {
                if (!_isClosed)
                {
                    _window.Close();
                }
            });
            _window.Dispatcher.InvokeShutdown();
        }

        _ = _thread.Join(StartTimeout);
        _started.Dispose();
    }

    private void Run(Func<TWindow> createWindow)
    {
        try
        {
            _window = createWindow();

            // Ready once drawn: before that, its fields are still reported as off screen and ignored
            _window.ContentRendered += (_, _) => _started.Set();
            _window.Closed += (_, _) => _isClosed = true;
            _window.Show();
        }
        catch (Exception exception)
        {
            _startError = ExceptionDispatchInfo.Capture(exception);
            _started.Set();
            return;
        }

        Dispatcher.Run();
    }
}
