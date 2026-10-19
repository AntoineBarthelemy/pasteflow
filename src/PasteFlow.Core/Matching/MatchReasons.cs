using PasteFlow.Core.Candidates;
using PasteFlow.Core.Profile;

namespace PasteFlow.Core.Matching;

/// <summary>
/// The sentences that tell the user why a field was left to them, such as "51500 ne correspond pas à Reims selon
/// La Poste.".
/// </summary>
/// <remarks>
/// A sensitive value, such as an IBAN, is never quoted whole: only its last characters are shown.
/// </remarks>
public static class MatchReasons
{
    /// <summary>
    /// The maximum length of a value quoted in a sentence.
    /// </summary>
    public const int MaxQuotedLength = 30;

    /// <summary>
    /// The sentence when no value at all fits the field.
    /// </summary>
    public const string NothingFound = "Aucune valeur trouvée pour ce champ.";

    /// <summary>
    /// The sentence when values were found, but none looks enough like what the field expects.
    /// </summary>
    public const string NothingClose = "Aucune valeur ne ressemble assez à ce que ce champ attend.";

    /// <summary>
    /// The sentence when the field refused the value written.
    /// </summary>
    public const string WriteRefused = "Le champ a refusé la valeur.";

    /// <summary>
    /// The sentence when sensitive values were left out because the user did not prove it is them.
    /// </summary>
    public const string SensitiveClosed = "Valeur sensible : il faut d'abord valider Windows Hello.";

    /// <summary>
    /// Gets the sentence for a value likely but not sure enough to be written alone.
    /// </summary>
    /// <param name="candidate">The value.</param>
    /// <returns>The sentence.</returns>
    public static string Unsure(Candidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return $"{Quote(candidate)} est possible, mais rien ne la relie clairement à ce champ.";
    }

    /// <summary>
    /// Gets the sentence for two values too close to choose between.
    /// </summary>
    /// <param name="first">The best value.</param>
    /// <param name="second">The second best value.</param>
    /// <returns>The sentence.</returns>
    public static string Hesitation(Candidate first, Candidate second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return $"Deux valeurs aussi probables : {Quote(first)} et {Quote(second)}.";
    }

    /// <summary>
    /// Gets the sentence for a postal code and a commune that do not go together.
    /// </summary>
    /// <param name="postalCode">The postal code.</param>
    /// <param name="commune">The commune.</param>
    /// <returns>The sentence.</returns>
    public static string PostalCodeAndCommune(string postalCode, string commune)
    {
        ArgumentNullException.ThrowIfNull(postalCode);
        ArgumentNullException.ThrowIfNull(commune);

        return $"{Shorten(postalCode.Trim())} ne correspond pas à {Shorten(commune.Trim())} selon La Poste.";
    }

    /// <summary>
    /// Gets the sentence for a value read from the image of a window.
    /// </summary>
    /// <param name="candidate">The value.</param>
    /// <returns>The sentence.</returns>
    public static string ReadFromImage(Candidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return $"{Quote(candidate)} a été lue dans une image : un caractère a pu être mal lu.";
    }

    /// <summary>
    /// Gets the sentence for a profile and a screen that give two different values.
    /// </summary>
    /// <param name="profileValue">The value of the profile.</param>
    /// <param name="screenValue">The value of the screen.</param>
    /// <returns>The sentence.</returns>
    public static string ProfileAndScreenDiffer(Candidate profileValue, Candidate screenValue)
    {
        ArgumentNullException.ThrowIfNull(profileValue);
        ArgumentNullException.ThrowIfNull(screenValue);

        return $"Ta fiche donne {Quote(profileValue)}, l'écran donne {Quote(screenValue)}.";
    }

    /// <summary>
    /// Gets the sentence for a profile that holds several values for the field, none used more on this site.
    /// </summary>
    /// <param name="first">A value of the profile.</param>
    /// <param name="second">Another value of the profile.</param>
    /// <returns>The sentence.</returns>
    public static string SeveralProfileValues(Candidate first, Candidate second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return $"Ta fiche a plusieurs valeurs : {Quote(first)} et {Quote(second)}.";
    }

    /// <summary>
    /// Gets a value as it can be quoted to the user, between French quotes: shortened if long, only its last
    /// characters if it is sensitive.
    /// </summary>
    /// <param name="candidate">The value.</param>
    /// <returns>The quoted value.</returns>
    public static string Quote(Candidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var shown = candidate.IsSensitive
            ? ProfileValue.Masked(candidate.Value)
            : Shorten(candidate.Value.Trim());
        return $"« {shown} »";
    }

    private static string Shorten(string value)
    {
        return value.Length <= MaxQuotedLength ? value : $"{value[..(MaxQuotedLength - 1)]}…";
    }
}
