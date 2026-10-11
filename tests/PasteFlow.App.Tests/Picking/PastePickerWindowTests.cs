using System.Windows;
using System.Windows.Input;
using PasteFlow.App.Picking;
using PasteFlow.App.Tests.TestWindows;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Picking;
using PasteFlow.Core.Screen;

namespace PasteFlow.App.Tests.Picking;

public sealed class PastePickerWindowTests : IDisposable
{
    private static readonly Candidate[] Suggestions =
    {
        new() { Value = "lea.martin@atelier-nova.fr", Kind = CandidateKind.Email, SourceName = "Claude", SourceRank = 1 },
        new() { Value = "06 12 48 30 77", Kind = CandidateKind.Phone, SourceName = "Claude", SourceRank = 1 },
    };

    private readonly WindowHost<PastePickerWindow> _host = new(() => new PastePickerWindow(new PickRequest
    {
        Suggestions = Suggestions,
        Finder = new PassageFinder(
            new[] { new WindowText { Title = "Claude", Rank = 1, Text = "Elle gère l'agenda d'Hugo et répond sous 48 h." } },
            Suggestions),
    })
    {
        CancelsWhenDeactivated = false,
    });

    [Fact]
    public async Task Enter_PastesTheHighlightedValue()
    {
        Press(Key.Down);
        Press(Key.Enter);

        Assert.Equal("06 12 48 30 77", await Result());
    }

    [Fact]
    public async Task Digit_BeforeTyping_PastesThatValue()
    {
        Press(Key.D1);

        Assert.Equal("lea.martin@atelier-nova.fr", await Result());
    }

    [Fact]
    public async Task Typing_SearchesAndTabWidensTheCut()
    {
        _ = _host.Invoke(window =>
        {
            window.TypeQuery("agenda d'hu");
            return true;
        });
        Assert.Equal("agenda d'Hugo", _host.Invoke(window => window.State.Selected?.Text));

        Press(Key.Tab);
        Press(Key.Enter);

        Assert.Equal("Elle gère l'agenda d'Hugo et répond sous 48 h.", await Result());
    }

    [Fact]
    public async Task Escape_CancelsTheChoice()
    {
        Press(Key.Escape);

        Assert.Null(await Result());
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private void Press(Key key)
    {
        _ = _host.Invoke(window =>
        {
            var source = PresentationSource.FromVisual(window) ?? throw new InvalidOperationException("La liste n'est pas affichée.");
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            window.RaiseEvent(args);
            return true;
        });
    }

    private Task<string?> Result()
    {
        return _host.Invoke(window => window.Result).WaitAsync(TimeSpan.FromSeconds(5));
    }
}
