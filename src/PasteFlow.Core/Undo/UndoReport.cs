namespace PasteFlow.Core.Undo;

/// <summary>
/// The result of undoing the last fill.
/// </summary>
public sealed record UndoReport
{
    /// <summary>
    /// Gets the number of fields emptied.
    /// </summary>
    public required int EmptiedCount { get; init; }

    /// <summary>
    /// Gets the number of fields left as they are because the user changed them since the fill.
    /// </summary>
    public required int ChangedCount { get; init; }

    /// <summary>
    /// Gets the number of fields that could not be read or emptied, for example because the window was closed.
    /// </summary>
    public required int FailedCount { get; init; }

    /// <summary>
    /// Describes the result in a short French message.
    /// </summary>
    /// <returns>A message such as "Annulé : 3 champs vidés, 1 laissé car modifié depuis.".</returns>
    public string Describe()
    {
        var parts = new List<string>(capacity: 3);
        if (EmptiedCount > 0)
        {
            parts.Add(Count(EmptiedCount, "champ vidé", "champs vidés"));
        }

        if (ChangedCount > 0)
        {
            parts.Add(Count(ChangedCount, "laissé car modifié depuis", "laissés car modifiés depuis"));
        }

        if (FailedCount > 0)
        {
            parts.Add(Count(FailedCount, "impossible à vider", "impossibles à vider"));
        }

        if (parts.Count == 0)
        {
            return "Rien à annuler.";
        }

        var message = $"Annulé : {string.Join(", ", parts)}.";
        return EmptiedCount == 0 && ChangedCount == 0
            ? $"{message}\nLe formulaire a sans doute été fermé."
            : message;
    }

    private static string Count(int count, string singular, string plural)
    {
        return $"{count} {(count == 1 ? singular : plural)}";
    }
}
