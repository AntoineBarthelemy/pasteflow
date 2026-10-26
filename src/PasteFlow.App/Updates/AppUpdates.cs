using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows.Threading;
using PasteFlow.App.Diagnostics;
using PasteFlow.App.Notifications;
using Velopack;
using Velopack.Sources;

namespace PasteFlow.App.Updates;

/// <summary>
/// Keeps PasteFlow up to date: a new version published on the public repository of the versions is downloaded in the
/// background, then installed at the next launch of PasteFlow, or at once from the menu (Ctrl+Alt+P, then U). The
/// menu can also look for one at once.
/// </summary>
/// <remarks>
/// <para>Only an installed PasteFlow updates itself; started from the code (dotnet run), it never looks for updates.</para>
/// <para>The code of PasteFlow stays private: only the installers and update packages are public.</para>
/// </remarks>
internal sealed class AppUpdates : IDisposable
{
    /// <summary>
    /// The public repository where the versions are published.
    /// </summary>
    public const string ReleasesRepository = "https://github.com/AntoineBarthelemy/pasteflow-releases";

    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly UpdateManager[] _managers;
    private readonly PopupNotifier _notifier;
    private readonly Dispatcher _dispatcher;
    private readonly CancellationTokenSource _stop = new();
    private volatile UpdateInfo? _ready;
    private UpdateManager? _readyManager;
    private int _isChecking;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUpdates"/> class.
    /// </summary>
    /// <param name="notifier">The notifier that tells when a new version is ready.</param>
    public AppUpdates(PopupNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(notifier);

        _notifier = notifier;

        // Created on the thread of the windows: the checks end on other threads, the popups are shown on this one
        _dispatcher = Dispatcher.CurrentDispatcher;
        _managers = new[]
        {
            new UpdateManager(new GithubSource(ReleasesRepository, null, false)),

            // The files of the latest version, read without the API of GitHub: its free quota is shared by all the
            // computers of a network (a school, a company), and can be used up by others
            new UpdateManager(new SimpleWebSource($"{ReleasesRepository}/releases/latest/download/")),
        };
    }

    /// <summary>
    /// Gets the version of PasteFlow running, such as "1.0.0".
    /// </summary>
    public static string CurrentVersion
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev";

            // The build adds the commit after a "+": only the version is shown
            var end = version.IndexOf('+', StringComparison.Ordinal);
            return end > 0 ? version[..end] : version;
        }
    }

    /// <summary>
    /// Gets a value indicating whether a new version is downloaded and waits to be installed.
    /// </summary>
    public bool IsUpdateReady => _ready is not null;

    /// <summary>
    /// Gets the version downloaded and waiting to be installed, or an empty string.
    /// </summary>
    public string ReadyVersion => _ready?.TargetFullRelease.Version.ToString() ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether PasteFlow runs installed, the only way it updates itself.
    /// </summary>
    public bool IsInstalled => _managers[0].IsInstalled;

    /// <summary>
    /// Starts looking for new versions: a minute after the launch, then every few hours.
    /// </summary>
    public void Start()
    {
        if (_managers[0].IsInstalled)
        {
            _ = CheckRegularlyAsync(_stop.Token);
        }
    }

    /// <summary>
    /// Looks for a new version at once, as asked from the menu, and tells the result.
    /// </summary>
    /// <returns>A task that ends when the new version is downloaded, or when there is none.</returns>
    public Task CheckNowAsync()
    {
        return _managers[0].IsInstalled ? CheckOnceAsync(isAsked: true) : Task.CompletedTask;
    }

    /// <summary>
    /// Installs the new version downloaded and restarts PasteFlow.
    /// </summary>
    public void InstallNowAndRestart()
    {
        if (_ready is { } ready && _readyManager is { } manager)
        {
            manager.ApplyUpdatesAndRestart(ready);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private void Tell(string message)
    {
        _ = _dispatcher.InvokeAsync(() => _notifier.Notify(message));
    }

    private async Task CheckRegularlyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(FirstCheckDelay, cancellationToken);
            while (!cancellationToken.IsCancellationRequested)
            {
                await CheckOnceAsync(isAsked: false);
                await Task.Delay(CheckInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // PasteFlow closes
        }
    }

    private async Task CheckOnceAsync(bool isAsked)
    {
        if (_ready is not null)
        {
            if (isAsked)
            {
                Tell($"PasteFlow {ReadyVersion} est déjà prêt : Ctrl+Alt+P puis U pour l'installer.");
            }

            return;
        }

        // One check at a time: the menu can ask while the regular check runs
        if (Interlocked.Exchange(ref _isChecking, 1) == 1)
        {
            return;
        }

        try
        {
            // The second source is only asked when the first one fails
            var detail = string.Empty;
            foreach (var manager in _managers)
            {
                try
                {
                    await CheckWithAsync(manager, isAsked);
                    return;
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or UnauthorizedAccessException or TaskCanceledException)
                {
                    ErrorLog.Write("Recherche de mise à jour", exception);
                    detail = ErrorLog.NetworkDetail(exception);
                }
            }

            if (isAsked)
            {
                var reason = detail.Length > 0 ? detail.Trim() : "serveur injoignable";
                Tell($"Impossible de vérifier les mises à jour pour l'instant ({reason}). Tu as PasteFlow {CurrentVersion}. Réessaie plus tard.");
            }
        }
        finally
        {
            _ = Interlocked.Exchange(ref _isChecking, 0);
        }
    }

    private async Task CheckWithAsync(UpdateManager manager, bool isAsked)
    {
        var update = await manager.CheckForUpdatesAsync();
        if (update is null)
        {
            if (isAsked)
            {
                Tell($"PasteFlow est à jour ({CurrentVersion}).");
            }

            return;
        }

        if (isAsked)
        {
            Tell($"PasteFlow {update.TargetFullRelease.Version} se télécharge…");
        }

        await manager.DownloadUpdatesAsync(update);
        _readyManager = manager;
        _ready = update;
        Tell($"PasteFlow {update.TargetFullRelease.Version} est prêt : il s'installe au prochain lancement, ou tout de suite avec Ctrl+Alt+P puis U.");
    }
}
