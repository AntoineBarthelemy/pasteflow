using System.IO.Compression;

namespace PasteFlow.Core.Reference;

/// <summary>
/// Official lists the application checks names against: French first names, communes with their postal codes,
/// and countries.
/// </summary>
/// <remarks>
/// <para>Sources: INSEE file of first names (names given at least 500 times since 1900), official base of postal
/// codes of La Poste, lists of countries in French and English. All names are compared in the form given by
/// <see cref="ReferenceText.Normalize"/>.</para>
/// <para>The embedded lists are loaded once, on first use.</para>
/// </remarks>
public sealed class ReferenceData
{
    private const string ResourcePrefix = "PasteFlow.Core.Data.";

    private static readonly Lazy<ReferenceData> SharedData = new(LoadEmbedded, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly HashSet<string> _firstNames;
    private readonly HashSet<string> _countries;
    private readonly HashSet<string> _communes;
    private readonly Dictionary<string, HashSet<string>> _communesByPostalCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceData"/> class from lists of names.
    /// </summary>
    /// <param name="firstNames">First names, in any form.</param>
    /// <param name="postalCodesAndCommunes">Lines "postal code;commune", the commune in any form.</param>
    /// <param name="countries">Country names, in any form.</param>
    public ReferenceData(IEnumerable<string> firstNames, IEnumerable<string> postalCodesAndCommunes, IEnumerable<string> countries)
    {
        ArgumentNullException.ThrowIfNull(firstNames);
        ArgumentNullException.ThrowIfNull(postalCodesAndCommunes);
        ArgumentNullException.ThrowIfNull(countries);

        _firstNames = firstNames.Select(ReferenceText.Normalize).Where(name => name.Length > 0).ToHashSet(StringComparer.Ordinal);
        _countries = countries.Select(ReferenceText.Normalize).Where(name => name.Length > 0).ToHashSet(StringComparer.Ordinal);
        _communes = new HashSet<string>(StringComparer.Ordinal);
        _communesByPostalCode = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var line in postalCodesAndCommunes)
        {
            var separator = line.IndexOf(';', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var postalCode = line[..separator].Trim();
            var commune = ReferenceText.Normalize(line[(separator + 1)..]);
            if (commune.Length == 0)
            {
                continue;
            }

            if (!_communesByPostalCode.TryGetValue(postalCode, out var communes))
            {
                communes = new HashSet<string>(StringComparer.Ordinal);
                _communesByPostalCode[postalCode] = communes;
            }

            // La Poste names the districts of Paris, Lyon and Marseille "PARIS 10": addresses just say "Paris"
            foreach (var name in new[] { commune, WithoutDistrictNumber(commune) })
            {
                _ = _communes.Add(name);
                _ = communes.Add(name);
            }
        }
    }

    /// <summary>
    /// Gets the lists embedded in the application.
    /// </summary>
    public static ReferenceData Shared => SharedData.Value;

    /// <summary>
    /// Gets lists without any name, for code that must not recognize anything.
    /// </summary>
    public static ReferenceData Empty { get; } = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    /// <summary>
    /// Determines whether a word is a known first name, such as "Antoine" or "Jean-Pierre".
    /// </summary>
    /// <param name="name">The word, in any case, with or without accents.</param>
    /// <returns><see langword="true"/> if it is a known first name; otherwise <see langword="false"/>.</returns>
    public bool IsFirstName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _firstNames.Contains(ReferenceText.Normalize(name));
    }

    /// <summary>
    /// Determines whether a name is a known country, in French or English.
    /// </summary>
    /// <param name="name">The name, in any case, with or without accents.</param>
    /// <returns><see langword="true"/> if it is a known country; otherwise <see langword="false"/>.</returns>
    public bool IsCountry(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _countries.Contains(ReferenceText.Normalize(name));
    }

    /// <summary>
    /// Determines whether a name is a French commune or locality, "Cedex" mentions aside.
    /// </summary>
    /// <param name="name">The name, in any case, with or without accents.</param>
    /// <returns><see langword="true"/> if it is a known commune; otherwise <see langword="false"/>.</returns>
    public bool IsCommune(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _communes.Contains(CommuneKey(name));
    }

    /// <summary>
    /// Determines whether a postal code exists.
    /// </summary>
    /// <param name="postalCode">The postal code, five digits.</param>
    /// <returns><see langword="true"/> if La Poste knows the postal code; otherwise <see langword="false"/>.</returns>
    public bool IsPostalCode(string postalCode)
    {
        ArgumentNullException.ThrowIfNull(postalCode);

        return _communesByPostalCode.ContainsKey(postalCode.Trim());
    }

    /// <summary>
    /// Determines whether a commune is served by a postal code, "Cedex" mentions aside.
    /// </summary>
    /// <param name="postalCode">The postal code, five digits.</param>
    /// <param name="name">The name of the commune, in any case, with or without accents.</param>
    /// <returns><see langword="true"/> if the commune has this postal code; otherwise <see langword="false"/>.</returns>
    public bool IsCommuneOf(string postalCode, string name)
    {
        ArgumentNullException.ThrowIfNull(postalCode);
        ArgumentNullException.ThrowIfNull(name);

        return _communesByPostalCode.TryGetValue(postalCode.Trim(), out var communes) && communes.Contains(CommuneKey(name));
    }

    /// <summary>
    /// Normalizes the name of a commune, without the "Cedex" mention of business addresses.
    /// </summary>
    /// <param name="name">The name, such as "REIMS CEDEX 2".</param>
    /// <returns>The comparable form, such as "REIMS".</returns>
    private static string CommuneKey(string name)
    {
        var normalized = ReferenceText.Normalize(name);
        var cedex = normalized.IndexOf(" CEDEX", StringComparison.Ordinal);
        return cedex > 0 ? normalized[..cedex] : normalized;
    }

    private static string WithoutDistrictNumber(string commune)
    {
        var lastSpace = commune.LastIndexOf(' ');
        return lastSpace > 0 && commune[(lastSpace + 1)..].All(char.IsAsciiDigit) ? commune[..lastSpace] : commune;
    }

    private static ReferenceData LoadEmbedded()
    {
        return new ReferenceData(ReadLines("firstnames.txt.gz"), ReadLines("communes.txt.gz"), ReadLines("countries.txt.gz"));
    }

    private static List<string> ReadLines(string fileName)
    {
        using var stream = typeof(ReferenceData).Assembly.GetManifestResourceStream(ResourcePrefix + fileName);
        if (stream is null)
        {
            // A build without the lists still works, it just recognizes fewer values
            return new List<string>();
        }

        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }

        return lines;
    }
}
