using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PasteFlow.App.Highlights;
using PasteFlow.Core.Hints;

namespace PasteFlow.App.Hints;

/// <summary>
/// Layer over the whole screen that puts a code of letters on each value seen, as Vimium does in a browser: typing a
/// code chooses its value, Échap or a click cancels, Retour arrière erases the last letter.
/// </summary>
internal sealed class HintOverlayWindow : Window
{
    private const double HiddenPosition = -32000;
    private const double LetterWidth = 9;
    private const double LabelPadding = 10;

    private static readonly Brush LabelBackground = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F)));
    private static readonly Brush LabelBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xC5, 0x8B, 0x00)));
    private static readonly Brush TypedLetters = Freeze(new SolidColorBrush(Color.FromRgb(0xA0, 0x7A, 0x10)));
    private static readonly Brush ShadeBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x22, 0x00, 0x00, 0x00)));

    private readonly IReadOnlyList<HintTarget> _targets;
    private readonly IReadOnlyList<string> _codes;
    private readonly Rect _screen;
    private readonly Canvas _canvas = new();
    private readonly List<(Border Label, TextBlock Text)> _labels = new();
    private readonly TaskCompletionSource<HintTarget?> _choice = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _isClosing;

    /// <summary>
    /// Initializes a new instance of the <see cref="HintOverlayWindow"/> class.
    /// </summary>
    /// <param name="targets">The values with their codes.</param>
    /// <param name="screen">The whole screen, all monitors together, in pixels.</param>
    public HintOverlayWindow(IReadOnlyList<HintTarget> targets, Rect screen)
    {
        ArgumentNullException.ThrowIfNull(targets);

        _targets = targets;
        _codes = targets.Select(target => target.Code).ToList();
        _screen = screen;
        Title = "PasteFlow : coller une valeur au clavier";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = ShadeBrush;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // Created out of sight: the real place is given in pixels once the window exists
        Left = HiddenPosition;
        Top = HiddenPosition;
        Width = 1;
        Height = 1;
        Content = _canvas;

        SourceInitialized += (_, _) => PlaceLabels();
        PreviewTextInput += (_, e) =>
        {
            e.Handled = true;
            foreach (var letter in e.Text.Where(char.IsLetter))
            {
                Type(letter);
            }
        };
        PreviewKeyDown += OnPreviewKeyDown;
        MouseDown += (_, _) => Finish(null);
        Deactivated += (_, _) => _ = Dispatcher.BeginInvoke(new Action(() => Finish(null)));
        Closing += (_, _) => _isClosing = true;
        Closed += (_, _) => _ = _choice.TrySetResult(null);
        Loaded += (_, _) =>
        {
            _ = Activate();
            _ = Focus();
        };
    }

    /// <summary>
    /// Gets the value chosen, or <see langword="null"/> once cancelled.
    /// </summary>
    public Task<HintTarget?> Choice => _choice.Task;

    /// <summary>
    /// Gets the letters typed so far.
    /// </summary>
    internal string Typed { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the codes still shown, those that start with the letters typed.
    /// </summary>
    internal IReadOnlyList<string> ShownCodes => _labels
        .Where(label => label.Label.Visibility == Visibility.Visible)
        .Select(label => string.Concat(label.Text.Inlines.OfType<Run>().Select(run => run.Text)))
        .ToList();

    /// <summary>
    /// Gets where the label of a value goes on the layer, in device independent units.
    /// </summary>
    /// <param name="target">The value.</param>
    /// <param name="screen">The whole screen, in pixels.</param>
    /// <param name="scale">The number of pixels in a device independent unit.</param>
    /// <param name="shift">How far to the right of its place the label goes, when other labels share the place.</param>
    /// <returns>The top left corner of the label.</returns>
    internal static Point PlaceOf(HintTarget target, Rect screen, DpiScale scale, double shift)
    {
        ArgumentNullException.ThrowIfNull(target);

        return new Point(
            ((target.Place.Left - screen.Left) / scale.DpiScaleX) + shift,
            (target.Place.Top - screen.Top) / scale.DpiScaleY);
    }

    /// <summary>
    /// Types a letter, as the user does.
    /// </summary>
    /// <param name="letter">The letter.</param>
    internal void Type(char letter)
    {
        var typed = Typed + char.ToUpperInvariant(letter);
        var (chosen, canGoOn) = HintCodes.Read(_codes, typed);
        if (chosen is { } index)
        {
            Finish(_targets[index]);
            return;
        }

        // A letter that starts no code is ignored: the user simply types the right one
        if (canGoOn)
        {
            Typed = typed;
            ShowTyped();
        }
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    private void PlaceLabels()
    {
        OverlayWindows.Place(new WindowInteropHelper(this).Handle, _screen);
        var scale = VisualTreeHelper.GetDpi(this);
        var shifts = new Dictionary<ScreenText, double>();
        foreach (var target in _targets)
        {
            var shift = shifts.GetValueOrDefault(target.Place);
            shifts[target.Place] = shift + (target.Code.Length * LetterWidth) + LabelPadding;

            var text = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
            };
            var label = new Border
            {
                Background = LabelBackground,
                BorderBrush = LabelBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(3, 0, 3, 0),
                Child = text,
                ToolTip = target.Value,
            };
            var place = PlaceOf(target, _screen, scale, shift);
            Canvas.SetLeft(label, place.X);
            Canvas.SetTop(label, place.Y);
            _ = _canvas.Children.Add(label);
            _labels.Add((label, text));
        }

        ShowTyped();
    }

    private void ShowTyped()
    {
        for (var index = 0; index < _labels.Count; index++)
        {
            var (label, text) = _labels[index];
            var code = _codes[index];
            var matches = code.StartsWith(Typed, StringComparison.Ordinal);
            label.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
            text.Inlines.Clear();
            if (matches)
            {
                // The letters already typed fade, the ones left stand out
                text.Inlines.Add(new Run(code[..Typed.Length]) { Foreground = TypedLetters });
                text.Inlines.Add(new Run(code[Typed.Length..]));
            }
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Finish(null);
                break;
            case Key.Back:
                e.Handled = true;
                if (Typed.Length > 0)
                {
                    Typed = Typed[..^1];
                    ShowTyped();
                }

                break;
        }
    }

    private void Finish(HintTarget? chosen)
    {
        _ = _choice.TrySetResult(chosen);
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        Close();
    }
}
