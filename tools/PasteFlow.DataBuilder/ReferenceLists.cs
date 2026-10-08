using System.Globalization;
using System.Text.Json;
using PasteFlow.Core.Reference;

namespace PasteFlow.DataBuilder;

/// <summary>
/// Turns the official files into the compact lists embedded in the application.
/// </summary>
internal static class ReferenceLists
{
    /// <summary>
    /// The minimum number of births since 1900 for a first name to be kept: rarer names are mostly noise.
    /// </summary>
    internal const int MinBirths = 500;

    /// <summary>
    /// Builds the list of first names from the INSEE file of first names.
    /// </summary>
    /// <param name="table">The national file: one row per first name, sex and year.</param>
    /// <returns>The normalized first names given at least <see cref="MinBirths"/> times, sorted.</returns>
    public static IReadOnlyList<string> FirstNames(CsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var nameColumn = table.ColumnOf("PRENOM", "PREUSUEL");
        var countColumn = table.ColumnOf("VALEUR", "NOMBRE");
        var births = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var row in table.Rows.Where(row => row.Length > Math.Max(nameColumn, countColumn)))
        {
            // "_PRENOMS_RARES" groups the rare names together
            var name = row[nameColumn];
            if (name.StartsWith('_') || !long.TryParse(row[countColumn], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
            {
                continue;
            }

            var normalized = ReferenceText.Normalize(name);
            if (normalized.Length > 1)
            {
                births[normalized] = births.GetValueOrDefault(normalized) + count;
            }
        }

        return births.Where(entry => entry.Value >= MinBirths).Select(entry => entry.Key).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Builds the list of postal codes and place names from the official base of La Poste.
    /// </summary>
    /// <param name="table">The base: one row per commune and postal code, with its delivery name and locality.</param>
    /// <returns>Lines "postal code;normalized name", sorted, without duplicates.</returns>
    public static IReadOnlyList<string> Communes(CsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var postalCodeColumn = table.ColumnOf("CODE_POSTAL");
        var nameColumns = new[]
        {
            table.ColumnOf("NOM_DE_LA_COMMUNE", "NOM_COMMUNE"),
            table.FindColumn("LIBELLE_D_ACHEMINEMENT", "LIBELLE_ACHEMINEMENT"),
            table.FindColumn("LIGNE_5"),
        }.Where(column => column >= 0).ToArray();

        var lines = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var row in table.Rows)
        {
            if (row.Length <= postalCodeColumn)
            {
                continue;
            }

            var postalCode = row[postalCodeColumn].PadLeft(5, '0');
            foreach (var column in nameColumns.Where(column => column < row.Length))
            {
                var name = ReferenceText.Normalize(row[column]);
                if (name.Length > 0)
                {
                    _ = lines.Add($"{postalCode};{name}");
                }
            }
        }

        return lines.ToList();
    }

    /// <summary>
    /// Builds the list of country names from lists of countries by code, in several languages.
    /// </summary>
    /// <param name="jsonLists">JSON objects such as {"FR": "France", "DE": "Allemagne"}.</param>
    /// <returns>The normalized names, sorted, without duplicates.</returns>
    public static IReadOnlyList<string> Countries(IEnumerable<string> jsonLists)
    {
        ArgumentNullException.ThrowIfNull(jsonLists);

        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var json in jsonLists)
        {
            var countries = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            foreach (var name in countries.Values.Select(ReferenceText.Normalize).Where(name => name.Length > 0))
            {
                _ = names.Add(name);
            }
        }

        return names.ToList();
    }
}
