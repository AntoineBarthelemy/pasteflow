using System.Globalization;
using System.Text;

namespace PasteFlow.Core.Profile;

/// <summary>
/// Keeps, for a value of the profile, the sites where the user wrote it and how often, written as one short text
/// such as "univ-reims.fr=3 github.com=1", the most recent site first.
/// </summary>
public static class SiteUses
{
    /// <summary>
    /// The maximum number of sites kept for a value; the oldest ones are forgotten first.
    /// </summary>
    public const int MaxSites = 30;

    /// <summary>
    /// Reads the sites and their number of uses.
    /// </summary>
    /// <param name="sites">The sites, as kept.</param>
    /// <returns>The number of uses of each site, the most recent first.</returns>
    public static IReadOnlyList<(string Site, int Count)> Read(string sites)
    {
        ArgumentNullException.ThrowIfNull(sites);

        var uses = new List<(string Site, int Count)>();
        foreach (var part in sites.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.LastIndexOf('=');
            if (separator > 0 && int.TryParse(part.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count > 0)
            {
                uses.Add((part[..separator], count));
            }
        }

        return uses;
    }

    /// <summary>
    /// Gets the number of times the value was written on a site.
    /// </summary>
    /// <param name="sites">The sites, as kept.</param>
    /// <param name="site">The site.</param>
    /// <returns>The number of uses, 0 if the value was never written there.</returns>
    public static int CountFor(string sites, string site)
    {
        ArgumentNullException.ThrowIfNull(site);

        return site.Length == 0 ? 0 : Read(sites).Where(use => string.Equals(use.Site, site, StringComparison.Ordinal)).Sum(use => use.Count);
    }

    /// <summary>
    /// Adds a use of the value on a site.
    /// </summary>
    /// <param name="sites">The sites, as kept.</param>
    /// <param name="site">The site where the value was written.</param>
    /// <returns>The sites with this use, the site first.</returns>
    public static string Add(string sites, string site)
    {
        ArgumentNullException.ThrowIfNull(site);

        var clean = site.Trim().ToLowerInvariant();
        if (clean.Length == 0 || clean.Contains(' ', StringComparison.Ordinal) || clean.Contains('=', StringComparison.Ordinal))
        {
            return sites;
        }

        var uses = Read(sites).ToList();
        var count = uses.Where(use => use.Site == clean).Sum(use => use.Count) + 1;
        _ = uses.RemoveAll(use => use.Site == clean);
        uses.Insert(0, (clean, count));

        var builder = new StringBuilder();
        foreach (var use in uses.Take(MaxSites))
        {
            if (builder.Length > 0)
            {
                _ = builder.Append(' ');
            }

            _ = builder.Append(use.Site).Append('=').Append(use.Count.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
