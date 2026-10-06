using System.Globalization;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Hotkeys;

namespace PasteFlow.Core.Forms;

/// <summary>
/// Result of an attempt to fill a form.
/// </summary>
public sealed record FillReport
{
    /// <summary>
    /// The maximum number of sources listed in the message.
    /// </summary>
    public const int MaxListedSources = 4;

    /// <summary>
    /// The maximum length of a source name in the message.
    /// </summary>
    public const int MaxSourceNameLength = 40;

    /// <summary>
    /// The maximum number of filled values detailed in the message.
    /// </summary>
    public const int MaxListedValues = 4;

    /// <summary>
    /// The maximum length of a value in the message.
    /// </summary>
    public const int MaxValueLength = 40;

    /// <summary>
    /// Gets the number of fields that were filled.
    /// </summary>
    public required int FilledCount { get; init; }

    /// <summary>
    /// Gets the number of fields left empty because no value was sure enough.
    /// </summary>
    public required int UncertainCount { get; init; }

    /// <summary>
    /// Gets the number of fields left untouched because the user had already filled them.
    /// </summary>
    public required int AlreadyFilledCount { get; init; }

    /// <summary>
    /// Gets the number of fields that could not be written.
    /// </summary>
    public required int FailedCount { get; init; }

    /// <summary>
    /// Gets the values written, in the order of the fields.
    /// </summary>
    public IReadOnlyList<FilledValue> FilledValues { get; init; } = Array.Empty<FilledValue>();

    /// <summary>
    /// Gets the sources read during the fill, closest first.
    /// </summary>
    public required IReadOnlyList<SourceSummary> Sources { get; init; }

    /// <summary>
    /// Gets the number of distinct values recognized in the sources, by kind. Plain text is not counted.
    /// </summary>
    public IReadOnlyDictionary<CandidateKind, int> RecognizedCounts { get; init; } = new Dictionary<CandidateKind, int>();

    /// <summary>
    /// Gets the number of distinct example values ignored, such as "jean.dupont@exemple.fr" or a placeholder.
    /// </summary>
    public int IgnoredSampleCount { get; init; }

    /// <summary>
    /// Gets the time spent reading and filling the form.
    /// </summary>
    public required TimeSpan Elapsed { get; init; }

    /// <summary>
    /// Gets the fields the user should check: those without a sure value and those that could not be written.
    /// </summary>
    public IReadOnlyList<FieldToCheck> FieldsToCheck { get; init; } = Array.Empty<FieldToCheck>();

    /// <summary>
    /// Gets the fields whose value could not be written; they are also part of <see cref="FieldsToCheck"/>.
    /// </summary>
    public IReadOnlyList<FormField> FailedFields { get; init; } = Array.Empty<FormField>();

    /// <summary>
    /// Gets the shortcut that undoes the fill, or <see langword="null"/> if undoing is not available.
    /// </summary>
    public Hotkey? UndoHotkey { get; init; }

    /// <summary>
    /// Describes the result in a short French message.
    /// </summary>
    /// <returns>
    /// A message such as "2 champs remplis, 1 à vérifier. En 120 ms.", followed by one line per value written,
    /// such as "Email : antoine@example.com (Gmail)", by the undo shortcut, then by the sources read, such as
    /// "Lu à l'écran : Gmail (84), Bloc-notes (12).", by the values recognized, such as "Reconnu : 1 e-mail.",
    /// and by the example values ignored, such as "Ignoré : 2 exemples.".
    /// </returns>
    public string Describe()
    {
        var duration = string.Create(CultureInfo.InvariantCulture, $"En {Math.Round(Elapsed.TotalMilliseconds)} ms.");
        var message = $"{Summary()}. {duration}";
        foreach (var filled in FilledValues.Take(MaxListedValues))
        {
            message += $"\n{DescribeFilledValue(filled)}";
        }

        if (FilledCount > 0 && UndoHotkey is not null)
        {
            message += $"\n{UndoHotkey} pour tout annuler.";
        }

        message = $"{message}\n{DescribeSources()}";
        if (!Sources.Any(source => source.CandidateCount > 0))
        {
            return message;
        }

        var recognized = RecognizedCounts
            .Where(entry => entry.Key != CandidateKind.Text && entry.Value > 0)
            .OrderBy(entry => entry.Key)
            .Select(entry => DescribeKind(entry.Key, entry.Value))
            .ToList();

        if (recognized.Count > 0)
        {
            message = $"{message}\nReconnu : {string.Join(", ", recognized)}.";
        }

        var ignored = DescribeIgnoredSamples();
        return ignored is null ? message : $"{message}\n{ignored}";
    }

