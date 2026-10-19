using System.Windows;
using PasteFlow.App.Forms;
using PasteFlow.App.Highlights;
using PasteFlow.App.Screen;
using PasteFlow.App.Tests.Fakes;
using PasteFlow.App.Tests.TestWindows;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Undo;

namespace PasteFlow.App.Tests.Highlights;

public sealed class SuggestionBubbleControllerTests : IDisposable
{
    private static readonly Rect FieldBounds = new(200, 150, 240, 24);

    private readonly WindowHost<ContactFormWindow> _host = new(() => new ContactFormWindow());
    private readonly UiAutomationFormAccess _formAccess = new();
    private readonly FillMemory _fillMemory = new();
    private readonly FakeHotkeyListener[] _listeners = { new(), new(), new() };
    private readonly SuggestionBubbleController _controller;

    public SuggestionBubbleControllerTests()
    {
        // The bubble is a window: the controller lives on the user interface thread of the test window
        _controller = _host.Invoke(_ => new SuggestionBubbleController(new SuggestionPicker(_formAccess, _fillMemory), _listeners));
    }

    [Fact]
    public async Task Show_DisplaysTheSuggestionsJustBelowTheField()
    {
        var nameField = await NameToCheckAsync("Antoine", "Barthélémy");

        Show(nameField);

        var bubble = Assert.IsType<SuggestionBubble>(_host.Invoke(_ => _controller.Bubble));
        Assert.True(_host.Invoke(_ => bubble.IsVisible));
        Assert.Equal(new Point(FieldBounds.Left, FieldBounds.Bottom + SuggestionBubble.Gap), _host.Invoke(_ => bubble.PixelPosition));
        var text = UiAutomationWindowTextReader.ReadWindow(_host.Invoke(_ => bubble.Handle), rank: 0)?.Text;
        Assert.NotNull(text);
        Assert.Contains("Antoine", text, StringComparison.Ordinal);
        Assert.Contains("Barthélémy", text, StringComparison.Ordinal);
        Assert.Contains("Ctrl+Maj+1", text, StringComparison.Ordinal);
        Assert.Contains("Ctrl+Maj+2", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Show_RegistersOneShortcutPerSuggestionOnly()
    {
        Show(await NameToCheckAsync("Antoine", "Barthélémy"));

        Assert.Equal(SuggestionBubbleController.ChoiceHotkey(0), _listeners[0].RegisteredHotkey);
        Assert.Equal(SuggestionBubbleController.ChoiceHotkey(1), _listeners[1].RegisteredHotkey);
        Assert.Null(_listeners[2].RegisteredHotkey);
    }

    [Fact]
    public void ChoiceHotkey_AvoidsAltGr()
    {
        Assert.Equal("Ctrl+Shift+1", SuggestionBubbleController.ChoiceHotkey(0).ToString());
        Assert.False(SuggestionBubbleController.ChoiceHotkey(2).Modifiers.HasFlag(KeyModifiers.Alt));
    }

    [Fact]
    public async Task Show_WithoutSuggestion_ShowsNothingAndRegistersNothing()
    {
        Show(await NameToCheckAsync());

        Assert.Null(_host.Invoke(_ => _controller.ShownFieldId));
        Assert.All(_listeners, listener => Assert.Null(listener.RegisteredHotkey));
    }

    [Fact]
    public async Task Show_TellsWhyTheFieldIsLeftToTheUser()
    {
        Show(await NameToCheckAsync("Antoine", "Barthélémy") with { Reason = "Deux valeurs aussi probables." });

        var bubble = Assert.IsType<SuggestionBubble>(_host.Invoke(_ => _controller.Bubble));
        var text = UiAutomationWindowTextReader.ReadWindow(_host.Invoke(_ => bubble.Handle), rank: 0)?.Text;
        Assert.NotNull(text);
        Assert.Contains("Pourquoi : Deux valeurs aussi probables.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Show_WithAReasonButNoSuggestion_ShowsTheReasonAndRegistersNothing()
    {
        Show(await NameToCheckAsync() with { Reason = "Aucune valeur trouvée pour ce champ." });

        var bubble = Assert.IsType<SuggestionBubble>(_host.Invoke(_ => _controller.Bubble));
        Assert.True(_host.Invoke(_ => bubble.IsVisible));
        Assert.All(_listeners, listener => Assert.Null(listener.RegisteredHotkey));
    }

    [Fact]
    public async Task Hide_FreesTheShortcutsForOtherApplications()
    {
        Show(await NameToCheckAsync("Antoine", "Barthélémy"));

        _ = _host.Invoke(_ =>
        {
            _controller.Hide();
            return 0;
        });

        Assert.All(_listeners, listener => Assert.Null(listener.RegisteredHotkey));
        Assert.False(_host.Invoke(_ => _controller.Bubble!.IsVisible));
    }

    [Fact]
    public async Task ShortcutPressed_WritesTheValueIntoTheFieldAndHidesTheBubble()
    {
        Show(await NameToCheckAsync("Antoine", "Barthélémy"));

        _ = _host.Invoke(_ =>
        {
            _listeners[1].SimulatePress();
            return 0;
        });

        await WaitUntilAsync(() => _host.Invoke(window => window.NameBox.Text) == "Barthélémy");
        Assert.Null(_host.Invoke(_ => _controller.ShownFieldId));
    }

    [Fact]
    public async Task PickAsync_ValuePickedCanBeUndoneWithTheFill()
    {
        Show(await NameToCheckAsync("Antoine"));

        var isWritten = await _host.Invoke(_ => _controller.PickAsync(0));

        Assert.True(isWritten);
        Assert.Equal("Antoine", Assert.Single(_fillMemory.TakeLastFill()).Value);
    }

    [Fact]
    public async Task PickAsync_TellsThatAValueWasPicked()
    {
        var picked = 0;
        _ = _host.Invoke(_ =>
        {
            _controller.ValuePicked += (_, _) => picked++;
            return 0;
        });
        Show(await NameToCheckAsync("Antoine"));

        _ = await _host.Invoke(_ => _controller.PickAsync(0));

        Assert.Equal(1, picked);
    }

    [Fact]
    public async Task PickAsync_WhenNothingIsShown_WritesNothing()
    {
        var isWritten = await _host.Invoke(_ => _controller.PickAsync(0));

        Assert.False(isWritten);
        Assert.Equal(string.Empty, _host.Invoke(window => window.NameBox.Text));
    }

    public void Dispose()
    {
        _ = _host.Invoke(_ =>
        {
            _controller.Dispose();
            return 0;
        });
        _host.Dispose();
        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }
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

    private void Show(FieldToCheck fieldToCheck)
    {
        _ = _host.Invoke(_ =>
        {
            _controller.Show(fieldToCheck, FieldBounds);
            return 0;
        });
    }

    private async Task<FieldToCheck> NameToCheckAsync(params string[] values)
    {
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);
        return new FieldToCheck
        {
            Field = Assert.Single(fields, field => field.Label == "Nom"),
            Suggestions = values.Select(value => new SuggestedValue { Value = value, SourceName = "Gmail" }).ToList(),
        };
    }
}
