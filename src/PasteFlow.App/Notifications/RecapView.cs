using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PasteFlow.App.Design;
using PasteFlow.Core.Forms;

namespace PasteFlow.App.Notifications;

/// <summary>
/// Builds the content of the popup shown after a fill: one line per field with a colored dot, the value and its
/// source, then how to undo.
/// </summary>
internal static class RecapView
{
    private const double MaxWidth = 460;
    private const double DotSize = 8;

    // Display time grows with the number of lines, to leave time to read them
    private const int MinDisplayMilliseconds = 4000;
    private const int MaxDisplayMilliseconds = 12000;
    private const int DisplayMillisecondsPerLine = 700;

    /// <summary>
    /// Gets how long a recap stays open when the mouse is not over it.
    /// </summary>
    /// <param name="recap">The recap to display.</param>
    /// <returns>The display time.</returns>
    public static TimeSpan DisplayTimeFor(FillRecap recap)
    {
        ArgumentNullException.ThrowIfNull(recap);

        return TimeSpan.FromMilliseconds(Math.Clamp(
            MinDisplayMilliseconds + (DisplayMillisecondsPerLine * (recap.Lines.Count + (2 * recap.Lines.Count(line => line.Reason.Length > 0)))),
            MinDisplayMilliseconds,
            MaxDisplayMilliseconds));
    }

    /// <summary>
    /// Builds the content showing a recap.
    /// </summary>
    /// <param name="recap">The recap to display.</param>
    /// <returns>The content, to put in a <see cref="NotificationPopup"/>.</returns>
    public static UIElement Create(FillRecap recap)
    {
        ArgumentNullException.ThrowIfNull(recap);

        var panel = new StackPanel { MaxWidth = MaxWidth };
        panel.Children.Add(CreateHeader(recap));

        if (recap.Lines.Count > 0)
        {
            panel.Children.Add(CreateLines(recap.Lines));
        }

        if (recap.HiddenLineCount > 0)
        {
            var others = recap.HiddenLineCount == 1 ? "+ 1 autre champ" : $"+ {recap.HiddenLineCount.ToString(CultureInfo.InvariantCulture)} autres champs";
            panel.Children.Add(Text(others, PopupColors.SecondaryText, 12, new Thickness(18, 2, 0, 0)));
        }

        panel.Children.Add(Ui.Divider(new Thickness(0, 12, 0, 10)));

        // How to undo, with the key drawn as a key: "Ctrl+Alt+Z  tout annuler"
        if (recap.UndoHint is { } undo)
        {
            var parts = undo.Split(" pour ", 2, StringSplitOptions.TrimEntries);
            var hints = Ui.KeyHints((parts[0], parts.Length > 1 ? parts[1] : string.Empty));
            hints.Margin = new Thickness(0, 0, 0, 6);
            panel.Children.Add(hints);
        }

        foreach (var detail in recap.Details)
        {
            var line = Text(detail, PopupColors.DiscreetText, 11, new Thickness(0, 1, 0, 1));
            line.TextWrapping = TextWrapping.Wrap;
            line.TextTrimming = TextTrimming.None;
            panel.Children.Add(line);
        }

        return panel;
    }

    private static DockPanel CreateHeader(FillRecap recap)
    {
        var header = new DockPanel { LastChildFill = true };
        var duration = Text(recap.Duration, PopupColors.DiscreetText, 11, new Thickness(12, 2, 0, 0));
        DockPanel.SetDock(duration, Dock.Right);
        header.Children.Add(duration);

        // A check when everything is filled, a warning when something is left to the user
        var isComplete = recap.Lines.All(line => line.Kind == RecapLineKind.Filled);
        var badge = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = isComplete ? PopupColors.Filled : PopupColors.ToCheck,
            Margin = new Thickness(0, 0, 10, 0),
            Child = Ui.Icon(isComplete ? Icons.Check : Icons.Warning, 11, Theme.Brush(palette => palette.OnAccent)),
        };
        DockPanel.SetDock(badge, Dock.Left);
        header.Children.Add(badge);

        var title = Text(recap.Title, PopupColors.Text, 14, default);
        title.FontWeight = FontWeights.SemiBold;
        title.FontFamily = Theme.DisplayFont;
        header.Children.Add(title);
        return header;
    }

    private static Grid CreateLines(IReadOnlyList<RecapLine> lines)
    {
        var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MaxWidth = 150 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var row = 0;
        foreach (var line in lines)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var color = ColorOf(line.Kind);
            var margin = new Thickness(0, 2, 10, 2);

            var dot = new Ellipse { Width = DotSize, Height = DotSize, Fill = color, Margin = margin, VerticalAlignment = VerticalAlignment.Center };
            AddToGrid(grid, dot, row, 0);
            AddToGrid(grid, Text(line.Label, PopupColors.SecondaryText, 12, margin), row, 1);

            var value = Text(line.Value, line.Kind == RecapLineKind.Filled ? PopupColors.Text : color, 13, margin);
            if (line.Kind != RecapLineKind.Filled)
            {
                value.FontStyle = FontStyles.Italic;
            }

            AddToGrid(grid, value, row, 2);
            AddToGrid(grid, Text(line.SourceName, PopupColors.DiscreetText, 11, new Thickness(0, 3, 0, 2)), row, 3);
            row++;

            if (line.Reason.Length > 0)
            {
                // Why the field is left to the user, under its line, in full
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var reason = Text(line.Reason, PopupColors.SecondaryText, 11, new Thickness(0, 0, 0, 4));
                reason.TextWrapping = TextWrapping.Wrap;
                reason.TextTrimming = TextTrimming.None;
                AddToGrid(grid, reason, row, 1);
                Grid.SetColumnSpan(reason, 3);
                row++;
            }
        }

        return grid;
    }

    private static Brush ColorOf(RecapLineKind kind)
    {
        return kind switch
        {
            RecapLineKind.Filled => PopupColors.Filled,
            RecapLineKind.Failed => PopupColors.Failed,
            _ => PopupColors.ToCheck,
        };
    }

    private static void AddToGrid(Grid grid, UIElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private static TextBlock Text(string text, Brush color, double size, Thickness margin)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = color,
            FontFamily = Theme.UiFont,
            FontSize = size,
            Margin = margin,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }
}
