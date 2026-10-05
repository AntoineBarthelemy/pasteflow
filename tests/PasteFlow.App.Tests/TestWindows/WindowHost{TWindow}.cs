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
            _window.Dispatcher.Invoke(_window.Close);
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
            _window.Show();
        }
        catch (Exception exception)
        {
            _startError = ExceptionDispatchInfo.Capture(exception);
            _started.Set();
            return;
        }

        _started.Set();
        Dispatcher.Run();
    }
}
