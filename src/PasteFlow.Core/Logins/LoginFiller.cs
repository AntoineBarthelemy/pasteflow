using PasteFlow.Core.Forms;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Reference;
using PasteFlow.Core.Security;

namespace PasteFlow.Core.Logins;

/// <summary>
/// Fills a sign-in form from the password manager: the identifier and the password of the account saved for the site.
/// </summary>
/// <remarks>
/// <para>In a browser, the account must be saved for the very site of the address bar (<see cref="LoginSites"/>); with
/// several accounts, the user chooses. In an application, which has no address, the accounts named like the program
/// are only proposed: the user always chooses.</para>
/// <para>The password is asked for to the manager while Windows Hello checks the user, so that both waits overlap; it is
/// written into the password field only once the user is checked, and never kept, shown or learned. A form with several password fields (creating an account, changing a password) is left alone,
/// and so is a form whose identifier is already there, typed by the user or another tool.</para>
/// </remarks>
public sealed class LoginFiller
{
    // Words of a field that takes the identifier of an account
    private static readonly string[] IdentifierWords =
    {
        "EMAIL", "MAIL", "COURRIEL", "IDENTIFIANT", "IDENTIFIER", "LOGIN", "USERNAME", "UTILISATEUR", "USER", "PSEUDO",
        "COMPTE", "ACCOUNT", "TELEPHONE", "PHONE",
    };

    // Words of a password field that asks for a new password, not the one saved
    private static readonly string[] NewPasswordWords = { "NOUVEAU", "NOUVEL", "NEW", "CONFIRMER", "CONFIRMEZ", "CONFIRM", "CONFIRMATION", "REPETEZ" };

