using System.IO;
using System.Net.Http;
using System.Reflection;
using PasteFlow.App.Diagnostics;
using PasteFlow.App.Notifications;
using Velopack;
using Velopack.Sources;

namespace PasteFlow.App.Updates;

/// <summary>
/// Keeps PasteFlow up to date: a new version published on the public repository of the versions is downloaded in the
/// background, then installed at the next launch of PasteFlow, or at once from the menu (Ctrl+Alt+P, then U).
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
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly UpdateManager _manager;
    private readonly PopupNotifier _notifier;
    private readonly CancellationTokenSource _stop = new();
    private UpdateInfo? _ready;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUpdates"/> class.
    /// </summary>
    /// <param name="notifier">The notifier that tells when a new version is ready.</param>
    public AppUpdates(PopupNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(notifier);

        _notifier = notifier;
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

    private async Task CheckRegularlyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(FirstCheckDelay, cancellationToken);
            while (!cancellationToken.IsCancellationRequested)
            {
                await CheckOnceAsync();
                await Task.Delay(CheckInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // PasteFlow closes
        }
    }

    private async Task CheckOnceAsync()
    {
        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null || _ready is not null)
            {
                return;
            }

            await _manager.DownloadUpdatesAsync(update);
            _ready = update;
            _notifier.Notify($"PasteFlow {update.TargetFullRelease.Version} est prêt : il s'installe au prochain lancement, ou tout de suite avec Ctrl+Alt+P puis U.");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Offline, or the repository does not answer: tried again later, the error is kept for understanding
            ErrorLog.Write("Recherche de mise à jour", exception);
        }
    }
}
