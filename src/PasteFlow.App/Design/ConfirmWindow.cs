using System.Windows;
using System.Windows.Controls;

namespace PasteFlow.App.Design;

/// <summary>
/// Small window that asks the user to confirm an action, in the style of PasteFlow rather than a gray box of Windows.
/// </summary>
/// <remarks>
/// The keyboard starts on "Annuler": an Entrée or an Échap typed by mistake changes nothing.
/// </remarks>
internal sealed class ConfirmWindow : ThemedWindow
{
    private readonly Button _refuse;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmWindow"/> class.
    /// </summary>
    /// <param name="glyph">The icon at the top.</param>
    /// <param name="question">The question, as a title.</param>
    /// <param name="details">What the action does, in a sentence or two.</param>
    /// <param name="confirm">The words of the button that confirms, such as "Effacer".</param>
    /// <param name="refuse">The words of the button that refuses.</param>
    /// <param name="isDestructive">Whether the action erases something, which makes its button red.</param>
    internal ConfirmWindow(string glyph, string question, string details, string confirm, string refuse, bool isDestructive)
    {
        Title = "PasteFlow";
        Topmost = true;

        var yes = Ui.ActionButton(confirm);
        yes.SetResourceReference(StyleProperty, isDestructive ? "Pf.DangerButton" : "Pf.PrimaryButton");
        yes.Click += (_, _) => DialogResult = true;
        _refuse = Ui.ActionButton(refuse, isCancel: true);

        var panel = new StackPanel { Margin = new Thickness(28, 24, 28, 22), Width = 400 };
        panel.Children.Add(Ui.Header(glyph, question, details));
        panel.Children.Add(Ui.ButtonRow(_refuse, yes));
        Content = panel;
        Loaded += (_, _) =>
        {
            _ = Activate();
            _ = _refuse.Focus();
        };
    }

    /// <summary>
    /// Asks the question and waits for the answer.
    /// </summary>
    /// <param name="glyph">The icon at the top.</param>
    /// <param name="question">The question, as a title.</param>
    /// <param name="details">What the action does.</param>
    /// <param name="confirm">The words of the button that confirms.</param>
    /// <param name="refuse">The words of the button that refuses.</param>
    /// <param name="isDestructive">Whether the action erases something.</param>
    /// <returns><see langword="true"/> if the user confirmed; otherwise <see langword="false"/>.</returns>
    public static bool Ask(string glyph, string question, string details, string confirm, string refuse = "Annuler", bool isDestructive = false)
    {
        return new ConfirmWindow(glyph, question, details, confirm, refuse, isDestructive).ShowDialog() == true;
    }
}
