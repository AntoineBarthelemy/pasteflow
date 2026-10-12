using System.Text.Json.Serialization;

namespace PasteFlow.Core.Profile;

/// <summary>
/// Where an information of the profile comes from.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProfileOrigin>))]
public enum ProfileOrigin
{
    /// <summary>
    /// Typed by the user in the window of the profile.
    /// </summary>
    Manual,

    /// <summary>
    /// Taken from the Windows account of the user.
    /// </summary>
    WindowsAccount,

    /// <summary>
    /// Taken from an identity of the password manager of the user.
    /// </summary>
    PasswordManager,

    /// <summary>
    /// Learned from the fills of the user.
    /// </summary>
    Learned,

    /// <summary>
    /// Brought from an export of the profile.
    /// </summary>
    Imported,
}
