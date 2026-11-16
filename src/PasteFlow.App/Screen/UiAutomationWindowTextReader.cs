using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using PasteFlow.App.Forms;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Screen;

namespace PasteFlow.App.Screen;

/// <summary>
/// Reads the text of the active window and of the windows just behind it through UI Automation.
/// </summary>
/// <remarks>
/// <para>Windows are read in parallel; a window that does not answer in time is skipped so the whole reading stays fast.</para>
/// <para>The filled fields of the windows behind the form are also read, each with its label, so that a form can be
/// copied into another one; password fields are never read.</para>
/// <para>A window behind the form that gives little or no text, such as a picture, a scanned PDF or a remote desktop,
/// is read from its image with the character recognition of Windows (OCR), when available and allowed.</para>
/// </remarks>
internal sealed class UiAutomationWindowTextReader : IWindowTextReader
{
    /// <summary>
    /// Below this number of letters and digits, a window is considered to give no text.
    /// </summary>
    internal const int MinAccessibleTextLength = 30;

    /// <summary>
    /// The longest value read from a field of another form; a longer text is not a value to copy.
    /// </summary>
    internal const int MaxFieldValueLength = 5000;

    private const int MaxWindows = 4;
    private const int MaxFilledFields = 100;
    private const int MaxDocumentLength = 100_000;
    private const int MaxGridCells = 2000;
    private const char ObjectReplacementCharacter = '\uFFFC';
    private const string UntitledWindow = "Fenêtre sans texte";

    private static readonly TimeSpan WindowTimeout = TimeSpan.FromMilliseconds(600);

    // The picture is read larger, and once more without the red of a spell checker: a full window of a
    // spreadsheet takes about 1.5 s (LibreOffice Calc, 15/11/2026)
    private static readonly TimeSpan ImageReadingTimeout = TimeSpan.FromMilliseconds(3000);

