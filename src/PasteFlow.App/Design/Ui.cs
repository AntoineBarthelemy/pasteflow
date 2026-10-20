using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PasteFlow.App.Design;

/// <summary>
/// The building blocks of the interface of PasteFlow, so that every window and popup speaks the same language: texts,
/// cards, keys, icons, fields and rows of buttons.
/// </summary>
internal static class Ui
{
    /// <summary>
    /// The room left around a popup for its shadow.
    /// </summary>
    public const double ShadowMargin = 18;

    /// <summary>
    /// Builds a text of a given kind.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="role">Its kind, which sets its font, size and color.</param>
    /// <returns>The text, wrapping on several lines.</returns>
    public static TextBlock Text(string text, TextRole role)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        switch (role)
        {
            case TextRole.Title:
                block.FontFamily = Theme.SerifFont;
                block.FontSize = 22;
                block.Foreground = Theme.Brush(palette => palette.Text);
                break;
            case TextRole.Heading:
                block.FontFamily = Theme.DisplayFont;
                block.FontSize = 15;
                block.FontWeight = FontWeights.SemiBold;
                block.Foreground = Theme.Brush(palette => palette.Text);
                break;
            case TextRole.Body:
                block.FontSize = 13;
                block.Foreground = Theme.Brush(palette => palette.Text);
                break;
            case TextRole.Secondary:
                block.FontSize = 13;
                block.Foreground = Theme.Brush(palette => palette.TextSecondary);
                break;
            case TextRole.Caption:
                block.FontSize = 12;
                block.Foreground = Theme.Brush(palette => palette.TextSecondary);
                break;
            default:
                block.FontSize = 11;
                block.Foreground = Theme.Brush(palette => palette.TextTertiary);
                break;
        }

        if (role is not TextRole.Title and not TextRole.Heading)
        {
            block.FontFamily = Theme.UiFont;
        }

