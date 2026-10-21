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

    private readonly UpdateManager _manager;
    private readonly PopupNotifier _notifier;
    private readonly Dispatcher _dispatcher;
    private readonly CancellationTokenSource _stop = new();
    private volatile UpdateInfo? _ready;
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
        _manager = new UpdateManager(new GithubSource(ReleasesRepository, null, false));
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
    public bool IsInstalled => _manager.IsInstalled;

    /// <summary>
    /// Starts looking for new versions: a minute after the launch, then every few hours.
    /// </summary>
    public void Start()
    {
        if (_manager.IsInstalled)
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
        return _manager.IsInstalled ? CheckOnceAsync(isAsked: true) : Task.CompletedTask;
    }

    /// <summary>
    /// Installs the new version downloaded and restarts PasteFlow.
    /// </summary>
    public void InstallNowAndRestart()
    {
        if (_ready is { } ready)
        {
            _manager.ApplyUpdatesAndRestart(ready);
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
            var update = await _manager.CheckForUpdatesAsync();
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

            await _manager.DownloadUpdatesAsync(update);
            _ready = update;
            Tell($"PasteFlow {update.TargetFullRelease.Version} est prêt : il s'installe au prochain lancement, ou tout de suite avec Ctrl+Alt+P puis U.");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Offline, or the repository does not answer: tried again later, the error is kept for understanding
            ErrorLog.Write("Recherche de mise à jour", exception);
            if (isAsked)
            {
                Tell("Impossible de joindre le serveur des mises à jour. Réessaie plus tard.");
            }
        }
        finally
        {
            _ = Interlocked.Exchange(ref _isChecking, 0);
        }
    }
}
