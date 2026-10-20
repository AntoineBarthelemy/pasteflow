using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using PasteFlow.App.Design;
using PasteFlow.App.Diagnostics;

namespace PasteFlow.App.Notifications;

/// <summary>
/// The popup that tells an unexpected error, with a button that opens the journal of the errors and one that copies its
/// last errors, ready to be pasted in a message.
/// </summary>
internal static class ErrorNotice
{
    /// <summary>
    /// Gets how long the popup stays when the mouse is not on it: long enough to reach a button.
    /// </summary>
    public static TimeSpan DisplayTime { get; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Builds the content of the popup.
    /// </summary>
    /// <returns>The content, to show with <see cref="PopupNotifier.ShowContent"/>.</returns>
    public static UIElement Create()
    {
        var grid = new Grid { MaxWidth = 380 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var mark = Ui.Icon(Icons.Warning, 14, Theme.Brush(palette => palette.Danger));
        mark.VerticalAlignment = VerticalAlignment.Top;
        mark.Margin = new Thickness(0, 2, 12, 0);
        grid.Children.Add(mark);

        var texts = new StackPanel();
        var message = Ui.Text("Erreur inattendue, PasteFlow continue. Le détail est gardé dans le journal des erreurs.", TextRole.Body);
        texts.Children.Add(message);

        // Quiet buttons, aligned with the text: their own margin is taken back on the left
        var open = Ui.QuietButton("Ouvrir le journal");
        open.Foreground = Theme.Brush(palette => palette.Accent);
        open.Click += (_, _) =>
        {
            if (!ErrorLog.Open())
            {
                message.Text = $"Impossible d'ouvrir le journal. Il est ici : {ErrorLog.DefaultPath}";
            }
        };
        var copy = Ui.QuietButton("Copier les dernières erreurs");
        copy.Click += (_, _) => message.Text = Copy()
            ? "Copié : tu peux le coller dans un message."
            : "Impossible de copier pour l'instant, réessaie.";
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-10, 6, 0, -6) };
        buttons.Children.Add(open);
        buttons.Children.Add(copy);
        texts.Children.Add(buttons);

        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);
        return grid;
    }

    private static bool Copy()
    {
        var recent = ErrorLog.ReadRecent();
        if (recent.Length == 0)
        {
            return false;
        }

        try
        {
            Clipboard.SetText(recent);
            return true;
        }
        catch (ExternalException)
        {
            // Another program holds the clipboard
            return false;
        }
    }
}
