using System.Windows;
using PasteFlow.App.Hints;
using PasteFlow.App.Tests.TestWindows;
using PasteFlow.Core.Hints;

namespace PasteFlow.App.Tests.Hints;

public sealed class HintOverlayWindowTests : IDisposable
{
    private static readonly Rect Screen = new(0, 0, 1920, 1080);

    private readonly IReadOnlyList<HintTarget> _targets = Enumerable.Range(0, 9)
        .Select(index => Target($"valeur {index}", HintCodes.Create(9)[index], top: 40 * index))
        .ToList();

    private readonly WindowHost<HintOverlayWindow> _host;

    public HintOverlayWindowTests()
    {
        _host = new WindowHost<HintOverlayWindow>(() => new HintOverlayWindow(_targets, Screen));
    }

    [Fact]
    public void PlaceOf_TurnsPixelsIntoUnitsOfTheLayer()
    {
        var target = Target("antoine.b@orange.fr", "F", top: 300) with { Place = new ScreenText { Text = "x", Left = 450, Top = 300 } };

        var place = HintOverlayWindow.PlaceOf(target, new Rect(-1920, 0, 3840, 1080), new DpiScale(1.5, 1.5), shift: 4);

        Assert.Equal(new Point(((450 + 1920) / 1.5) + 4, 200), place);
    }

    [Fact]
    public void Type_ShowsOnlyTheCodesStartingWithTheLettersTyped()
    {
        var shown = _host.Invoke(overlay =>
        {
            overlay.Type('j');
            return overlay.ShownCodes;
        });

        Assert.Equal(new[] { "JF" }, shown);
    }

    [Fact]
    public async Task Type_AWholeCodeChoosesItsValue()
    {
        var choice = _host.Invoke(overlay =>
        {
            overlay.Type('f');
            overlay.Type('J');
            return overlay.Choice;
        });

        Assert.Equal("valeur 1", (await choice)?.Value);
    }

    [Fact]
    public void Type_IgnoresALetterThatStartsNoCode()
    {
        Assert.Empty(_host.Invoke(overlay =>
        {
            overlay.Type('z');
            return overlay.Typed;
        }));
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private static HintTarget Target(string value, string code, double top)
    {
        return new HintTarget { Value = value, Code = code, Place = new ScreenText { Text = value, Left = 10, Top = top, Width = 100, Height = 20 } };
    }
}
