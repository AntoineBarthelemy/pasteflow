using PasteFlow.Core.Reference;

namespace PasteFlow.Core.Forms;

/// <summary>
/// Rules that decide which fields of a form the application may fill.
/// </summary>
public static class FillRules
{
    // A search box asks what the user wants to find, never a value of the screen
    private static readonly HashSet<string> SearchWords = new(StringComparer.Ordinal)
    {
        "SEARCH", "RECHERCHE", "RECHERCHER", "RECHERCHEZ", "CHERCHER", "CHERCHEZ", "FIND", "FILTER", "FILTRER",
    };

    /// <summary>
    /// Determines whether a field may be filled automatically.
    /// </summary>
    /// <param name="field">The field to check.</param>
    /// <returns>
    /// <see langword="true"/> for empty text fields; <see langword="false"/> for fields already filled by the user,
    /// password fields, lists of choices and search boxes.
    /// </returns>
    public static bool CanFill(FormField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return field.Kind == FieldKind.Text && !field.HasValue && !IsSearchBox(field);
    }

    /// <summary>
    /// Determines whether a field is a search box, such as "Search across your channel" or "Rechercher".
    /// </summary>
    /// <param name="field">The field to check.</param>
    /// <returns><see langword="true"/> if the label, technical name or help text talks about searching.</returns>
    public static bool IsSearchBox(FormField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        var description = ReferenceText.Normalize($"{field.Label} {field.TechnicalName} {field.Hint}");
        return description.Split(' ').Any(SearchWords.Contains);
    }
}
