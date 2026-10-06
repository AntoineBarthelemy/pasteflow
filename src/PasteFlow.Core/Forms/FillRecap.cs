using System.Globalization;

namespace PasteFlow.Core.Forms;

/// <summary>
/// What the recap shown after a fill contains, ready to be displayed.
/// </summary>
/// <remarks>
/// The fields that need the user come first, then the fields filled, each in the order of the form.
/// Only the first <see cref="MaxLines"/> fields are listed.
/// </remarks>
public sealed record FillRecap
{
    /// <summary>
    /// The maximum number of fields listed.
    /// </summary>
    public const int MaxLines = 8;

    /// <summary>
    /// The maximum length of a label or a value.
    /// </summary>
    public const int MaxTextLength = 40;

    private const string UnnamedField = "Champ sans nom";

    /// <summary>
    /// Gets the result in a few words, such as "2 champs remplis, 1 à vérifier".
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the time the fill took, such as "120 ms".
    /// </summary>
    public required string Duration { get; init; }

    /// <summary>
    /// Gets the fields listed.
    /// </summary>
    public required IReadOnlyList<RecapLine> Lines { get; init; }

    /// <summary>
    /// Gets the number of fields not listed for lack of room.
    /// </summary>
    public int HiddenLineCount { get; init; }

    /// <summary>
    /// Gets how to undo the fill, such as "Ctrl+Alt+Z pour tout annuler", or <see langword="null"/> when there is
    /// nothing to undo.
    /// </summary>
    public string? UndoHint { get; init; }

    /// <summary>
    /// Gets secondary information: the windows read and the example values set aside.
    /// </summary>
    public required IReadOnlyList<string> Details { get; init; }

    /// <summary>
    /// Prepares the recap of a fill.
    /// </summary>
    /// <param name="report">The result of the fill.</param>
    /// <returns>The recap to display.</returns>
    public static FillRecap From(FillReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var failedIds = report.FailedFields.Select(field => field.Id).ToHashSet(StringComparer.Ordinal);
        var lines = report.FieldsToCheck
            .Select(field => failedIds.Contains(field.Id)
                ? Line(RecapLineKind.Failed, field.Label, "non écrit")
                : Line(RecapLineKind.ToCheck, field.Label, "à vérifier"))
            .Concat(report.FilledValues.Select(filled => Line(RecapLineKind.Filled, filled.FieldLabel, filled.Value) with
            {
                SourceName = FillReport.Shorten(filled.SourceName, MaxTextLength),
            }))
            .ToList();

        var details = new List<string>(capacity: 2) { report.DescribeSources() };
        if (report.DescribeIgnoredSamples() is { } ignored)
        {
            details.Add(ignored);
        }

        return new FillRecap
        {
            Title = report.Summary(),
            Duration = string.Create(CultureInfo.InvariantCulture, $"{Math.Round(report.Elapsed.TotalMilliseconds)} ms"),
            Lines = lines.Take(MaxLines).ToList(),
            HiddenLineCount = Math.Max(lines.Count - MaxLines, 0),
            UndoHint = report.FilledCount > 0 && report.UndoHotkey is not null
                ? $"{report.UndoHotkey} pour tout annuler"
                : null,
            Details = details,
        };
    }

    private static RecapLine Line(RecapLineKind kind, string label, string value)
    {
        return new RecapLine
        {
            Kind = kind,
            Label = string.IsNullOrWhiteSpace(label) ? UnnamedField : FillReport.Shorten(label, MaxTextLength),
            Value = FillReport.Shorten(value, MaxTextLength),
        };
    }
}
