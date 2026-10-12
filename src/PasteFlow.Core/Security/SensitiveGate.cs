namespace PasteFlow.Core.Security;

/// <summary>
/// Guards the sensitive values: they are only used once the user proved it is them (Windows Hello), and for a
/// limited time; the gate closes again when the computer is locked or goes to sleep.
/// </summary>
/// <remarks>Two requests at once lead to a single check: the second one waits for the answer to the first.</remarks>
public sealed class SensitiveGate : IDisposable
{
    /// <summary>
    /// How long the gate stays open after a check.
    /// </summary>
    public static readonly TimeSpan OpenDuration = TimeSpan.FromMinutes(30);

    private readonly IUserPresenceCheck _check;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);

    // UTC ticks, read and written at once: the gate is closed from the thread of the Windows session events
    private long _openUntilTicks;

    /// <summary>
    /// Initializes a new instance of the <see cref="SensitiveGate"/> class.
    /// </summary>
    /// <param name="check">What checks the user.</param>
    public SensitiveGate(IUserPresenceCheck check)
        : this(check, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SensitiveGate"/> class with a given clock.
    /// </summary>
    /// <param name="check">What checks the user.</param>
    /// <param name="timeProvider">The clock that measures how long the gate stays open.</param>
    public SensitiveGate(IUserPresenceCheck check, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _check = check;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets a value indicating whether the gate is open: the user proved it is them less than
    /// <see cref="OpenDuration"/> ago, and the computer was not locked since.
    /// </summary>
    public bool IsOpen => _timeProvider.GetUtcNow().UtcTicks < Interlocked.Read(ref _openUntilTicks);

    /// <summary>
    /// Opens the gate, asking the user to prove it is them unless it is already open.
    /// </summary>
    /// <param name="reason">Why, shown to the user.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns><see cref="PresenceResult.Verified"/> if the gate is open; otherwise why it stays closed.</returns>
    public async Task<PresenceResult> OpenAsync(string reason, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsOpen)
            {
                return PresenceResult.Verified;
            }

            var result = await _check.VerifyAsync(reason, cancellationToken);
            if (result == PresenceResult.Verified)
            {
                _ = Interlocked.Exchange(ref _openUntilTicks, (_timeProvider.GetUtcNow() + OpenDuration).UtcTicks);
            }

            return result;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>
    /// Closes the gate at once: the next sensitive value asks the user again.
    /// </summary>
    public void Close()
    {
        _ = Interlocked.Exchange(ref _openUntilTicks, 0);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _gate.Dispose();
    }
}
