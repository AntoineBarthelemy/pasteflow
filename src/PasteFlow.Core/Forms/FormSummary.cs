using System.Globalization;

namespace PasteFlow.Core.Forms;

/// <summary>
/// Builds the short message that describes the fields found in a form.
/// </summary>
public static class FormSummary
{
    /// <summary>
    /// The maximum number of fields listed by name in the message.
    /// </summary>
    public const int MaxListedFields = 5;

    /// <summary>
    /// Describes the fields found in a form, in French.
    /// </summary>
    /// <param name="fields">The fields found.</param>
    /// <param name="elapsed">The time spent reading the form.</param>
    /// <returns>A message such as "2 champs trouvés : Nom (vide), Email (rempli). Lu en 120 ms.".</returns>
    public static string Describe(IReadOnlyList<FormField> fields, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var duration = string.Create(CultureInfo.InvariantCulture, $"Lu en {Math.Round(elapsed.TotalMilliseconds)} ms.");
        if (fields.Count == 0)
        {
            return $"Aucun champ de formulaire trouvé dans cette fenêtre. {duration}";
        }

        var title = fields.Count == 1 ? "1 champ trouvé" : $"{fields.Count} champs trouvés";
        var listed = fields.Take(MaxListedFields).Select(DescribeField);
        var hidden = fields.Count - MaxListedFields;
        var more = hidden > 0 ? $" et {hidden} autre(s)" : string.Empty;

        return $"{title} : {string.Join(", ", listed)}{more}. {duration}";
    }

    private static string DescribeField(FormField field)
    {
        var label = string.IsNullOrWhiteSpace(field.Label) ? "sans nom" : field.Label;
        var state = field.Kind switch
        {
            FieldKind.Password => "mot de passe",
            _ when field.HasValue => "rempli",
            _ => "vide",
        };

        return $"{label} ({state})";
    }
}
