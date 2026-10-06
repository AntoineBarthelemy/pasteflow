using System.Windows;
using System.Windows.Threading;
using PasteFlow.App.Forms;
using PasteFlow.App.Interop;
using PasteFlow.Core.Forms;

namespace PasteFlow.App.Highlights;

/// <summary>
/// Draws a <see cref="FieldFrame"/> around each field to check, and keeps the frames up to date.
/// </summary>
/// <remarks>
/// <para>The frames follow their fields when the window moves or scrolls. A frame disappears as soon as its field
/// holds a value, and all of them disappear when another window comes to the foreground or after
/// <see cref="MaxDuration"/>.</para>
/// <para>Must be created on the user interface thread; its methods can be called from any thread.</para>
/// </remarks>
internal sealed class UiAutomationFieldHighlighter : IFieldHighlighter, IDisposable
{
    /// <summary>
    /// The time between two updates of the frames.
    /// </summary>
    internal static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// The longest time frames stay on screen.
    /// </summary>
    internal static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(1);

    private readonly UiAutomationFormAccess _formAccess;
    private readonly Func<IntPtr> _foregroundWindow;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _refreshTimer;
    private readonly Dictionary<string, (FormField Field, FieldFrame Frame)> _frames = new(StringComparer.Ordinal);
    private IntPtr _formWindow;
    private long _shownAt;
    private int _generation;
    private bool _isRefreshing;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="UiAutomationFieldHighlighter"/> class.
    /// </summary>
    /// <param name="formAccess">The access to the fields read.</param>
    public UiAutomationFieldHighlighter(UiAutomationFormAccess formAccess)
        : this(formAccess, NativeMethods.GetForegroundWindow)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UiAutomationFieldHighlighter"/> class.
    /// </summary>
    /// <param name="formAccess">The access to the fields read.</param>
    /// <param name="foregroundWindow">Gives the window currently in the foreground.</param>
    internal UiAutomationFieldHighlighter(UiAutomationFormAccess formAccess, Func<IntPtr> foregroundWindow)
    {
        ArgumentNullException.ThrowIfNull(formAccess);
        ArgumentNullException.ThrowIfNull(foregroundWindow);

        _formAccess = formAccess;
        _foregroundWindow = foregroundWindow;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = RefreshInterval };
        _refreshTimer.Tick += OnRefreshTimerTick;
    }

    /// <summary>
    /// Gets the positions of the frames currently visible, in screen pixels.
    /// </summary>
    internal IReadOnlyList<Rect> VisibleFrameBounds => _dispatcher.Invoke(
        () => _frames.Values.Where(entry => entry.Frame.IsVisible).Select(entry => entry.Frame.PixelBounds).ToList());

    /// <summary>
    /// Gets the window handles of the frames, visible or not.
    /// </summary>
    internal IReadOnlyList<IntPtr> FrameHandles => _dispatcher.Invoke(
        () => _frames.Values.Select(entry => entry.Frame.Handle).ToList());

    /// <inheritdoc/>
    public void Highlight(IReadOnlyList<FormField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        _ = HighlightAsync(fields);
    }

    /// <summary>
    /// Stops updating the frames and removes them.
    /// </summary>
    public void Dispose()
    {
        if (_dispatcher.HasShutdownStarted)
        {
            _isDisposed = true;
            return;
        }

        _dispatcher.Invoke(() =>
        {
            RemoveFrames();
            _isDisposed = true;
        });
    }

    /// <summary>
    /// Highlights fields, replacing the previous frames.
    /// </summary>
    /// <param name="fields">The fields to highlight.</param>
    /// <returns>A task completed when the frames are shown.</returns>
    internal Task HighlightAsync(IReadOnlyList<FormField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        return _dispatcher.InvokeAsync(() => ShowFramesAsync(fields)).Task.Unwrap();
    }

    /// <summary>
    /// Updates the frames once, as the timer does regularly.
    /// </summary>
    /// <returns>A task completed when the frames are updated.</returns>
    internal Task RefreshAsync()
    {
        return _dispatcher.InvokeAsync(RefreshFramesAsync).Task.Unwrap();
    }

    private async Task ShowFramesAsync(IReadOnlyList<FormField> fields)
    {
        RemoveFrames();
        if (_isDisposed || fields.Count == 0)
        {
            return;
        }

        var generation = _generation;
        var states = await _formAccess.ReadStatesAsync(fields, CancellationToken.None);
        if (_isDisposed || generation != _generation)
        {
            // Another fill came in the meantime
            return;
        }

        foreach (var field in fields)
        {
            if (states.TryGetValue(field.Id, out var state) && !state.HasValue)
            {
                var frame = new FieldFrame();
                _frames[field.Id] = (field, frame);
                frame.Surround(state.Bounds);
            }
        }

        if (_frames.Count > 0)
        {
            _formWindow = _formAccess.GetWindow(_frames.Values.First().Field);
            _shownAt = Environment.TickCount64;
            _refreshTimer.Start();
        }
    }

    private async Task RefreshFramesAsync()
    {
        if (_isRefreshing || _frames.Count == 0)
        {
            return;
        }

        var isTooOld = Environment.TickCount64 - _shownAt > MaxDuration.TotalMilliseconds;
        if (isTooOld || _foregroundWindow() != _formWindow)
        {
            // The user went to another window: the frames would float above it
            RemoveFrames();
            return;
        }

        _isRefreshing = true;
        try
        {
            var generation = _generation;
            var fields = _frames.Values.Select(entry => entry.Field).ToList();
            var states = await _formAccess.ReadStatesAsync(fields, CancellationToken.None);
            if (generation != _generation)
            {
                return;
            }

            foreach (var (id, (_, frame)) in _frames.ToList())
            {
                if (!states.TryGetValue(id, out var state) || state.HasValue)
                {
                    // Filled by the user, or gone with its page
                    frame.Close();
                    _ = _frames.Remove(id);
                }
                else
                {
                    frame.Surround(state.Bounds);
                }
            }

            if (_frames.Count == 0)
            {
                RemoveFrames();
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void RemoveFrames()
    {
        _generation++;
        _refreshTimer.Stop();
        foreach (var (_, frame) in _frames.Values)
        {
            frame.Close();
        }

        _frames.Clear();
        _formWindow = IntPtr.Zero;
    }

    private async void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        await RefreshFramesAsync();
    }
}
