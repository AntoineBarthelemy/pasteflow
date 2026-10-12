using PasteFlow.Core.Screen;

namespace PasteFlow.Core.Logins;

/// <summary>
/// Tells whether an account saved for an address belongs to the page being filled.
/// </summary>
/// <remarks>
/// Safety net against look-alike sites: the site of the saved address must be exactly the site of the page, as read
/// in the address bar. "github.com" fits "github.com/login" and "gist.github.com", never "github.com.fake.ru" nor
/// "githuh.com".
/// </remarks>
public static class LoginSites
{
    /// <summary>
    /// Determines whether an account is saved for the site of a page.
    /// </summary>
    /// <param name="login">The account.</param>
    /// <param name="host">The host of the page.</param>
    /// <returns><see langword="true"/> if one of the addresses of the account is on the same site.</returns>
    public static bool IsFor(VaultLogin login, string host)
    {
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(host);

        var site = PageAddress.SiteOf(PageAddress.HostOf(host));
        return site.Length > 0 && login.Uris.Any(uri =>
        {
            var uriHost = PageAddress.HostOf(uri);
            return uriHost.Length > 0 && string.Equals(PageAddress.SiteOf(uriHost), site, StringComparison.Ordinal);
        });
    }
}
