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
/// </remarks>
internal sealed partial class BitwardenCli : IPasswordManager
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(20);

    private readonly string? _executable;
    private volatile string? _session;

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
        var output = await RunAsync(new[] { "status" }, new Dictionary<string, string>(), cancellationToken);
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
            new[] { "unlock", "--passwordenv", "BW_PASSWORD", "--raw" },
            new Dictionary<string, string> { ["BW_PASSWORD"] = masterPassword },
            cancellationToken);
        session = session?.Trim();
        _session = string.IsNullOrEmpty(session) || session.Any(char.IsWhiteSpace) ? null : session;
        return _session is not null;
    }

    /// <summary>
    /// Forgets the session key: the vault must be unlocked again.
    /// </summary>
    public void Lock()
    {
        _session = null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<VaultLogin>> FindLoginsForSiteAsync(string host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);

        var address = $"https://{host}";
        if (!IsSafeArgument(host) || host.Contains(' ', StringComparison.Ordinal))
        {
            return Array.Empty<VaultLogin>();
        }

        var output = await RunWithSessionAsync(new[] { "list", "items", "--url", address }, cancellationToken);
        return output is null ? Array.Empty<VaultLogin>() : BitwardenJson.Logins(output);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<VaultLogin>> SearchLoginsAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!IsSafeArgument(text))
        {
            return Array.Empty<VaultLogin>();
        }

        var output = await RunWithSessionAsync(new[] { "list", "items", "--search", text }, cancellationToken);
        return output is null ? Array.Empty<VaultLogin>() : BitwardenJson.Logins(output);
    }

    /// <inheritdoc/>
    public async Task<string?> GetPasswordAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (!IsSafeArgument(id) || id.Contains(' ', StringComparison.Ordinal))
        {
            return null;
        }

        var output = await RunWithSessionAsync(new[] { "get", "password", id }, cancellationToken);

        // bw ends its answer with a line break that is not part of the password
        return string.IsNullOrEmpty(output) ? null : output.TrimEnd('\r', '\n');
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProfileValue>> GetIdentityAsync(CancellationToken cancellationToken)
    {
        var output = await RunWithSessionAsync(new[] { "list", "items" }, cancellationToken);
        return output is null ? Array.Empty<ProfileValue>() : BitwardenJson.Identity(output);
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

    [GeneratedRegex(@"^[\p{L}\p{N} ._:/@-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SafeArgumentRegex();

    private Task<string?> RunWithSessionAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var session = _session;
        return session is null
            ? Task.FromResult<string?>(null)
            : RunAsync(arguments, new Dictionary<string, string> { ["BW_SESSION"] = session }, cancellationToken);
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
        start.ArgumentList.Add("--nointeraction");

        start.Environment["BW_NOINTERACTION"] = "true";
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
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
}