    private static readonly Condition DocumentCondition =
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document);

    private static readonly Condition TextCondition =
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text);

    // A terminal, such as Windows Terminal, shows its text through a text pattern, without a name; fields and
    // documents are read elsewhere, and so are the cells of a grid: their text pattern gives what the screen shows, cut
    // short when the cell is too narrow (« helene.more » in Excel, 16/11/2026)
    private static readonly Condition TextPatternCondition = new AndCondition(
        new PropertyCondition(AutomationElement.IsTextPatternAvailableProperty, true),
        new NotCondition(DocumentCondition),
        new NotCondition(new PropertyCondition(AutomationElement.IsGridItemPatternAvailableProperty, true)),
        new NotCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)));

    // A grid of cells, such as a spreadsheet; a list of a web page is read with its document
    private static readonly Condition GridCondition = new AndCondition(
        new PropertyCondition(AutomationElement.IsGridPatternAvailableProperty, true),
        new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataGrid),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Table)));

    private static readonly Condition CellCondition = new AndCondition(
        new PropertyCondition(AutomationElement.IsGridItemPatternAvailableProperty, true),
        new PropertyCondition(AutomationElement.IsValuePatternAvailableProperty, true));

    private readonly OcrTextRecognizer? _ocr;

    /// <summary>
    /// Initializes a new instance of the <see cref="UiAutomationWindowTextReader"/> class that reads accessible text only.
    /// </summary>
    public UiAutomationWindowTextReader()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UiAutomationWindowTextReader"/> class that reads the image of
    /// windows giving no text.
    /// </summary>
    /// <param name="ocr">The character recognition, or <see langword="null"/> when Windows has none.</param>
    public UiAutomationWindowTextReader(OcrTextRecognizer? ocr)
    {
        _ocr = ocr;
    }

    /// <summary>
    /// Gets or sets a value indicating whether windows giving no text are read from their image.
    /// </summary>
    public bool ReadImages { get; set; } = true;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WindowText>> ReadWindowsAsync(CancellationToken cancellationToken)
    {
        var windows = DesktopWindows.GetActiveAndBehind(MaxWindows);
        var readings = windows.Select((window, rank) => ReadWithFallbackAsync(window, rank, cancellationToken));

        var results = await Task.WhenAll(readings).ConfigureAwait(false);
        return results.OfType<WindowText>().ToList();
    }

    /// <summary>
    /// Reads the text of one window.
    /// </summary>
    /// <param name="handle">The window to read.</param>
    /// <param name="rank">The position of the window in the Alt+Tab order.</param>
    /// <returns>The text of the window, or <see langword="null"/> if the window cannot be read.</returns>
    internal static WindowText? ReadWindow(IntPtr handle, int rank)
    {
        try
        {
            var window = AutomationElement.FromHandle(handle);
            var text = ReadDocuments(window) ?? ReadTextElements(window);

            // The fields of the form being filled are its own, only the forms behind it give values
            IReadOnlyList<FieldText> fields = rank == 0 ? Array.Empty<FieldText>() : ReadFilledFields(window);

            // The site matters for the window being filled only: it tells which of the user's values goes there
            var address = rank == 0 ? BrowserAddress.TryRead(window) : string.Empty;
            var program = rank == 0 ? WindowProgram.NameOf(window.Current.ProcessId) : string.Empty;
            return new WindowText { Title = window.Current.Name, Text = text, Rank = rank, Fields = fields, Address = address, Program = program };
        }
        catch (ElementNotAvailableException)
        {
            // The window was closed while being read
            return null;
        }
        catch (COMException)
        {
            // The application does not answer accessibility requests
            return null;
        }
    }

    /// <summary>
    /// Reads the text of one window, from its image when it gives too little text.
    /// </summary>
    /// <param name="handle">The window to read.</param>
    /// <param name="rank">The position of the window in the Alt+Tab order; 0 is the form itself, never read from its image.</param>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The text of the window, or <see langword="null"/> if the window cannot be read.</returns>
    internal async Task<WindowText?> ReadWithFallbackAsync(IntPtr handle, int rank, CancellationToken cancellationToken)
    {
        var reading = StartReading(handle, rank, cancellationToken);
        var accessible = await WithinAsync(reading, WindowTimeout, cancellationToken).ConfigureAwait(false);
        var isLate = !reading.IsCompleted;
        var letterCount = accessible?.Text.Count(char.IsLetterOrDigit) ?? 0;
        if (rank == 0 || _ocr is null || !ReadImages || letterCount >= MinAccessibleTextLength)
        {
            return accessible;
        }

        // A window slow to answer, such as a spreadsheet full of cells, may still give its whole text while its image
        // is read: the screen shows a cell too narrow for its text cut short (« helene.more », Excel, 16/11/2026)
        var imageReading = ReadImageWithTimeoutAsync(_ocr, handle, cancellationToken);
        if (isLate)
        {
            var late = await WithinAsync(reading, ImageReadingTimeout, cancellationToken).ConfigureAwait(false);
            if ((late?.Text.Count(char.IsLetterOrDigit) ?? 0) >= MinAccessibleTextLength)
            {
                return late;
            }

            accessible = late;
            letterCount = late?.Text.Count(char.IsLetterOrDigit) ?? 0;
        }

        var fromImage = await imageReading.ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(fromImage) || fromImage.Count(char.IsLetterOrDigit) <= letterCount)
        {
            return accessible;
        }

        var title = accessible is { Title: { } accessibleTitle } && !string.IsNullOrWhiteSpace(accessibleTitle)
            ? accessibleTitle
            : UntitledWindow;
        return new WindowText
        {
            Title = title,
            Text = fromImage,
            Rank = rank,
            IsReadFromImage = true,
            Address = accessible?.Address ?? string.Empty,
            Links = accessible?.Links ?? Array.Empty<LinkText>(),
        };
    }

    /// <summary>
    /// Gives the result of a reading if it ends in time.
    /// </summary>
    /// <param name="reading">The reading.</param>
    /// <param name="timeout">The time it is given.</param>
    /// <param name="cancellationToken">A token to cancel the wait.</param>
    /// <returns>The text read, or <see langword="null"/> if the reading is late; it goes on in the background.</returns>
    private static async Task<WindowText?> WithinAsync(Task<WindowText?> reading, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var finished = await Task.WhenAny(reading, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
        return finished == reading ? await reading.ConfigureAwait(false) : null;
    }

    private static Task<WindowText?> StartReading(IntPtr window, int rank, CancellationToken cancellationToken)
    {
        // The links of a window behind are read aside, in the same time: a page full of links never delays its text
        var reading = Task.Run(() => ReadWindow(window, rank), cancellationToken);
        var linksReading = rank == 0
            ? Task.FromResult<(string Address, IReadOnlyList<LinkText> Links)>((string.Empty, Array.Empty<LinkText>()))
            : Task.Run(() => WindowLinks.Read(window), cancellationToken);
        return AddLinksAsync(reading, linksReading, Task.Delay(WindowTimeout, cancellationToken), rank);
    }

    private static async Task<WindowText?> AddLinksAsync(
        Task<WindowText?> reading,
        Task<(string Address, IReadOnlyList<LinkText> Links)> linksReading,
        Task deadline,
        int rank)
    {
        // UI Automation calls cannot be interrupted: a late window ends its reading in the background
        var text = await reading.ConfigureAwait(false);
        if (text is null || rank == 0)
        {
            return text;
        }

        // The links never make the text later than the time given to a window
        var finished = await Task.WhenAny(linksReading, deadline).ConfigureAwait(false);
        if (finished != linksReading)
        {
            return text;
        }

        var (address, links) = await linksReading.ConfigureAwait(false);
        return text with { Address = address, Links = links };
    }

    private static async Task<string?> ReadImageWithTimeoutAsync(OcrTextRecognizer ocr, IntPtr handle, CancellationToken cancellationToken)
    {
        try
        {
            var reading = Task.Run(
                async () =>
                {
                    var png = WindowCapture.TryCapturePng(handle);
                    return png is null ? null : await ocr.RecognizeAsync(png, cancellationToken).ConfigureAwait(false);
                },
                cancellationToken);
            var finished = await Task.WhenAny(reading, Task.Delay(ImageReadingTimeout, cancellationToken)).ConfigureAwait(false);
            return finished == reading ? await reading.ConfigureAwait(false) : null;
        }
        catch (Exception exception) when (exception is ExternalException or ArgumentException)
        {
            // The picture could not be taken or read: the window simply gives no text
            return null;
        }
    }

    /// <summary>
    /// Reads documents such as web pages or text editors, which expose their whole text at once.
    /// </summary>
    /// <param name="window">The window to read.</param>
    /// <returns>The text of the documents, or <see langword="null"/> if the window has none.</returns>
    private static string? ReadDocuments(AutomationElement window)
    {
        var builder = new StringBuilder();
        foreach (AutomationElement document in window.FindAll(TreeScope.Descendants, DocumentCondition))
        {
            if (document.TryGetCurrentPattern(TextPattern.Pattern, out var pattern) && pattern is TextPattern textPattern)
            {
                // Browsers put a placeholder character where a field or an image sits in the text: it is a break, not a value
                builder.AppendLine(textPattern.DocumentRange.GetText(MaxDocumentLength).Replace(ObjectReplacementCharacter, '\n'));
            }
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }

    /// <summary>
    /// Reads the text labels of classic applications, one per line.
    /// </summary>
    /// <param name="window">The window to read.</param>
    /// <returns>The labels separated by line breaks.</returns>
    private static string ReadTextElements(AutomationElement window)
    {
        var lines = window.FindAll(TreeScope.Descendants, TextCondition)
            .Cast<AutomationElement>()
            .Select(element => element.Current.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        lines.AddRange(ReadTextPatterns(window));
        lines.AddRange(ReadGrids(window));
        return string.Join('\n', lines);
    }

    /// <summary>
    /// Reads the grids of cells, such as a spreadsheet, one row per line with a tab between cells: the whole value of
    /// each cell, even when the cell is too narrow to show it (seen in Excel on 14/11/2026, read « helene.more » from
    /// its image). Only the cells of the grid that Windows exposes are read, usually those on screen.
    /// </summary>
    /// <param name="window">The window to read.</param>
    /// <returns>The rows that hold at least one value.</returns>
    private static List<string> ReadGrids(AutomationElement window)
    {
        var rows = new List<string>();
        foreach (AutomationElement grid in window.FindAll(TreeScope.Descendants, GridCondition))
        {
            try
            {
                var cache = new CacheRequest { AutomationElementMode = AutomationElementMode.None };
                cache.Add(ValuePattern.ValueProperty);
                cache.Add(GridItemPattern.RowProperty);
                cache.Add(GridItemPattern.ColumnProperty);
                AutomationElementCollection cells;
                using (cache.Activate())
                {
                    cells = grid.FindAll(TreeScope.Descendants, CellCondition);
                }

                var values = new List<(int Row, int Column, string Value)>();
                foreach (AutomationElement cell in cells.Cast<AutomationElement>().Take(MaxGridCells))
                {
                    if (cell.GetCachedPropertyValue(ValuePattern.ValueProperty, ignoreDefaultValue: true) is string value
                        && !string.IsNullOrWhiteSpace(value)
                        && cell.GetCachedPropertyValue(GridItemPattern.RowProperty, ignoreDefaultValue: false) is int row
                        && cell.GetCachedPropertyValue(GridItemPattern.ColumnProperty, ignoreDefaultValue: false) is int column)
                    {
                        values.Add((row, column, value.Trim()));
                    }
                }

                rows.AddRange(values
                    .GroupBy(cell => cell.Row)
                    .OrderBy(row => row.Key)
                    .Select(row => string.Join('	', row.OrderBy(cell => cell.Column).Select(cell => cell.Value))));
            }
            catch (Exception exception) when (exception is ElementNotAvailableException or InvalidOperationException or COMException or ArgumentException)
            {
                // A grid that answers badly does not stop the reading
            }
        }

        return rows;
    }

    /// <summary>
    /// Reads the elements that give their text only through a text pattern, such as a terminal: only the part seen on
    /// screen, not the whole history.
    /// </summary>
    /// <param name="window">The window to read.</param>
    /// <returns>The texts seen.</returns>
    private static List<string> ReadTextPatterns(AutomationElement window)
    {
        var texts = new List<string>();
        foreach (AutomationElement element in window.FindAll(TreeScope.Descendants, TextPatternCondition))
        {
            try
            {
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern) && pattern is TextPattern textPattern)
                {
                    // A label gives the same text as its name, already read; a terminal gives far more than its name,
                    // such as "Windows PowerShell"
                    var name = element.Current.Name.Trim();
                    texts.AddRange(textPattern.GetVisibleRanges()
                        .Select(range => range.GetText(MaxDocumentLength))
                        .Where(text => !string.IsNullOrWhiteSpace(text) && !string.Equals(text.Trim(), name, StringComparison.Ordinal)));
                }
            }
            catch (Exception exception) when (exception is ElementNotAvailableException or InvalidOperationException or COMException or ArgumentException)
            {
                // One element that answers badly does not stop the others
            }
        }

        return texts;
    }

    /// <summary>
    /// Reads the filled fields of a form behind, each with its label, so that a form can be copied into another.
    /// Password fields, search boxes and address bars are never read.
    /// </summary>
    /// <param name="window">The window to read.</param>
    /// <returns>The filled fields, in reading order.</returns>
    private static List<FieldText> ReadFilledFields(AutomationElement window)
    {
        var fields = new List<FieldText>();
        var windowBounds = window.Current.BoundingRectangle;
        foreach (var element in UiAutomationFormAccess.FindFields(window))
        {
            if (fields.Count == MaxFilledFields)
            {
                break;
            }

            if (!UiAutomationFormAccess.IsInPage(element, windowBounds))
            {
                continue;
            }

            var field = TryReadFilledField(element);
            if (field is not null)
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    private static FieldText? TryReadFilledField(AutomationElement element)
    {
        try
        {
            var current = element.Current;
            if (current.IsPassword || UiAutomationFormAccess.IsInsideComboBox(element))
            {
                return null;
            }

            var label = UiAutomationFormAccess.ReadLabel(current);
            var field = new FormField
            {
                Id = string.Empty,
                Label = label,
                TechnicalName = current.AutomationId ?? string.Empty,
                Hint = current.HelpText ?? string.Empty,
                Kind = FieldKind.Text,
                HasValue = true,
            };

            // What was typed in a search box or an address bar is not a value of the form
            if (label.Length == 0 || FillRules.IsSearchBox(field))
            {
                return null;
            }

            var value = ReadValue(element)?.Replace(ObjectReplacementCharacter, ' ').Trim();
            var isUsable = !string.IsNullOrEmpty(value)
                && !string.IsNullOrEmpty(value)
                && value.Length <= MaxFieldValueLength
                && !string.Equals(value, label, StringComparison.OrdinalIgnoreCase);
            return isUsable ? new FieldText { Label = label, Value = value! } : null;
        }
        catch (ElementNotAvailableException)
        {
            // The field disappeared while being read
            return null;
        }
    }

    /// <summary>
    /// Reads the whole value of a field, line breaks included: its value, else its text for rich text boxes.
    /// </summary>
    /// <param name="element">The field.</param>
    /// <returns>The value, or <see langword="null"/> if the field gives none.</returns>
    private static string? ReadValue(AutomationElement element)
    {
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern) && valuePattern is ValuePattern value
            && !string.IsNullOrWhiteSpace(value.Current.Value))
        {
            return value.Current.Value;
        }

        return element.TryGetCurrentPattern(TextPattern.Pattern, out var textPattern) && textPattern is TextPattern text
            ? text.DocumentRange.GetText(MaxFieldValueLength + 1)
            : null;
    }
}
