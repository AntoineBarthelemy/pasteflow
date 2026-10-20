using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using PasteFlow.App.Interop;

namespace PasteFlow.App.Design;

/// <summary>
/// The look of PasteFlow, inspired by the desktop application of Claude and by Apple: warm colors, one accent, quiet
/// shapes, nothing in excess. It follows the light or dark theme of Windows.
/// </summary>
/// <remarks>
/// Brushes and fonts are read when an element is built: a window opened after Windows changes theme takes the new
/// one. The styles of the standard controls (buttons, fields, lists…) come from <c>Controls.xaml</c>.
/// </remarks>
internal static class Theme
{
    /// <summary>
    /// The resource keys of the brushes used by the styles.
    /// </summary>
    public const string BackgroundKey = "Pf.Background";

    /// <summary>
    /// The radius of fields and buttons.
    /// </summary>
    public const double ControlRadius = 8;

    /// <summary>
    /// The radius of cards and popups.
    /// </summary>
    public const double CardRadius = 12;

    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const int DarkModeAttribute = 20;
    private const int CaptionColorAttribute = 35;
    private const int TextColorAttribute = 36;

    [ThreadStatic]
    private static ResourceDictionary? _lightResources;

    [ThreadStatic]
    private static ResourceDictionary? _darkResources;

    /// <summary>
    /// Gets or sets the theme forced for the tests and the snapshots; <see langword="null"/> follows Windows.
    /// </summary>
    public static bool? ForcedDark { get; set; }

    /// <summary>
    /// Gets a value indicating whether Windows is in dark mode for applications.
    /// </summary>
    public static bool IsDark => ForcedDark ?? ReadSystemIsDark();

    /// <summary>
    /// Gets the palette of the current theme.
    /// </summary>
    public static Palette Current => IsDark ? Palette.Dark : Palette.Light;

    /// <summary>
    /// Gets the font of the interface: Segoe UI Variable on Windows 11, Segoe UI on Windows 10.
    /// </summary>
    public static FontFamily UiFont { get; } = new("Segoe UI Variable Text, Segoe UI");

    /// <summary>
    /// Gets the font of the large titles.
    /// </summary>
    public static FontFamily DisplayFont { get; } = new("Segoe UI Variable Display, Segoe UI");

    /// <summary>
    /// Gets the serif font of the titles of the windows, a warm touch in the way of Claude.
    /// </summary>
    public static FontFamily SerifFont { get; } = new("Georgia");

    /// <summary>
    /// Gets the font of the keys and codes.
    /// </summary>
    public static FontFamily MonoFont { get; } = new("Cascadia Mono, Consolas");

    /// <summary>
    /// Gets the font of the icons, installed with Windows.
    /// </summary>
    public static FontFamily IconFont { get; } = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    /// <summary>
    /// Gets a frozen brush of a color of the current palette.
    /// </summary>
    /// <param name="pick">The color to take from the palette.</param>
    /// <returns>The brush.</returns>
    public static SolidColorBrush Brush(Func<Palette, Color> pick)
    {
        ArgumentNullException.ThrowIfNull(pick);

        var brush = new SolidColorBrush(pick(Current));
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Gets the soft shadow of cards and popups.
    /// </summary>
    /// <returns>A new shadow, to give to one element.</returns>
    public static Effect CardShadow()
    {
        return new DropShadowEffect
        {
            Color = Current.Shadow.WithoutAlpha(),
            Opacity = Current.Shadow.A / 255.0,
            BlurRadius = 24,
            ShadowDepth = 6,
            Direction = 270,
        };
    }

    /// <summary>
    /// Gives an element the look of PasteFlow: the brushes and the styles of the standard controls.
    /// </summary>
    /// <param name="element">A window, or the root of a popup.</param>
    public static void Apply(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        element.Resources.MergedDictionaries.Add(Resources());
        TextOptions.SetTextFormattingMode(element, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(element, TextRenderingMode.ClearType);
        element.UseLayoutRounding = true;
        element.SnapsToDevicePixels = true;
    }

    /// <summary>
    /// Paints the title bar of a window in the colors of the theme, so that it melts into the window (Windows 11);
    /// Windows 10 only takes the dark mode.
    /// </summary>
    /// <param name="window">The window, once its handle exists.</param>
    public static void PaintTitleBar(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var dark = Current.IsDark ? 1 : 0;
        _ = NativeMethods.DwmSetWindowAttribute(handle, DarkModeAttribute, ref dark, sizeof(int));
        var caption = ColorRef(Current.Background);
        _ = NativeMethods.DwmSetWindowAttribute(handle, CaptionColorAttribute, ref caption, sizeof(int));
        var text = ColorRef(Current.Text);
        _ = NativeMethods.DwmSetWindowAttribute(handle, TextColorAttribute, ref text, sizeof(int));
    }

    /// <summary>
    /// Gets the resources of the current theme for this thread: brushes and styles of the standard controls.
    /// </summary>
    /// <returns>The resources, shared by the elements of the thread.</returns>
    internal static ResourceDictionary Resources()
    {
        if (IsDark)
        {
            return _darkResources ??= Build(Palette.Dark);
        }

        return _lightResources ??= Build(Palette.Light);
    }

    private static ResourceDictionary Build(Palette palette)
    {
        var resources = new ResourceDictionary();
        void Add(string name, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[$"Pf.{name}"] = brush;
        }

        Add("Background", palette.Background);
        Add("Surface", palette.Surface);
        Add("Sunken", palette.Sunken);
        Add("Hover", palette.Hover);
        Add("Border", palette.Border);
        Add("BorderStrong", palette.BorderStrong);
        Add("Text", palette.Text);
        Add("TextSecondary", palette.TextSecondary);
        Add("TextTertiary", palette.TextTertiary);
        Add("Accent", palette.Accent);
        Add("AccentHover", palette.AccentHover);
        Add("AccentSoft", palette.AccentSoft);
        Add("OnAccent", palette.OnAccent);
        Add("Success", palette.Success);
        Add("Warning", palette.Warning);
        Add("Danger", palette.Danger);
        resources["Pf.UiFont"] = UiFont;
        resources["Pf.MonoFont"] = MonoFont;
        resources["Pf.IconFont"] = IconFont;
        resources.MergedDictionaries.Add(LoadControls());
        return resources;
    }

    private static ResourceDictionary LoadControls()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PasteFlow.App.Design.Controls.xaml")
            ?? throw new InvalidOperationException("Les styles de PasteFlow sont introuvables.");
        return (ResourceDictionary)XamlReader.Load(stream);
    }

    private static bool ReadSystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private static int ColorRef(Color color)
    {
        return color.R | (color.G << 8) | (color.B << 16);
    }

    private static Color WithoutAlpha(this Color color)
    {
        return Color.FromRgb(color.R, color.G, color.B);
    }
}
