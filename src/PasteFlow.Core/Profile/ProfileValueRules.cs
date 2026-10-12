using PasteFlow.Core.Candidates;

namespace PasteFlow.Core.Profile;

/// <summary>
/// The rules a value must follow to enter the profile, and how two values are recognized as the same.
/// </summary>
public static class ProfileValueRules
{
    /// <summary>
    /// The maximum length of a value of the profile.
    /// </summary>
    public const int MaxValueLength = 200;

    // Topics written as a block of characters, where spaces, dots and dashes only group them
    private static readonly HashSet<ProfileTopic> CompactTopics = new()
    {
        ProfileTopic.Phone, ProfileTopic.PostalCode, ProfileTopic.Iban, ProfileTopic.Bic, ProfileTopic.SocialSecurityNumber,
        ProfileTopic.PassportNumber, ProfileTopic.IdentityCardNumber, ProfileTopic.DrivingLicenceNumber,
    };

    /// <summary>
    /// Checks that a value can enter the profile under a topic.
    /// </summary>
    /// <param name="topic">The topic chosen.</param>
    /// <param name="value">The value.</param>
    /// <returns><see cref="ProfileEditResult.Done"/> if the value is accepted; otherwise the reason it is refused.</returns>
    public static ProfileEditResult Check(ProfileTopic topic, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return ProfileEditResult.Empty;
        }

        if (trimmed.Length > MaxValueLength)
        {
            return ProfileEditResult.TooLong;
        }

        var isValid = topic switch
        {
            ProfileTopic.Email => ValueValidator.IsValid(CandidateKind.Email, trimmed),
            ProfileTopic.Iban => SensitiveFormats.IsIban(trimmed),
            ProfileTopic.SocialSecurityNumber => SensitiveFormats.IsFrenchSocialSecurityNumber(trimmed),
            _ => true,
        };
        if (!isValid)
        {
            return ProfileEditResult.InvalidFormat;
        }

        // Payment cards stay with the browser or the password manager, whatever the topic chosen
        return SensitiveFormats.IsPaymentCardNumber(trimmed) ? ProfileEditResult.PaymentCardRefused : ProfileEditResult.Done;
    }

    /// <summary>
    /// Determines whether two values of a topic are the same information, written differently:
    /// "FR76 3000 6000..." and "FR7630006000...", or "antoine@orange.fr" and "Antoine@Orange.fr".
    /// </summary>
    /// <param name="topic">The topic of both values.</param>
    /// <param name="first">The first value.</param>
    /// <param name="second">The second value.</param>
    /// <returns><see langword="true"/> if both values are the same information; otherwise <see langword="false"/>.</returns>
    public static bool AreSame(ProfileTopic topic, string first, string second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return string.Equals(Key(topic, first), Key(topic, second), StringComparison.OrdinalIgnoreCase);
    }

    private static string Key(ProfileTopic topic, string value)
    {
        return CompactTopics.Contains(topic)
            ? SensitiveFormats.Compact(value)
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
