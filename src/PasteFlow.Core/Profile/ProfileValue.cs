using System.Text.Json.Serialization;

namespace PasteFlow.Core.Profile;

/// <summary>
/// An information of the profile of the user, such as their email or their IBAN.
/// </summary>
/// <remarks>A topic can have several values: a personal email and a school email, for example.</remarks>
public sealed record ProfileValue
{
    private const int VisibleEndLength = 4;

    /// <summary>
    /// Gets the identifier of the value, which never changes, even when the value is corrected.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets what the value is about.
    /// </summary>
    public required ProfileTopic Topic { get; init; }

    /// <summary>
    /// Gets the value.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the name the user gave to the value, such as "E-mail de l'IUT", or an empty string.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets where the value comes from.
    /// </summary>
    public ProfileOrigin Origin { get; init; } = ProfileOrigin.Manual;

    /// <summary>
    /// Gets the moment the value was added to the profile.
    /// </summary>
    public DateTimeOffset AddedAt { get; init; }

    /// <summary>
    /// Gets the number of times PasteFlow or the user wrote the value into a field.
    /// </summary>
    public int UseCount { get; init; }

    /// <summary>
    /// Gets the sites where the value was written, see <see cref="SiteUses"/>.
    /// </summary>
    public string Sites { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the value is sensitive, by its topic or by its format: an IBAN typed as
    /// "Autre" is sensitive all the same.
    /// </summary>
    [JsonIgnore]
    public bool IsSensitive => ProfileTopics.IsSensitive(Topic) || SensitiveFormats.IsSensitiveValue(Value);

    /// <summary>
    /// Gets the value as it can be shown on screen: whole if it is ordinary, only its last characters if it is sensitive.
    /// </summary>
    [JsonIgnore]
    public string ShownValue
    {
        get
        {
            if (!IsSensitive)
            {
                return Value;
            }

            var compact = SensitiveFormats.Compact(Value);
            return compact.Length <= VisibleEndLength * 2 ? "••••" : $"•••• {compact[^VisibleEndLength..]}";
        }
    }

    /// <summary>
    /// Gets the name shown for the value: the one the user gave, or else the name of its topic.
    /// </summary>
    [JsonIgnore]
    public string ShownLabel => Label.Length > 0 ? Label : ProfileTopics.DisplayName(Topic);
}
