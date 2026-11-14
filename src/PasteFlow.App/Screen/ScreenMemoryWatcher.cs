using PasteFlow.App.Interop;
using PasteFlow.Core.Screen;

namespace PasteFlow.App.Screen;

/// <summary>
/// Feeds the memory of the screen from the events of Windows, never by reading all the time: a window is read when the
/// user leaves it for another one, and the window in front at most every 15 seconds while the user works in it, so that
/// a conversation left or a window closed was read just before.
/// </summary>
/// <remarks>Created on the thread of the application, whose messages carry the events.</remarks>
internal sealed class ScreenMemoryWatcher : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectFocus = 0x8005;
    private const uint OutOfContext = 0;
    private static readonly TimeSpan ReadEvery = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(3);

    private readonly ScreenMemory _memory;
    private readonly NativeMethods.WinEventProc _callback;
    private readonly IntPtr _foregroundHook;
    private readonly IntPtr _focusHook;
    private readonly SemaphoreSlim _reading = new(1, 1);
    private IntPtr _front;
    private DateTime _lastRead = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScreenMemoryWatcher"/> class.
    /// </summary>
    /// <param name="memory">The memory fed.</param>
    public ScreenMemoryWatcher(ScreenMemory memory)
    {
        ArgumentNullException.ThrowIfNull(memory);

        _memory = memory;
        _callback = OnEvent;
        _front = NativeMethods.GetForegroundWindow();
        _foregroundHook = NativeMethods.SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, _callback, 0, 0, OutOfContext);
        _focusHook = NativeMethods.SetWinEventHook(EventObjectFocus, EventObjectFocus, IntPtr.Zero, _callback, 0, 0, OutOfContext);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_foregroundHook != IntPtr.Zero)
        {
            _ = NativeMethods.UnhookWinEvent(_foregroundHook);
        }

        if (_focusHook != IntPtr.Zero)
        {
            _ = NativeMethods.UnhookWinEvent(_focusHook);
        }

        _reading.Dispose();
    }

    private void OnEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (!_memory.IsEnabled)
        {
            return;
        }

        if (eventType == EventSystemForeground)
        {
            // The window left is read as the user saw it last
            var left = _front;
            _front = hwnd;
            if (left != IntPtr.Zero && left != hwnd)
            {
                ReadLater(left);
            }

            return;
        }

        // Working in the window in front: read now and then, so that what it shows is kept before it changes
        if (DateTime.UtcNow - _lastRead >= ReadEvery && _front != IntPtr.Zero)
        {
            ReadLater(_front);
        }
    }

    private void ReadLater(IntPtr window)
    {
        _lastRead = DateTime.UtcNow;
        _ = Task.Run(async () =>
        {
            // One reading at a time: a busy PC is never slowed down by readings piling up
            if (!await _reading.WaitAsync(0).ConfigureAwait(false))
            {
                return;
            }

            try
            {
                _ = NativeMethods.GetWindowThreadProcessId(window, out var processId);
                if (processId == 0 || processId == (uint)Environment.ProcessId)
                {
                    return;
                }

                var reading = Task.Run(() => UiAutomationWindowTextReader.ReadWindow(window, 1));
                if (await Task.WhenAny(reading, Task.Delay(ReadTimeout)).ConfigureAwait(false) == reading
                    && await reading.ConfigureAwait(false) is { } text)
                {
                    _memory.Remember(text with { Program = WindowProgram.NameOf((int)processId) });
                }
            }
            finally
            {
                _ = _reading.Release();
            }
        });
    }
}
