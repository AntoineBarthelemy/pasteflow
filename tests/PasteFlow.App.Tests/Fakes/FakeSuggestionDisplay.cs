using System.Windows;
using PasteFlow.App.Highlights;
using PasteFlow.Core.Forms;

namespace PasteFlow.App.Tests.Fakes;

/// <summary>
/// Suggestion display that records what it is asked to show.
/// </summary>
internal sealed class FakeSuggestionDisplay : ISuggestionDisplay
{
    public event EventHandler? ValuePicked;

    public FieldToCheck? ShownField { get; private set; }

    public int HideCount { get; private set; }

    public void Show(FieldToCheck fieldToCheck, Rect fieldBounds)
    {
        ShownField = fieldToCheck;
    }

    public void Hide()
    {
        ShownField = null;
        HideCount++;
    }

    public void SimulatePick()
    {
        ValuePicked?.Invoke(this, EventArgs.Empty);
    }
}
