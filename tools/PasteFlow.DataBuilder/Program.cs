using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace PasteFlow.DataBuilder;

/// <summary>
/// Downloads the official open data and writes the compact lists embedded in PasteFlow.Core.
/// </summary>
/// <remarks>
/// Sources, all under Licence Ouverte 2.0 or MIT: INSEE file of first names, La Poste official base of postal codes,
/// lists of countries of the umpirsky/country-list project.
/// </remarks>
internal static class Program
{
    private const string FirstNamesUrl = "https://www.insee.fr/fr/statistiques/fichier/8595130/prenoms-2025-nat_csv.zip";
    private const string CommunesUrl = "https://data.laposte.fr/data-fair/api/v1/datasets/laposte-hexasmal/raw";
    private const string CountriesBaseUrl = "https://raw.githubusercontent.com/umpirsky/country-list/master/data";

    private static readonly string[] CountryLanguages = { "fr", "en" };

    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 1)
        {
            await Console.Error.WriteLineAsync("Usage: PasteFlow.DataBuilder <output folder>");
            return 1;
        }

        var outputFolder = Directory.CreateDirectory(args[0]).FullName;
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("PasteFlow-DataBuilder/1.0");

        var firstNames = ReferenceLists.FirstNames(CsvTable.Parse(await DownloadZippedCsvAsync(http, FirstNamesUrl)));
        await WriteAsync(Path.Combine(outputFolder, "firstnames.txt.gz"), firstNames);

        var communes = ReferenceLists.Communes(CsvTable.Parse(await http.GetStringAsync(new Uri(CommunesUrl))));
        await WriteAsync(Path.Combine(outputFolder, "communes.txt.gz"), communes);

        var countryLists = new List<string>();
        foreach (var language in CountryLanguages)
        {
            countryLists.Add(await http.GetStringAsync(new Uri($"{CountriesBaseUrl}/{language}/country.json")));
        }

        var countries = ReferenceLists.Countries(countryLists);
        await WriteAsync(Path.Combine(outputFolder, "countries.txt.gz"), countries);

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{firstNames.Count} first names, {communes.Count} postal code and place pairs, {countries.Count} countries"));
        return 0;
    }

    private static async Task<string> DownloadZippedCsvAsync(HttpClient http, string url)
    {
        await using var download = await http.GetStreamAsync(new Uri(url));
        using var buffer = new MemoryStream();
        await download.CopyToAsync(buffer);
        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
        var entry = archive.Entries.FirstOrDefault(item => item.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"No CSV file in {url}");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static async Task WriteAsync(string path, IReadOnlyList<string> lines)
    {
        // Gzip: the lists are embedded in the application, they must stay small
        await using var file = File.Create(path);
        await using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
        await using var writer = new StreamWriter(gzip, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        foreach (var line in lines)
        {
            await writer.WriteAsync(line);
            await writer.WriteAsync('\n');
        }
    }
}