    private static readonly HashSet<string> BrowserPrograms = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi",
    };

    private readonly IPasswordManager _manager;
    private readonly SensitiveGate _gate;
    private readonly ILoginPicker? _picker;
    private readonly IFieldWriter _writer;
    private bool _hasAccounts;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginFiller"/> class.
    /// </summary>
    /// <param name="manager">The password manager.</param>
    /// <param name="gate">The gate the password goes through.</param>
    /// <param name="picker">Lets the user choose among several accounts; without it, nothing is chosen for them.</param>
    /// <param name="writer">Writes into the fields.</param>
    public LoginFiller(IPasswordManager manager, SensitiveGate gate, ILoginPicker? picker, IFieldWriter writer)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(writer);

        _manager = manager;
        _gate = gate;
        _picker = picker;
        _writer = writer;
    }

    /// <summary>
    /// Gets the sentence that tells the vault is locked.
    /// </summary>
    public static string LockedNote { get; } = "Page de connexion : Bitwarden est verrouillé dans PasteFlow. Déverrouille-le (Ctrl+Alt+P puis B), puis refais le raccourci.";

    /// <summary>
    /// Gets a value indicating whether the last form was a sign-in form left to the password manager that it could not
    /// fill: its identifier and its password are then never taken from the profile or the screen (directive of
    /// 12/11/2026), the user is told why instead.
    /// </summary>
    public bool IsLeftToManager { get; private set; }

    /// <summary>
    /// Gets why the last sign-in form was not filled, in a sentence for the user, or an empty string when the user
    /// cancelled the choice of the account.
    /// </summary>
    public string UnfilledNote { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the sentence that tells no account of the vault fits the page.
    /// </summary>
    /// <param name="page">The page being filled.</param>
    /// <returns>The sentence, in French.</returns>
    public static string NoAccountNoteFor(PageContext page)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (page.Host.Length > 0)
        {
            return $"Page de connexion : aucun compte Bitwarden n'est enregistré pour {page.Host}. Ajoute cette adresse à ton entrée dans Bitwarden, puis refais le raccourci.";
        }

        return BrowserPrograms.Contains(page.Program)
            ? "Page de connexion : PasteFlow n'a pas pu lire l'adresse de la page, il ne sait pas quel compte Bitwarden proposer."
            : "Page de connexion : aucun compte Bitwarden ne porte le nom de ce programme.";
    }

    /// <summary>
    /// Finds the fields of a sign-in form: its only password field and the identifier field just before it.
    /// </summary>
    /// <param name="fields">The fields of the form, in their order.</param>
    /// <returns>The identifier field (or <see langword="null"/>) and the password field, or <see langword="null"/> if the form does not sign in.</returns>
    public static (FormField? Identifier, FormField Password)? SignInFields(IReadOnlyList<FormField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var passwords = fields.Where(field => field.Kind == FieldKind.Password).ToList();
        if (passwords.Count != 1 || HasAnyWord(passwords[0], NewPasswordWords))
        {
            return null;
        }

        var password = passwords[0];
        var before = fields.TakeWhile(field => !ReferenceEquals(field, password) && field.Id != password.Id)
            .Where(field => field.Kind == FieldKind.Text)
            .ToList();
        var identifier = before.LastOrDefault(field => HasAnyWord(field, IdentifierWords)) ?? before.LastOrDefault();
        return (identifier, password);
    }

    /// <summary>
    /// Gets the sentence that tells why the password stays closed.
    /// </summary>
    /// <param name="presence">What came of the check of the user.</param>
    /// <returns>The sentence, in French.</returns>
    public static string PasswordNoteFor(PresenceResult presence)
    {
        return presence == PresenceResult.Unavailable
            ? "Mot de passe non rempli : Windows Hello n'est pas configuré sur ce PC. Ajoute un code PIN dans Paramètres, Comptes, Options de connexion."
            : "Mot de passe non rempli : vérification Windows Hello annulée.";
    }

    /// <summary>
    /// Fills the sign-in form of the page, if there is one and an account fits.
    /// </summary>
    /// <param name="fields">The fields of the form.</param>
    /// <param name="page">The page being filled: its site in a browser, its program elsewhere.</param>
    /// <param name="cancellationToken">A token to cancel the fill.</param>
    /// <returns>What was written, or <see langword="null"/> if the form was left to the rest of PasteFlow.</returns>
    public async Task<LoginFill?> FillAsync(IReadOnlyList<FormField> fields, PageContext page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(page);

        IsLeftToManager = false;
        UnfilledNote = string.Empty;
        var signIn = SignInFields(fields);
        if (signIn is null || signIn.Value.Identifier is { HasValue: true })
        {
            return null;
        }

        // A sign-in page asks for the account of the site: the profile or the screen would only give a wrong one
        IsLeftToManager = true;
        if (!_manager.IsUnlocked)
        {
            UnfilledNote = LockedNote;
            return null;
        }

        var (identifierField, passwordField) = signIn.Value;

        var login = await ChooseLoginAsync(page, cancellationToken);
        if (login is null)
        {
            UnfilledNote = _hasAccounts ? string.Empty : NoAccountNoteFor(page);
            return null;
        }

        IsLeftToManager = false;

        // The identifier is not secret: it is written even if the password stays closed
        var identifier = identifierField is not null && login.UserName.Length > 0
            && await _writer.TryWriteAsync(identifierField, login.UserName, cancellationToken)
            ? login.UserName
            : string.Empty;
        var fill = new LoginFill
        {
            AccountName = login.Name,
            IdentifierField = identifier.Length > 0 ? identifierField : null,
            Identifier = identifier,
            PasswordField = passwordField,
        };

        // Asked for while Windows Hello waits for the user, so that it is ready when they are; only written once they are
        // checked, and dropped otherwise
        var passwordTask = _manager.GetPasswordAsync(login.Id, cancellationToken);
        var presence = await _gate.OpenAsync($"Remplir ton mot de passe {login.Name}", cancellationToken);
        var password = await passwordTask;
        if (presence != PresenceResult.Verified)
        {
            return fill with { PasswordNote = PasswordNoteFor(presence) };
        }

        if (password is not { Length: > 0 })
        {
            return fill with { PasswordNote = $"Mot de passe non rempli : Bitwarden n'a pas donné le mot de passe de « {login.Name} »." };
        }

        var isWritten = await _writer.TryWriteAsync(passwordField, password, cancellationToken);
        return fill with
        {
            IsPasswordWritten = isWritten,
            PasswordNote = isWritten ? string.Empty : "Mot de passe non rempli : ce champ refuse l'écriture automatique.",
        };
    }

    private static bool HasAnyWord(FormField field, string[] words)
    {
        var fieldWords = ReferenceText.Normalize($"{field.Label} {field.TechnicalName} {field.Hint}")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fieldWords.Any(word => words.Contains(word, StringComparer.Ordinal));
    }

    /// <summary>
    /// Chooses the account: the only one saved for the site of the page, else the one the user picks among those of
    /// the site, or among those named like the application.
    /// </summary>
    /// <param name="page">The page being filled.</param>
    /// <param name="cancellationToken">A token to cancel the choice.</param>
    /// <returns>The account, or <see langword="null"/> if none fits or the user cancelled.</returns>
    private async Task<VaultLogin?> ChooseLoginAsync(PageContext page, CancellationToken cancellationToken)
    {
        _hasAccounts = false;
        if (page.Host.Length > 0)
        {
            // Safety net: whatever the manager answers, only the accounts of this very site are kept
            var logins = (await _manager.FindLoginsForSiteAsync(page.Host, cancellationToken))
                .Where(login => LoginSites.IsFor(login, page.Host))
                .ToList();
            _hasAccounts = logins.Count > 0;
            return logins.Count switch
            {
                0 => null,
                1 => logins[0],
                _ => _picker is null ? null : await _picker.ChooseAsync(logins, cancellationToken),
            };
        }

        // An application has no address: its accounts are only proposed, never chosen for the user
        if (page.Program.Length == 0 || BrowserPrograms.Contains(page.Program) || _picker is null)
        {
            return null;
        }

        var named = await _manager.SearchLoginsAsync(page.Program, cancellationToken);
        _hasAccounts = named.Count > 0;
        return named.Count == 0 ? null : await _picker.ChooseAsync(named, cancellationToken);
    }
}
