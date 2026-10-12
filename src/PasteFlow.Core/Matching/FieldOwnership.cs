using PasteFlow.Core.Forms;
using PasteFlow.Core.Reference;

namespace PasteFlow.Core.Matching;

/// <summary>
/// Tells whom a field is about, first from its own words, then from the words of the page when the field says
/// nothing: "Votre e-mail" is about the user, "E-mail du contact" about someone else, and a plain "E-mail" depends
/// on whether the page is "Créer un compte" or "Nouveau client".
/// </summary>
/// <remarks>When the words disagree, nobody is assumed: the profile then never overrides the screen.</remarks>
public static class FieldOwnership
{
    private static readonly HashSet<string> UserWords = new(StringComparer.Ordinal)
    {
        "VOTRE", "VOS", "TON", "TES", "MON", "MES", "MA", "TA", "MOI", "YOUR", "MY",
    };

    private static readonly HashSet<string> SomeoneElseWords = new(StringComparer.Ordinal)
    {
        "CLIENT", "CLIENTS", "CONTACT", "DESTINATAIRE", "DESTINATAIRES", "RECIPIENT", "BENEFICIAIRE", "BENEFICIARY",
        "INVITE", "GUEST", "PATIENT", "ELEVE", "FILLEUL", "PARRAIN", "CONJOINT", "ENFANT", "SALARIE", "EMPLOYEE",
        "SON", "SA", "SES", "LEUR", "LEURS", "HIS", "HER", "THEIR",
    };

    // Written as ReferenceText.Normalize writes them: upper case, without accents, punctuation turned into spaces
    private static readonly string[] UserPagePhrases =
    {
        "CREER UN COMPTE", "CREER MON COMPTE", "CREATION DE COMPTE", "CREATION DU COMPTE", "INSCRIPTION", "S INSCRIRE",
        "INSCRIVEZ VOUS", "CONNEXION", "SE CONNECTER", "CONNECTEZ VOUS", "IDENTIFIEZ VOUS", "IDENTIFICATION",
        "CANDIDATURE", "POSTULER", "MON COMPTE", "MON PROFIL", "MES INFORMATIONS", "SIGN UP", "SIGN IN", "LOG IN",
        "LOGIN", "REGISTER", "CREATE ACCOUNT", "CREATE AN ACCOUNT", "MY ACCOUNT", "MY PROFILE",
    };

    private static readonly string[] SomeoneElsePagePhrases =
    {
        "NOUVEAU CLIENT", "AJOUTER UN CLIENT", "CREER UN CLIENT", "FICHE CLIENT", "NOUVEAU CONTACT", "AJOUTER UN CONTACT",
        "CREER UN CONTACT", "NOUVEAU PATIENT", "AJOUTER UN PATIENT", "NEW CONTACT", "ADD CONTACT", "ADD A CONTACT",
        "NEW CUSTOMER", "ADD CUSTOMER", "NEW CLIENT",
    };

    /// <summary>
    /// Tells whom a field is about.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="page">What the window being filled tells.</param>
    /// <returns>The user, someone else, or unknown.</returns>
    public static FieldOwner Of(FormField field, PageContext page)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(page);

        // The technical name is left out: "customer_email" is the buyer's own email on a checkout page
        var words = ReferenceText.Normalize($"{field.Label} {field.Hint}").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var fieldOwner = OwnerOf(words.Any(UserWords.Contains), words.Any(SomeoneElseWords.Contains));
        if (fieldOwner != FieldOwner.Unknown || words.Any(word => UserWords.Contains(word) || SomeoneElseWords.Contains(word)))
        {
            return fieldOwner;
        }

        var pageText = $" {ReferenceText.Normalize($"{page.Title}\n{page.Text}")} ";
        return OwnerOf(
            UserPagePhrases.Any(phrase => pageText.Contains($" {phrase} ", StringComparison.Ordinal)),
            SomeoneElsePagePhrases.Any(phrase => pageText.Contains($" {phrase} ", StringComparison.Ordinal)));
    }

    private static FieldOwner OwnerOf(bool isAboutUser, bool isAboutSomeoneElse)
    {
        if (isAboutUser == isAboutSomeoneElse)
        {
            return FieldOwner.Unknown;
        }

        return isAboutUser ? FieldOwner.User : FieldOwner.SomeoneElse;
    }
}
