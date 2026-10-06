using System.Windows;
using PasteFlow.App.Forms;
using PasteFlow.App.Highlights;
using PasteFlow.App.Interop;
using PasteFlow.App.Tests.Fakes;
using PasteFlow.App.Tests.TestWindows;
using PasteFlow.Core.Forms;

namespace PasteFlow.App.Tests.Highlights;

public sealed class UiAutomationFieldHighlighterTests : IDisposable
{
    private const int GwlExStyle = -20;
    private const nint WsExTransparent = 0x20;
    private const nint WsExNoActivate = 0x08000000;

    private readonly WindowHost<ContactFormWindow> _host = new(() => new ContactFormWindow());
    private readonly UiAutomationFormAccess _formAccess = new();
    private readonly FakeSuggestionDisplay _suggestionDisplay = new();
    private readonly UiAutomationFieldHighlighter _highlighter;
    private IntPtr _foregroundWindow;

    public UiAutomationFieldHighlighterTests()
    {
        _foregroundWindow = _host.Handle;

        // Frames are windows: the highlighter lives on the user interface thread of the test window
        _highlighter = _host.Invoke(_ => new UiAutomationFieldHighlighter(_formAccess, _suggestionDisplay, () => _foregroundWindow));
    }

    [Fact]
    public async Task HighlightAsync_FramesEachFieldAroundIt()
    {
        var fields = await ReadFieldsAsync("Nom", "Ville");

        await _highlighter.HighlightAsync(fields);

        var states = await _formAccess.ReadStatesAsync(fields.Select(toCheck => toCheck.Field).ToList(), CancellationToken.None);
        var expected = states.Values.Select(state => Inflate(state.Bounds)).OrderBy(bounds => bounds.Y);
        Assert.Equal(expected, _highlighter.VisibleFrameBounds.OrderBy(bounds => bounds.Y));
    }

    [Fact]
    public async Task HighlightAsync_SkipsFieldsThatAlreadyHoldAValue()
    {
        var fields = await ReadFieldsAsync("Nom", "Email");

        await _highlighter.HighlightAsync(fields);

        Assert.Single(_highlighter.VisibleFrameBounds);
    }

    [Fact]
    public async Task HighlightAsync_WithoutField_RemovesPreviousFrames()
    {
        await _highlighter.HighlightAsync(await ReadFieldsAsync("Nom", "Ville"));

        await _highlighter.HighlightAsync(Array.Empty<FieldToCheck>());

        Assert.Empty(_highlighter.FrameHandles);
    }

    [Fact]
    public async Task HighlightAsync_ReplacesPreviousFrames()
    {
        await _highlighter.HighlightAsync(await ReadFieldsAsync("Nom", "Ville"));

        await _highlighter.HighlightAsync(await ReadFieldsAsync("Nom"));

        Assert.Single(_highlighter.FrameHandles);
    }

    [Fact]
    public async Task HighlightAsync_WithFieldOfForgottenReading_ShowsNothing()
    {
        var fields = await ReadFieldsAsync("Nom");
        for (var reading = 0; reading < UiAutomationFormAccess.RememberedReadingCount; reading++)
        {
            _ = await _formAccess.ReadFormAsync(IntPtr.Zero, CancellationToken.None);
        }

        await _highlighter.HighlightAsync(fields);

        Assert.Empty(_highlighter.FrameHandles);
    }

    [Fact]
    public async Task Frames_LetClicksThroughAndNeverTakeTheFocus()
    {
        await _highlighter.HighlightAsync(await ReadFieldsAsync("Nom"));

        var style = NativeMethods.GetWindowLongPtr(Assert.Single(_highlighter.FrameHandles), GwlExStyle);

        Assert.Equal(WsExTransparent, style & WsExTransparent);
        Assert.Equal(WsExNoActivate, style & WsExNoActivate);
    }

    [Fact]
    public async Task RefreshAsync_RemovesFrameOnceFieldIsFilled()
    {
        await _highlighter.HighlightAsync(await ReadFieldsAsync("Nom", "Ville"));
        _ = _host.Invoke(window => window.NameBox.Text = "Antoine");

        await _highlighter.RefreshAsync();

        Assert.Single(_highlighter.FrameHandles);
    }

    [Fact]
    public async Task RefreshAsync_FollowsFieldWhenWindowMoves()
    {
        var fields = await ReadFieldsAsync("Nom");
        await _highlighter.HighlightAsync(fields);
        var before = Assert.Single(_highlighter.VisibleFrameBounds);
        _ = _host.Invoke(window => window.Top += 40);

        await _highlighter.RefreshAsync();

        var states = await _formAccess.ReadStatesAsync(fields.Select(toCheck => toCheck.Field).ToList(), CancellationToken.None);
        var after = Assert.Single(_highlighter.VisibleFrameBounds);
        Assert.NotEqual(before, after);
        Assert.Equal(Inflate(Assert.Single(states.Values).Bounds), after);
    }

    [Fact]
    public async Task RefreshAsync_WhenAnotherWindowComesToTheForeground_RemovesAllFrames()
    {
        await _highlighter.HighlightAsync(await ReadFieldsAsync("Nom", "Ville"));
        _foregroundWindow = IntPtr.Zero;

        await _highlighter.RefreshAsync();

        Assert.Empty(_highlighter.FrameHandles);
    }

    [Fact]
    public async Task HighlightAsync_WithoutFocusedFieldToCheck_HidesSuggestions()
    {
        await _highlighter.HighlightAsync(await ReadFieldsAsync("Nom", "Ville"));

        Assert.Null(_suggestionDisplay.ShownField);
        Assert.True(_suggestionDisplay.HideCount > 0);
    }

    [Fact]
    public async Task RefreshAsync_WhenAnotherWindowComesToTheForeground_HidesSuggestions()
    {
        await _highlighter.HighlightAsync(await ReadFieldsAsync("Nom"));
        var hideCount = _suggestionDisplay.HideCount;
        _foregroundWindow = IntPtr.Zero;

        await _highlighter.RefreshAsync();

        Assert.True(_suggestionDisplay.HideCount > hideCount);
    }

    [Fact]
    public async Task ValuePicked_RemovesTheFrameOfTheFieldFilledWithoutWaiting()
    {
        await _highlighter.HighlightAsync(await ReadFieldsAsync("Nom", "Ville"));
        _ = _host.Invoke(window => window.NameBox.Text = "Antoine");

        _ = _host.Invoke(_ =>
        {
            _suggestionDisplay.SimulatePick();
            return 0;
        });

        await WaitUntilAsync(() => _highlighter.FramedFieldIds.Count == 1);
    }

    [Fact]
    public async Task Dispose_RemovesAllFrames()
    {
        await _highlighter.HighlightAsync(await ReadFieldsAsync("Nom"));

        _highlighter.Dispose();

        Assert.Empty(_highlighter.FrameHandles);
    }

    public void Dispose()
    {
        _highlighter.Dispose();
        _host.Dispose();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was not met in time.");
            await Task.Delay(20);
        }
    }

    private static Rect Inflate(Rect bounds)
    {
        bounds.Inflate(FieldFrame.FrameMargin, FieldFrame.FrameMargin);
        return bounds;
    }

    private async Task<IReadOnlyList<FieldToCheck>> ReadFieldsAsync(params string[] labels)
    {
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);
        return labels
            .Select(label => new FieldToCheck { Field = Assert.Single(fields, field => field.Label == label) })
            .ToList();
    }
}
