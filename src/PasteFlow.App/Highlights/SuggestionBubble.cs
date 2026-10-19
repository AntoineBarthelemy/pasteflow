using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PasteFlow.App.Notifications;
using PasteFlow.Core.Forms;

namespace PasteFlow.App.Highlights;

/// <summary>
/// Small window shown below a field to check, proposing the values that could fit it.
/// </summary>
/// <remarks>
/// It never takes the focus, so the user can keep typing in the field; a value is picked with its shortcut or a click.
/// </remarks>
internal sealed class SuggestionBubble : Window
{
    /// <summary>
    /// The space between the field and the bubble, in pixels.
    /// </summary>
    internal const int Gap = FieldFrame.FrameMargin + 4;

    private const double HiddenPosition = -32000;
    private const double MaxValueWidth = 320;
    private const double MaxContentWidth = 460;

    private static readonly Brush HoverBackground = CreateBrush(0x33, 0x33, 0x33);

    /// <summary>
    /// Initializes a new instance of the <see cref="SuggestionBubble"/> class, not shown yet.
    /// </summary>
    public SuggestionBubble()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = HiddenPosition;
        Top = HiddenPosition;

        SourceInitialized += (_, _) => OverlayWindows.MakeOverlay(Handle, letClicksThrough: false);
    }

    /// <summary>
    /// Occurs when the user clicks a value; gives its position in the list.
    /// </summary>
    public event EventHandler<int>? ValueClicked;

    /// <summary>
    /// Gets the position of the top left corner of the bubble in screen pixels.
    /// </summary>
    internal Point PixelPosition { get; private set; }

    /// <summary>
    /// Gets the handle of the window of the bubble.
    /// </summary>
    internal IntPtr Handle => new WindowInteropHelper(this).Handle;

    /// <summary>
    /// Sets the values proposed, each with the shortcut that picks it.
    /// </summary>
    /// <param name="suggestions">The values proposed, the most likely first.</param>
    /// <param name="shortcutNames">The name of the shortcut of each value, such as "Ctrl+Maj+1".</param>
    /// <param name="reason">Why the field is left to the user, or an empty string.</param>
    public void SetSuggestions(IReadOnlyList<SuggestedValue> suggestions, IReadOnlyList<string> shortcutNames, string reason)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        ArgumentNullException.ThrowIfNull(shortcutNames);
        ArgumentNullException.ThrowIfNull(reason);

        var panel = new StackPanel { MaxWidth = MaxContentWidth };
        if (reason.Length > 0)
        {
            // Why the field was not filled comes first: it is what lets the user choose with confidence
            var why = Text($"Pourquoi : {reason}", PopupColors.Text, 12, new Thickness(8, 0, 8, suggestions.Count > 0 ? 8 : 0));
            why.TextWrapping = TextWrapping.Wrap;
            why.TextTrimming = TextTrimming.None;
            panel.Children.Add(why);
        }

        if (suggestions.Count > 0)
        {
            panel.Children.Add(Text("Propositions", PopupColors.DiscreetText, 11, new Thickness(8, 0, 8, 4)));
            for (var index = 0; index < suggestions.Count; index++)
            {
                var shortcut = index < shortcutNames.Count ? shortcutNames[index] : string.Empty;
                panel.Children.Add(CreateRow(index, suggestions[index], shortcut));
            }

            panel.Children.Add(Text("Ou tape ta propre valeur", PopupColors.DiscreetText, 11, new Thickness(8, 4, 8, 0)));
        }

        Content = new Border
        {
            Background = PopupColors.Background,
            BorderBrush = PopupColors.ToCheck,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(4, 8, 4, 8),
            Child = panel,
        };
    }

    /// <summary>
    /// Shows the bubble just below a field.
    /// </summary>
    /// <param name="fieldBounds">The position of the field in screen pixels.</param>
    public void ShowBelow(Rect fieldBounds)
    {
        if (!IsVisible)
        {
            Show();
        }

        var position = new Point(fieldBounds.Left, fieldBounds.Bottom + Gap);
        if (position != PixelPosition)
        {
            OverlayWindows.Move(Handle, position);
            PixelPosition = position;
        }
    }

    private static SolidColorBrush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static TextBlock Text(string text, Brush color, double size, Thickness margin)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = color,
            FontSize = size,
            Margin = margin,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
    }

    private Border CreateRow(int index, SuggestedValue suggestion, string shortcut)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MaxWidth = MaxValueWidth });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var key = Text(shortcut, PopupColors.ToCheck, 11, new Thickness(0, 0, 10, 0));
        var value = Text(suggestion.Value, PopupColors.Text, 13, new Thickness(0, 0, 10, 0));
        var source = Text(suggestion.SourceName, PopupColors.DiscreetText, 11, default);
        Grid.SetColumn(value, 1);
        Grid.SetColumn(source, 2);
        grid.Children.Add(key);
        grid.Children.Add(value);
        grid.Children.Add(source);

        var row = new Border
        {
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4),
            Cursor = Cursors.Hand,
            Child = grid,
        };
        row.MouseEnter += (_, _) => row.Background = HoverBackground;
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        row.MouseLeftButtonUp += (_, _) => ValueClicked?.Invoke(this, index);
        return row;
    }
}
