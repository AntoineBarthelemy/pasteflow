namespace PasteFlow.Core.Screen;

/// <summary>
/// Reads the address of a web page as a browser shows it, such as "github.com/AntoineBarthelemy" or
/// "https://www.univ-reims.fr/", and tells which site it belongs to.
/// </summary>
public static class PageAddress
{
    // Suffixes made of two labels, under which each site has its own name: "service-public.gouv.fr" is its own site,
    // and so is each page hosted on "github.io" or "netlify.app", which belong to different people
    private static readonly HashSet<string> TwoLabelSuffixes = new(StringComparer.Ordinal)
    {
        "gouv.fr", "asso.fr", "co.uk", "org.uk", "ac.uk", "gov.uk", "com.au", "co.jp", "co.nz", "com.br", "co.za",
        "github.io", "gitlab.io", "netlify.app", "vercel.app", "pages.dev", "web.app", "firebaseapp.com",
        "herokuapp.com", "azurewebsites.net", "blogspot.com", "wixsite.com",
    };

    /// <summary>
    /// Gets the host of an address, without "www.": "github.com" for "https://www.github.com/login".
    /// </summary>
    /// <param name="address">The address, with or without "https://", as the address bar shows it.</param>
    /// <returns>The host in lower case, or an empty string if the text is not the address of a web page.</returns>
    public static string HostOf(string address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var trimmed = address.Trim();
        if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace))
        {
            // A search typed in the address bar, such as "météo reims", is not an address
            return string.Empty;
        }

        var withScheme = trimmed.Contains("://", StringComparison.Ordinal) ? trimmed : "https://" + trimmed;
        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !uri.Host.Contains('.', StringComparison.Ordinal)
            || uri.HostNameType != UriHostNameType.Dns)
        {
            return string.Empty;
        }

        var host = uri.IdnHost.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    /// <summary>
    /// Gets the site a host belongs to: "univ-reims.fr" for "moodle.univ-reims.fr" as for "ent.univ-reims.fr".
    /// </summary>
    /// <param name="host">The host, as given by <see cref="HostOf"/>.</param>
    /// <returns>The site, or an empty string for an empty host.</returns>
    public static string SiteOf(string host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var labels = host.Trim().ToLowerInvariant().Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length <= 2)
        {
            return string.Join('.', labels);
        }

        var lastTwo = $"{labels[^2]}.{labels[^1]}";
        return TwoLabelSuffixes.Contains(lastTwo) ? $"{labels[^3]}.{lastTwo}" : lastTwo;
    }
}