        return block;
    }

    /// <summary>
    /// Builds a text on one line, cut with "…" when too long.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="role">Its kind.</param>
    /// <returns>The text.</returns>
    public static TextBlock Line(string text, TextRole role)
    {
        var block = Text(text, role);
        block.TextWrapping = TextWrapping.NoWrap;
        block.TextTrimming = TextTrimming.CharacterEllipsis;
        return block;
    }

    /// <summary>
    /// Builds an icon of the icon font of Windows.
    /// </summary>
    /// <param name="glyph">The glyph, see <see cref="Icons"/>.</param>
    /// <param name="size">The size.</param>
    /// <param name="color">The color, or <see langword="null"/> for the secondary text color.</param>
    /// <returns>The icon.</returns>
    public static TextBlock Icon(string glyph, double size, Brush? color = null)
    {
        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = Theme.IconFont,
            FontSize = size,
            Foreground = color ?? Theme.Brush(palette => palette.TextSecondary),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        AutomationProperties.SetName(icon, string.Empty);
        return icon;
    }

    /// <summary>
    /// Builds an icon in a small rounded square washed with the accent, as at the top of a window.
    /// </summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The badge.</returns>
    public static Border IconBadge(string glyph)
    {
        return new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(10),
            Background = Theme.Brush(palette => palette.AccentSoft),
            Child = Icon(glyph, 16, Theme.Brush(palette => palette.Accent)),
            VerticalAlignment = VerticalAlignment.Top,
        };
    }

    /// <summary>
    /// Builds a key of the keyboard, such as "Ctrl" or "Entrée", drawn as a small cap.
    /// </summary>
    /// <param name="key">The name of the key.</param>
    /// <param name="isAccent">Whether the key is highlighted with the accent.</param>
    /// <returns>The key.</returns>
    public static Border Keycap(string key, bool isAccent = false)
    {
        return new Border
        {
            Background = isAccent ? Theme.Brush(palette => palette.Accent) : Theme.Brush(palette => palette.Sunken),
            BorderBrush = isAccent ? Theme.Brush(palette => palette.Accent) : Theme.Brush(palette => palette.BorderStrong),
            BorderThickness = new Thickness(1, 1, 1, 2),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(6, 0, 6, 1),
            MinWidth = 20,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = key,
                FontFamily = Theme.MonoFont,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = isAccent ? Theme.Brush(palette => palette.OnAccent) : Theme.Brush(palette => palette.TextSecondary),
            },
        };
    }

    /// <summary>
    /// Builds a row of keys followed by what they do, such as "Entrée coller".
    /// </summary>
    /// <param name="hints">Pairs of a key and its action.</param>
    /// <returns>The row, wrapping when narrow.</returns>
    public static WrapPanel KeyHints(params (string Key, string Action)[] hints)
    {
        var row = new WrapPanel();
        foreach (var (key, action) in hints)
        {
            var hint = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 14, 2) };
            hint.Children.Add(Keycap(key));
            var text = Text(action, TextRole.Hint);
            text.Margin = new Thickness(6, 0, 0, 0);
            text.VerticalAlignment = VerticalAlignment.Center;
            hint.Children.Add(text);
            row.Children.Add(hint);
        }

        return row;
    }

    /// <summary>
    /// Builds a white card with a fine border and rounded corners.
    /// </summary>
    /// <param name="child">The content of the card.</param>
    /// <param name="padding">The room inside the card.</param>
    /// <returns>The card.</returns>
    public static Border Card(UIElement child, Thickness padding)
    {
        return new Border
        {
            Background = Theme.Brush(palette => palette.Surface),
            BorderBrush = Theme.Brush(palette => palette.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(Theme.CardRadius),
            Padding = padding,
            Child = child,
        };
    }

    /// <summary>
    /// Builds the root of a popup: a card with a soft shadow, with room around it so that the shadow is not cut.
    /// </summary>
    /// <param name="child">The content of the popup.</param>
    /// <param name="padding">The room inside the card.</param>
    /// <returns>The root, to put as the content of a transparent window.</returns>
    public static Grid PopupRoot(UIElement child, Thickness padding)
    {
        var card = Card(child, padding);
        card.Effect = Theme.CardShadow();
        var root = new Grid { Margin = new Thickness(ShadowMargin), Children = { card } };
        Theme.Apply(root);
        TextElement.SetFontFamily(root, Theme.UiFont);
        return root;
    }

    /// <summary>
    /// Builds the top of a window: an icon, a title in serif and a line that says what the window is for.
    /// </summary>
    /// <param name="glyph">The icon.</param>
    /// <param name="title">The title.</param>
    /// <param name="subtitle">What the window is for, or an empty string.</param>
    /// <returns>The top of the window.</returns>
    public static Grid Header(string glyph, string title, string subtitle)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var badge = IconBadge(glyph);
        badge.Margin = new Thickness(0, 2, 14, 0);
        grid.Children.Add(badge);
        var texts = new StackPanel();
        texts.Children.Add(Text(title, TextRole.Title));
        if (subtitle.Length > 0)
        {
            var line = Text(subtitle, TextRole.Secondary);
            line.Margin = new Thickness(0, 4, 0, 0);
            texts.Children.Add(line);
        }

        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);
        return grid;
    }

    /// <summary>
    /// Builds a field with its label above it; Alt and the underscored letter of the label put the keyboard in it.
    /// </summary>
    /// <param name="label">The label, with "_" before the access letter.</param>
    /// <param name="control">The field.</param>
    /// <returns>The field and its label.</returns>
    public static StackPanel Field(string label, Control control)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(new Label { Content = label, Target = control });
        panel.Children.Add(control);
        return panel;
    }

    /// <summary>
    /// Builds a button of the theme.
    /// </summary>
    /// <param name="text">The text, with "_" before the access letter.</param>
    /// <param name="isDefault">Whether it is the main action of the window, filled with the accent and run by Entrée.</param>
    /// <param name="isCancel">Whether Échap runs it.</param>
    /// <returns>The button.</returns>
    public static Button ActionButton(string text, bool isDefault = false, bool isCancel = false)
    {
        var button = new Button { Content = text, IsDefault = isDefault, IsCancel = isCancel, MinWidth = 96 };
        AutomationProperties.SetName(button, text.Replace("_", string.Empty, StringComparison.Ordinal));
        return button;
    }

    /// <summary>
    /// Builds a quiet button without frame, for a secondary action.
    /// </summary>
    /// <param name="text">The text, with "_" before the access letter.</param>
    /// <returns>The button.</returns>
    public static Button QuietButton(string text)
    {
        var button = ActionButton(text);
        button.SetResourceReference(FrameworkElement.StyleProperty, "Pf.SubtleButton");
        button.MinWidth = 0;
        return button;
    }

    /// <summary>
    /// Builds a row of buttons aligned to the right, the main one last, as on a Mac.
    /// </summary>
    /// <param name="buttons">The buttons, in order.</param>
    /// <returns>The row.</returns>
    public static StackPanel ButtonRow(params Button[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        foreach (var button in buttons)
        {
            button.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(button);
        }

        return row;
    }

    /// <summary>
    /// Builds a fine horizontal line between two parts.
    /// </summary>
    /// <param name="margin">The room around the line.</param>
    /// <returns>The line.</returns>
    public static Border Divider(Thickness margin)
    {
        return new Border { Height = 1, Background = Theme.Brush(palette => palette.Border), Margin = margin };
    }
}
