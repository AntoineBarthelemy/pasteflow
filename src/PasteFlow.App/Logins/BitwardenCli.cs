using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PasteFlow.Core.Logins;
using PasteFlow.Core.Profile;

namespace PasteFlow.App.Logins;

/// <summary>
/// The password manager Bitwarden, reached through its command line (bw), installed once by the user.
/// </summary>
/// <remarks>
/// <para>The vault is unlocked with the master password the user types in PasteFlow; Bitwarden then gives a session
/// key, kept in memory only, never written anywhere, and forgotten when PasteFlow closes.</para>
/// <para>The master password and the session key go to bw through its environment, never on its command line, which
/// other programs can read. Every word given to bw is checked first: only letters, digits and a few signs.</para>
/// <para>Each run of bw starts a program and decrypts the vault again, about a second. So the list of accounts, without
/// their passwords, is read once when the vault is unlocked and kept in memory, refreshed in the background every few
/// minutes: finding the account of a site then takes no time. Only the password is asked for at the moment it is
/// written, to the local service of Bitwarden (see <see cref="BitwardenServe"/>) in a few tens of milliseconds, or to
/// bw itself when the service is not there.</para>
/// </remarks>
internal sealed partial class BitwardenCli : IPasswordManager, IDisposable
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(20);
    private static readonly string[] StatusArguments = { "status" };
    private static readonly string[] UnlockArguments = { "unlock", "--passwordenv", "BW_PASSWORD", "--raw" };
    private static readonly string[] ListItemsArguments = { "list", "items" };
    private static readonly string[] SyncArguments = { "sync" };
    private static readonly TimeSpan LoginsMaxAge = TimeSpan.FromMinutes(5);

    private readonly string? _executable;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private volatile string? _session;
    private volatile LoginsCache? _logins;
    private volatile BitwardenServe? _serve;

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenCli"/> class with the bw found on this computer.
    /// </summary>
    public BitwardenCli()
        : this(FindExecutable())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenCli"/> class.
    /// </summary>
    /// <param name="executable">The path of bw, or <see langword="null"/> if it is not installed.</param>
    internal BitwardenCli(string? executable)
    {
        _executable = executable;
    }

    /// <summary>
    /// Gets a value indicating whether bw is installed on this computer.
    /// </summary>
    public bool IsInstalled => _executable is not null;

    /// <inheritdoc/>
    public bool IsUnlocked => _session is not null;

    /// <summary>
    /// Gets the session key of the unlocked vault, to keep it between launches, or <see langword="null"/>.
    /// </summary>
    internal string? Session => _session;

    /// <summary>
    /// Finds bw on this computer: in the folders of the PATH, then where npm and winget put it.
    /// </summary>
    /// <returns>The path of bw, or <see langword="null"/> if it is not installed.</returns>
    public static string? FindExecutable()
    {
        var folders = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"))
            .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links"));
        foreach (var folder in folders)
        {
            foreach (var name in new[] { "bw.exe", "bw.cmd" })
            {
                try
                {
                    var path = Path.Combine(folder.Trim('"'), name);
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
                catch (ArgumentException)
                {
                    // A damaged folder in the PATH: the next one is tried
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the state of the vault.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>"unlocked", "locked" or "unauthenticated", or an empty string if bw does not answer.</returns>
    public async Task<string> StatusAsync(CancellationToken cancellationToken)
    {
        var output = await RunAsync(StatusArguments, new Dictionary<string, string>(), cancellationToken);
        return output is null ? string.Empty : BitwardenJson.Status(output);
    }

    /// <summary>
    /// Unlocks the vault with the master password, and keeps the session key in memory.
    /// </summary>
    /// <param name="masterPassword">The master password typed by the user.</param>
    /// <param name="cancellationToken">A token to cancel the unlocking.</param>
    /// <returns><see langword="true"/> if the vault is unlocked; otherwise <see langword="false"/>.</returns>
    public async Task<bool> UnlockAsync(string masterPassword, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(masterPassword);

        if (masterPassword.Length == 0)
        {
            return false;
        }

        var session = await RunAsync(
            UnlockArguments,
            new Dictionary<string, string> { ["BW_PASSWORD"] = masterPassword },
            cancellationToken);
        session = session?.Trim();
        var unlocked = string.IsNullOrEmpty(session) || session.Any(char.IsWhiteSpace) ? null : session;
        _session = unlocked;
        if (unlocked is null)
        {
            return false;
        }

        // The accounts are read now, in the background, so that the first sign-in does not wait for them; the local
        // service starts too, so that passwords come in a few tens of milliseconds
        _ = RefreshLoginsAsync(withSync: true, CancellationToken.None);
        _ = StartServeAsync(unlocked);
        return true;
    }

    /// <summary>
    /// Takes back a session kept from a previous launch, if the vault is still unlocked with it.
    /// </summary>
    /// <param name="session">The session key kept.</param>
    /// <param name="cancellationToken">A token to cancel the check.</param>
    /// <returns><see langword="true"/> if the vault is unlocked again; otherwise <see langword="false"/>.</returns>
    public async Task<bool> ResumeAsync(string session, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(session);

        // Locked or logged out since, the key no longer opens the vault: bw answers "locked"
        var output = await RunAsync(StatusArguments, new Dictionary<string, string> { ["BW_SESSION"] = session }, cancellationToken);
        if (output is null || BitwardenJson.Status(output) != "unlocked")
        {
            return false;
        }

        _session = session;
        _ = RefreshLoginsAsync(withSync: true, CancellationToken.None);
        _ = StartServeAsync(session);
        return true;
    }

    /// <summary>
    /// Forgets the session key: the vault must be unlocked again.
    /// </summary>
    public void Lock()
    {
        _session = null;
        _logins = null;
        Interlocked.Exchange(ref _serve, null)?.Dispose();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<VaultLogin>> FindLoginsForSiteAsync(string host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);

        var logins = await LoginsAsync(cancellationToken);
        return logins.Where(login => LoginSites.IsFor(login, host)).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<VaultLogin>> SearchLoginsAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Trim().Length == 0)
        {
            return Array.Empty<VaultLogin>();
        }

        var logins = await LoginsAsync(cancellationToken);
        return logins.Where(login => login.Name.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <inheritdoc/>
    public async Task<string?> GetPasswordAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (!IsSafeArgument(id) || id.Contains(' ', StringComparison.Ordinal))
        {
            return null;
        }

        // The local service first, much faster; bw itself when the service is not there
        if (_serve is { IsRunning: true } serve && await serve.GetPasswordAsync(id, cancellationToken) is { } served)
        {
            return served;
        }

        var output = await RunWithSessionAsync(new[] { "get", "password", id }, cancellationToken);

        // bw ends its answer with a line break that is not part of the password
        return string.IsNullOrEmpty(output) ? null : output.TrimEnd('\r', '\n');
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProfileValue>> GetIdentityAsync(CancellationToken cancellationToken)
    {
        var output = await RunWithSessionAsync(ListItemsArguments, cancellationToken);
        return output is null ? Array.Empty<ProfileValue>() : BitwardenJson.Identity(output);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Lock();
        _refreshGate.Dispose();
    }

    /// <summary>
    /// Determines whether a word can be given to bw safely: letters, digits, spaces and a few signs only.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> if the word is safe; otherwise <see langword="false"/>.</returns>
    internal static bool IsSafeArgument(string word)
    {
        return word.Length is > 0 and <= 200 && SafeArgumentRegex().IsMatch(word);
    }

    /// <summary>
    /// Reads the accounts of the vault again, without their passwords, and keeps them in memory.
    /// </summary>
    /// <param name="withSync">Whether to bring the vault up to date with the Bitwarden server first.</param>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>A task completed once the accounts are read.</returns>
    internal async Task RefreshLoginsAsync(bool withSync, CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            if (withSync)
            {
                // Accounts added from the browser extension reach bw only through a sync; offline, the local vault is kept
                _ = await RunWithSessionAsync(SyncArguments, cancellationToken);
            }

            var output = await RunWithSessionAsync(ListItemsArguments, cancellationToken);
            if (output is not null && _session is not null)
            {
                _logins = new LoginsCache(BitwardenJson.Logins(output), DateTimeOffset.UtcNow);
            }
        }
        finally
        {
            _ = _refreshGate.Release();
        }
    }

    [GeneratedRegex(@"^[\p{L}\p{N} ._:/@-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SafeArgumentRegex();

    /// <summary>
    /// Gets the accounts of the vault: at once from memory, read again in the background when they are a few minutes old.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The accounts, without their passwords.</returns>
    private async Task<IReadOnlyList<VaultLogin>> LoginsAsync(CancellationToken cancellationToken)
    {
        if (_logins is null && _session is not null)
        {
            await RefreshLoginsAsync(withSync: false, cancellationToken);
        }

        var cache = _logins;
        if (cache is null)
        {
            return Array.Empty<VaultLogin>();
        }

        if (DateTimeOffset.UtcNow - cache.ReadAt > LoginsMaxAge && _refreshGate.CurrentCount > 0)
        {
            _ = RefreshLoginsAsync(withSync: true, CancellationToken.None);
        }

        return cache.Logins;
    }

    /// <summary>
    /// Starts the local service of Bitwarden for the session given, unless the vault was locked in the meantime.
    /// </summary>
    /// <param name="session">The session key of the unlocked vault.</param>
    /// <returns>A task completed once the service answers, or could not start.</returns>
    private async Task StartServeAsync(string session)
    {
        var start = CreateStartInfo(Array.Empty<string>(), new Dictionary<string, string> { ["BW_SESSION"] = session }, withNoInteraction: false);
        var serve = start is null ? null : await BitwardenServe.StartAsync(start, CancellationToken.None);
        if (serve is null)
        {
            return;
        }

        // Locked, or unlocked again, while the service was starting: this one is not kept
        if (!ReferenceEquals(_session, session) || Interlocked.CompareExchange(ref _serve, serve, null) is not null)
        {
            serve.Dispose();
        }
    }

    private Task<string?> RunWithSessionAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var session = _session;
        return session is null
            ? Task.FromResult<string?>(null)
            : RunAsync(arguments, new Dictionary<string, string> { ["BW_SESSION"] = session }, cancellationToken);
    }

    /// <summary>
    /// Prepares the start of bw, with the secrets in its environment.
    /// </summary>
    /// <param name="arguments">The words given to bw, already checked.</param>
    /// <param name="environment">The secrets given through the environment.</param>
    /// <param name="withNoInteraction">Whether to add --nointeraction after the words.</param>
    /// <returns>How to start bw, or <see langword="null"/> if bw is not installed.</returns>
    private ProcessStartInfo? CreateStartInfo(IReadOnlyList<string> arguments, Dictionary<string, string> environment, bool withNoInteraction)
    {
        if (_executable is null)
        {
            return null;
        }

        // A .cmd file goes through the command interpreter; the words given are checked, so nothing can be slipped in
        var isScript = _executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);
        var start = new ProcessStartInfo(isScript ? "cmd.exe" : _executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        if (isScript)
        {
            start.ArgumentList.Add("/d");
            start.ArgumentList.Add("/c");
            start.ArgumentList.Add(_executable);
        }

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // Never a question asked in a hidden window: bw answers at once, or fails
        if (withNoInteraction)
        {
            start.ArgumentList.Add("--nointeraction");
        }

        start.Environment["BW_NOINTERACTION"] = "true";
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        return start;
    }

    /// <summary>
    /// Runs bw and reads its answer.
    /// </summary>
    /// <param name="arguments">The words given to bw, already checked.</param>
    /// <param name="environment">The secrets given through the environment.</param>
    /// <param name="cancellationToken">A token to cancel the command.</param>
    /// <returns>The answer, or <see langword="null"/> if bw is missing, fails or takes too long.</returns>
    private async Task<string?> RunAsync(IReadOnlyList<string> arguments, Dictionary<string, string> environment, CancellationToken cancellationToken)
    {
        var start = CreateStartInfo(arguments, environment, withNoInteraction: true);
        if (start is null)
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CommandTimeout);
        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            _ = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            var text = await output;
            return process.ExitCode == 0 ? text : null;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            // bw was removed or cannot start: nothing is found
            return null;
        }
    }

    /// <summary>
    /// The accounts of the vault read at a given time.
    /// </summary>
    private sealed class LoginsCache
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LoginsCache"/> class.
        /// </summary>
        /// <param name="logins">The accounts, without their passwords.</param>
        /// <param name="readAt">When they were read.</param>
        public LoginsCache(IReadOnlyList<VaultLogin> logins, DateTimeOffset readAt)
        {
            Logins = logins;
            ReadAt = readAt;
        }

        /// <summary>
        /// Gets the accounts, without their passwords.
        /// </summary>
        public IReadOnlyList<VaultLogin> Logins { get; }

        /// <summary>
        /// Gets when the accounts were read.
        /// </summary>
        public DateTimeOffset ReadAt { get; }
    }
}
