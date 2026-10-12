using System.Text.Json;
using PasteFlow.Core.Profile;

namespace PasteFlow.Core.Logins;

/// <summary>
/// Reads the answers of the Bitwarden command line (bw), given as JSON.
/// </summary>
/// <remarks>
/// The passwords that Bitwarden puts in its lists are dropped at once: an account is kept without its password, which
/// is asked for alone, at the moment it is written. Accounts that ask for the master password again
/// ("reprompt") are left out, as the user asked Bitwarden.
/// </remarks>
public static class BitwardenJson
{
    private const int LoginType = 1;
    private const int IdentityType = 4;

    private static readonly (string Property, ProfileTopic Topic)[] IdentityTopics =
    {
        ("firstName", ProfileTopic.FirstName),
        ("lastName", ProfileTopic.LastName),
        ("email", ProfileTopic.Email),
        ("phone", ProfileTopic.Phone),
        ("address1", ProfileTopic.Street),
        ("postalCode", ProfileTopic.PostalCode),
        ("city", ProfileTopic.City),
        ("country", ProfileTopic.Country),
        ("company", ProfileTopic.Company),
        ("ssn", ProfileTopic.SocialSecurityNumber),
        ("passportNumber", ProfileTopic.PassportNumber),
        ("licenseNumber", ProfileTopic.DrivingLicenceNumber),
    };

    /// <summary>
    /// Reads the state of the vault from the answer of "bw status".
    /// </summary>
    /// <param name="json">The answer.</param>
    /// <returns>"unlocked", "locked" or "unauthenticated", or an empty string if the answer cannot be read.</returns>
    public static string Status(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("status", out var status)
                && status.ValueKind == JsonValueKind.String
                ? status.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Reads the accounts from the answer of "bw list items".
    /// </summary>
    /// <param name="json">The answer.</param>
    /// <returns>The accounts, without their passwords.</returns>
    public static IReadOnlyList<VaultLogin> Logins(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var logins = new List<VaultLogin>();
        foreach (var item in Items(json, LoginType))
        {
            if (!item.TryGetProperty("login", out var login) || login.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var uris = login.TryGetProperty("uris", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Select(uri => Text(uri, "uri")).Where(uri => uri.Length > 0).ToList()
                : new List<string>();
            logins.Add(new VaultLogin { Id = Text(item, "id"), Name = Text(item, "name"), UserName = Text(login, "username"), Uris = uris });
        }

        return logins.Where(login => login.Id.Length > 0).ToList();
    }

    /// <summary>
    /// Reads the identity of the user from the answer of "bw list items".
    /// </summary>
    /// <param name="json">The answer.</param>
    /// <returns>The values of the identity, or an empty list when the vault holds none, or several: PasteFlow cannot tell which one is the user's own.</returns>
    public static IReadOnlyList<ProfileValue> Identity(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var identities = Items(json, IdentityType)
            .Select(item => item.TryGetProperty("identity", out var identity) && identity.ValueKind == JsonValueKind.Object ? identity : default)
            .Where(identity => identity.ValueKind == JsonValueKind.Object)
            .ToList();
        if (identities.Count != 1)
        {
            return Array.Empty<ProfileValue>();
        }

        return IdentityTopics
            .Select(pair => (pair.Topic, Value: Text(identities[0], pair.Property).Trim()))
            .Where(pair => pair.Value.Length > 0)
            .Select(pair => new ProfileValue { Topic = pair.Topic, Value = pair.Value, Origin = ProfileOrigin.PasswordManager })
            .ToList();
    }

    private static List<JsonElement> Items(string json, int type)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new List<JsonElement>();
            }

            // Cloned, so that the elements outlive the document
            return document.RootElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("type", out var itemType) && itemType.ValueKind == JsonValueKind.Number
                    && itemType.TryGetInt32(out var itemTypeValue) && itemTypeValue == type
                    && !(item.TryGetProperty("reprompt", out var reprompt) && reprompt.ValueKind == JsonValueKind.Number
                        && (!reprompt.TryGetInt32(out var repromptValue) || repromptValue != 0)))
                .Select(item => item.Clone())
                .ToList();
        }
        catch (JsonException)
        {
            return new List<JsonElement>();
        }
    }

    private static string Text(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }
}
