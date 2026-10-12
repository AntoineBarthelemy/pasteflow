using PasteFlow.Core.Profile;

namespace PasteFlow.App.Profile;

/// <summary>
/// The sentences that tell the user what became of their changes to the profile.
/// </summary>
internal static class ProfileMessages
{
    /// <summary>
    /// Gets the sentence that tells what became of a change.
    /// </summary>
    /// <param name="result">What became of the change.</param>
    /// <returns>The sentence, in French.</returns>
    public static string For(ProfileEditResult result)
    {
        return result switch
        {
            ProfileEditResult.Done => "C'est enregistré.",
            ProfileEditResult.NotSaved => "C'est pris en compte, mais pas enregistré : ce sera perdu à la fermeture de PasteFlow.",
            ProfileEditResult.AlreadyThere => "Cette info est déjà dans ta fiche.",
            ProfileEditResult.Empty => "Écris d'abord la valeur.",
            ProfileEditResult.TooLong => $"Cette valeur est trop longue ({ProfileValueRules.MaxValueLength} caractères au plus).",
            ProfileEditResult.InvalidFormat => "Cette valeur n'a pas le bon format : vérifie-la (une erreur de frappe dans un IBAN ou un n° de sécu se voit à sa clé).",
            ProfileEditResult.PaymentCardRefused => "PasteFlow ne garde jamais de numéro de carte bancaire : laisse-le à ton navigateur ou à Bitwarden.",
            ProfileEditResult.NotFound => "Cette info n'est plus dans ta fiche.",
            ProfileEditResult.Full => $"Ta fiche est pleine ({ProfileBook.MaxValues} infos au plus) : supprimes-en une d'abord.",
            _ => "Quelque chose s'est mal passé.",
        };
    }

    /// <summary>
    /// Gets the sentence that tells what came of the reading of an export.
    /// </summary>
    /// <param name="status">What came of the reading.</param>
    /// <returns>The sentence, in French.</returns>
    public static string For(ProfileArchiveStatus status)
    {
        return status switch
        {
            ProfileArchiveStatus.WrongPassword => "Mot de passe incorrect, ou fichier modifié depuis l'export.",
            ProfileArchiveStatus.NotAnArchive => "Ce fichier n'est pas un export de fiche PasteFlow.",
            _ => "Fiche lue.",
        };
    }

    /// <summary>
    /// Gets the sentence that tells what an import brought.
    /// </summary>
    /// <param name="summary">What the import brought.</param>
    /// <returns>The sentence, in French.</returns>
    public static string For(ProfileImportSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var added = summary.Added switch
        {
            0 => "Rien de nouveau : tout était déjà dans ta fiche.",
            1 => "1 info ajoutée à ta fiche.",
            _ => $"{summary.Added} infos ajoutées à ta fiche.",
        };
        return summary.IsSaved ? added : $"{added} Attention, elles n'ont pas pu être enregistrées.";
    }
}
