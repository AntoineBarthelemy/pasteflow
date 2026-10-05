using System.Globalization;

namespace PasteFlow.Core.Forms;

/// <summary>
/// Result of an attempt to fill a form.
/// </summary>
public sealed record FillReport
{
    /// <summary>
    /// Gets the number of fields that were filled.
    /// </summary>
    public required int FilledCount { get; init; }

    /// <summary>
    /// Gets the number of fields left untouched because the user had already filled them.
    /// </summary>
    public required int AlreadyFilledCount { get; init; }

    /// <summary>
    /// Gets the number of fields that could not be written.
    /// </summary>
    public required int FailedCount { get; init; }

    /// <summary>
    /// Gets the time spent reading and filling the form.
    /// </summary>
    public required TimeSpan Elapsed { get; init; }

    /// <summary>
    /// Describes the result in a short French message.
    /// </summary>
    /// <returns>A message such as "2 champs remplis, 1 déjà rempli laissé intact. En 120 ms.".</returns>
    public string Describe()
    {
        var parts = new List<string>(capacity: 3);
        if (FilledCount > 0)
        {
            parts.Add(Count(FilledCount, "champ rempli", "champs remplis"));
        }

        if (AlreadyFilledCount > 0)
        {
            parts.Add(Count(AlreadyFilledCount, "déjà rempli laissé intact", "déjà remplis laissés intacts"));
        }

        if (FailedCount > 0)
        {
            parts.Add(Count(FailedCount, "champ impossible à remplir", "champs impossibles à remplir"));
        }

        var result = parts.Count > 0 ? string.Join(", ", parts) : "Aucun champ à remplir dans cette fenêtre";
        var duration = string.Create(CultureInfo.InvariantCulture, $"En {Math.Round(Elapsed.TotalMilliseconds)} ms.");
        return $"{result}. {duration}";
    }

    private static string Count(int count, string singular, string plural)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? singular : plural)}");
    }
}
