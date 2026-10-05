namespace PasteFlow.Core.Forms;

/// <summary>
/// Rules that decide which fields of a form the application may fill.
/// </summary>
public static class FillRules
{
    /// <summary>
    /// Determines whether a field may be filled automatically.
    /// </summary>
    /// <param name="field">The field to check.</param>
    /// <returns>
    /// <see langword="true"/> for empty text fields; <see langword="false"/> for fields already filled by the user,
    /// password fields and lists of choices.
    /// </returns>
    public static bool CanFill(FormField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return field.Kind == FieldKind.Text && !field.HasValue;
    }
}
