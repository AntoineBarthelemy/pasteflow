using System.Globalization;

namespace PasteFlow.Core.Screen;

/// <summary>
/// The memory of the screen: the texts of the windows seen a moment ago, so that a text no longer on screen (a window
/// closed, a conversation left) can still be found and pasted (decision of Antoine of 14/11/2026).
/// </summary>
/// <remarks>
/// <para>Privacy: the texts stay in memory only, never written to the disk, and are forgotten after
/// <see cref="KeepFor"/> or when PasteFlow stops. Password managers and private browsing are never kept, and the user can
/// switch the memory off or empty it.</para>
/// <para>Safety: these texts are only searched in the list of Ctrl+Alt+V; a form is never filled from a window that is
/// no longer on screen.</para>
/// </remarks>
public sealed class ScreenMemory
{
    /// <summary>
    /// The most texts kept; the oldest one goes first.
    /// </summary>
    public const int MaxTexts = 20;

    /// <summary>
    /// The longest text kept for a window.
    /// </summary>
    public const int MaxTextLength = 100_000;

    // Programs that keep secrets, never remembered
    private static readonly HashSet<string> SecretPrograms = new(StringComparer.OrdinalIgnoreCase)
    {
        "bitwarden", "keepass", "keepassxc", "1password", "dashlane", "lastpass", "enpass", "nordpass", "proton pass",
        "pasteflow", "pasteflow.app", "lockapp", "logonui", "credentialuibroker", "consent",
    };

    // Words of a title that show a private window or a vault
    private static readonly string[] SecretTitleWords =
    {
        "inprivate", "incognito", "navigation privée", "private browsing", "fenêtre privée", "bitwarden", "keepass",
        "1password",
    };

    private readonly object _gate = new();
    private readonly List<RecentText> _texts = new();
    private readonly TimeProvider _time;
    private bool _isEnabled = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScreenMemory"/> class.
    /// </summary>
    /// <param name="time">The clock, the system one by default.</param>
    public ScreenMemory(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets how long a text is kept.
    /// </summary>
    public static TimeSpan KeepFor { get; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets a value indicating whether the texts seen are kept; switching it off forgets them.
    /// </summary>
    public bool IsEnabled
    {
        get => Volatile.Read(ref _isEnabled);
        set
        {
            Volatile.Write(ref _isEnabled, value);
            if (!value)
            {
                Clear();
            }
        }
    }

    /// <summary>
    /// Determines whether a window must never be remembered: a password manager, a private window, PasteFlow itself.
    /// </summary>
    /// <param name="window">The window read.</param>
    /// <returns><see langword="true"/> to leave it out; otherwise <see langword="false"/>.</returns>
    public static bool IsExcluded(WindowText window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var title = window.Title.ToLowerInvariant();
        return SecretPrograms.Contains(window.Program.Trim())
            || SecretTitleWords.Any(word => title.Contains(word, StringComparison.Ordinal));
    }

    /// <summary>
    /// Keeps the text of a window just seen.
    /// </summary>
    /// <param name="window">The window read.</param>
    public void Remember(WindowText window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var text = window.Text.Trim();
        if (!IsEnabled || text.Length == 0 || IsExcluded(window))
        {
            return;
        }

        var kept = new WindowText
        {
            Title = window.Title.Trim(),
            Text = text.Length > MaxTextLength ? text[..MaxTextLength] : text,
            Rank = 0,
            Program = window.Program,
        };
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            Forget(now);

            // The same window seen again with the same text, or with more of it (a conversation going on), is kept once
            _ = _texts.RemoveAll(recent => string.Equals(recent.Window.Title, kept.Title, StringComparison.Ordinal)
                && kept.Text.Contains(recent.Window.Text, StringComparison.Ordinal));
            if (_texts.Exists(recent => string.Equals(recent.Window.Title, kept.Title, StringComparison.Ordinal)
                && recent.Window.Text.Contains(kept.Text, StringComparison.Ordinal)))
            {
                return;
            }

            _texts.Add(new RecentText(kept, now));
            if (_texts.Count > MaxTexts)
            {
                _texts.RemoveRange(0, _texts.Count - MaxTexts);
            }
        }
    }

    /// <summary>
    /// Gets the texts kept, newest first.
    /// </summary>
    /// <returns>The texts seen less than <see cref="KeepFor"/> ago.</returns>
    public IReadOnlyList<RecentText> Recent()
    {
        lock (_gate)
        {
            Forget(_time.GetUtcNow());
            return _texts.AsEnumerable().Reverse().ToList();
        }
    }

    /// <summary>
    /// Gives the texts kept as windows to search, after the windows on screen, without those still shown as they were.
    /// </summary>
    /// <param name="onScreen">The windows read on screen.</param>
    /// <returns>The windows of the memory, titled « Vu récemment : Claude (14h05) ».</returns>
    public IReadOnlyList<WindowText> AsWindows(IReadOnlyList<WindowText> onScreen)
    {
        ArgumentNullException.ThrowIfNull(onScreen);

        var firstRank = onScreen.Count == 0 ? 0 : onScreen.Max(window => window.Rank) + 1;
        return Recent()
            .Where(recent => !onScreen.Any(window => window.Text.Trim().Contains(recent.Window.Text, StringComparison.Ordinal)))
            .Select((recent, index) => recent.Window with
            {
                Title = string.Create(CultureInfo.InvariantCulture, $"Vu récemment : {recent.Window.Title} ({TimeOfDay(recent.SeenAt)})"),
                Rank = firstRank + index,
            })
            .ToList();
    }

    /// <summary>
    /// Forgets every text kept.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _texts.Clear();
        }
    }

    private static string TimeOfDay(DateTimeOffset seenAt)
    {
        var local = TimeZoneInfo.ConvertTime(seenAt, TimeZoneInfo.Local);
        return local.ToString("HH'h'mm", CultureInfo.InvariantCulture);
    }

    private void Forget(DateTimeOffset now)
    {
        _ = _texts.RemoveAll(recent => now - recent.SeenAt > KeepFor);
    }
}