    /// <summary>
    /// Sums the result up in a few words.
    /// </summary>
    /// <returns>
    /// A summary such as "2 champs remplis, 1 à vérifier", or "Aucun champ à remplir dans cette fenêtre".
    /// </returns>
    public string Summary()
    {
        var parts = new List<string>(capacity: 4);
        if (FilledCount > 0)
        {
            parts.Add(Count(FilledCount, "champ rempli", "champs remplis"));
        }

        if (UncertainCount > 0)
        {
            parts.Add(Count(UncertainCount, "à vérifier", "à vérifier"));
        }

        if (AlreadyFilledCount > 0)
        {
            parts.Add(Count(AlreadyFilledCount, "déjà rempli laissé intact", "déjà remplis laissés intacts"));
        }

        if (FailedCount > 0)
        {
            parts.Add(Count(FailedCount, "champ impossible à remplir", "champs impossibles à remplir"));
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "Aucun champ à remplir dans cette fenêtre";
    }

    /// <summary>
    /// Describes the windows read.
    /// </summary>
    /// <returns>A line such as "Lu à l'écran : Gmail (84), Bloc-notes (12).", or "Rien lu à l'écran.".</returns>
    public string DescribeSources()
    {
        var sources = Sources.Where(source => source.CandidateCount > 0).ToList();
        if (sources.Count == 0)
        {
            return "Rien lu à l'écran.";
        }

        var listed = sources.Take(MaxListedSources).Select(DescribeSource);
        return $"Lu à l'écran : {string.Join(", ", listed)}.";
    }

    /// <summary>
    /// Describes the example values set aside.
    /// </summary>
    /// <returns>A line such as "Ignoré : 2 exemples.", or <see langword="null"/> when none was ignored.</returns>
    public string? DescribeIgnoredSamples()
    {
        return IgnoredSampleCount > 0 ? $"Ignoré : {Count(IgnoredSampleCount, "exemple", "exemples")}." : null;
    }

    /// <summary>
    /// Shortens a text to a maximum length, ending it with an ellipsis when cut.
    /// </summary>
    /// <param name="text">The text to shorten.</param>
    /// <param name="maxLength">The maximum length, ellipsis included.</param>
    /// <returns>The text, trimmed and shortened if needed.</returns>
    internal static string Shorten(string text, int maxLength)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..(maxLength - 1)]}…";
    }

    private static string DescribeFilledValue(FilledValue filled)
    {
        var label = string.IsNullOrWhiteSpace(filled.FieldLabel) ? "Champ sans nom" : filled.FieldLabel.Trim();
        return $"{label} : {Shorten(filled.Value, MaxValueLength)} ({Shorten(filled.SourceName, MaxSourceNameLength)})";
    }

    private static string DescribeKind(CandidateKind kind, int count)
    {
        var (singular, plural) = kind switch
        {
            CandidateKind.Email => ("e-mail", "e-mails"),
            CandidateKind.Phone => ("téléphone", "téléphones"),
            CandidateKind.Url => ("lien", "liens"),
            CandidateKind.Date => ("date", "dates"),
            CandidateKind.Amount => ("montant", "montants"),
            CandidateKind.Iban => ("IBAN", "IBAN"),
            CandidateKind.Reference => ("référence", "références"),
            CandidateKind.Number => ("numéro", "numéros"),
            _ => ("texte", "textes"),
        };

        return Count(count, singular, plural);
    }

    private static string DescribeSource(SourceSummary source)
    {
        var name = string.IsNullOrWhiteSpace(source.Name) ? "sans titre" : Shorten(source.Name, MaxSourceNameLength);
        return string.Create(CultureInfo.InvariantCulture, $"{name} ({source.CandidateCount})");
    }

    private static string Count(int count, string singular, string plural)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? singular : plural)}");
    }
}
