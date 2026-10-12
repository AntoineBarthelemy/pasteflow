using System.Runtime.InteropServices;
using Microsoft.Win32;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Profile;
using Windows.System;

namespace PasteFlow.App.Profile;

/// <summary>
/// Reads what Windows knows about its user: first name, last name and the email of the Microsoft account.
/// </summary>
/// <remarks>
/// Windows gives the names only when its setting "Autoriser les applications à accéder aux informations de votre
/// compte" allows it; the email of a Microsoft account is also found among the accounts Windows signs in with.
/// Anything that cannot be read is left empty: the profile simply starts with less.
/// </remarks>
internal static class WindowsAccountReader
{
    private const string MicrosoftAccountsKey = @"Software\Microsoft\IdentityCRL\UserExtendedProperties";

    /// <summary>
    /// Reads the account of the user.
    /// </summary>
    /// <returns>What could be read; empty parts for the rest.</returns>
    public static async Task<WindowsAccount> ReadAsync()
    {
        var firstName = string.Empty;
        var lastName = string.Empty;
        var accountName = string.Empty;
        try
        {
            var users = await User.FindAllAsync(UserType.LocalUser, UserAuthenticationStatus.LocallyAuthenticated).AsTask();
            if (users.Count > 0)
            {
                var properties = await users[0].GetPropertiesAsync(new[] { KnownUserProperties.FirstName, KnownUserProperties.LastName, KnownUserProperties.AccountName }).AsTask();
                firstName = properties.TryGetValue(KnownUserProperties.FirstName, out var first) ? first as string ?? string.Empty : string.Empty;
                lastName = properties.TryGetValue(KnownUserProperties.LastName, out var last) ? last as string ?? string.Empty : string.Empty;
                accountName = properties.TryGetValue(KnownUserProperties.AccountName, out var account) ? account as string ?? string.Empty : string.Empty;
            }
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Windows refuses to tell: the profile starts without the names
        }

        var email = ValueValidator.IsValid(CandidateKind.Email, accountName) && accountName.Contains('@', StringComparison.Ordinal)
            ? accountName
            : SingleMicrosoftAccountEmail();
        return new WindowsAccount { FirstName = firstName.Trim(), LastName = lastName.Trim(), Email = email };
    }

    /// <summary>
    /// Finds the email of the Microsoft account Windows signs in with, when there is exactly one.
    /// </summary>
    /// <returns>The email, or an empty string when there is none, or several.</returns>
    internal static string SingleMicrosoftAccountEmail()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(MicrosoftAccountsKey);
            var emails = (key?.GetSubKeyNames() ?? Array.Empty<string>())
                .Where(name => name.Contains('@', StringComparison.Ordinal) && ValueValidator.IsValid(CandidateKind.Email, name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Several accounts: no way to tell which one is the user's own, so none is taken
            return emails.Count == 1 ? emails[0] : string.Empty;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return string.Empty;
        }
    }
}
