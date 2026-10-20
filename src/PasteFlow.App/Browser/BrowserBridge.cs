using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using PasteFlow.Core.Browser;

namespace PasteFlow.App.Browser;

/// <summary>
/// The link between PasteFlow and the extensions of the browsers: each browser starts a small relay (see
/// <see cref="NativeHost"/>), which connects to this pipe; PasteFlow asks every relay connected for the recent tabs.
/// </summary>
/// <remarks>
/// The pipe only accepts programs of the same Windows user. A browser that does not answer in time is left aside: the
/// fill goes on with what the screen shows.
/// </remarks>
internal sealed class BrowserBridge : IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<int, HostConnection> _hosts = new();
    private int _nextHost;
    private int _nextRequest;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserBridge"/> class and starts waiting for the browsers.
    /// </summary>
    /// <param name="pipeName">The name of the pipe.</param>
    public BrowserBridge(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        _pipeName = pipeName;
        _ = AcceptAsync(_stop.Token);
    }

    /// <summary>
    /// Gets the name of the pipe of the current Windows user.
    /// </summary>
    public static string PipeName => $"PasteFlow.Browser.{WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName}";

    /// <summary>
    /// Gets the number of browsers connected.
    /// </summary>
    public int ConnectedCount => _hosts.Count;

    /// <summary>
    /// Asks every browser connected for its recent tabs.
    /// </summary>
    /// <param name="timeout">How long to wait for the browsers.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The tabs, the most recently viewed first; empty without browser connected.</returns>
    public async Task<IReadOnlyList<BrowserTab>> ReadTabsAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var hosts = _hosts.Values.ToList();
        if (hosts.Count == 0)
        {
            return Array.Empty<BrowserTab>();
        }

        var id = Interlocked.Increment(ref _nextRequest);
        var answers = await Task.WhenAll(hosts.Select(host => host.AskAsync(id, timeout, cancellationToken))).ConfigureAwait(false);
        return answers
            .SelectMany(tabs => tabs)
            .OrderByDescending(tab => tab.LastAccessed)
            .Take(BrowserTabs.MaxTabs)
            .ToList();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _stop.Cancel();
        foreach (var host in _hosts.Values)
        {
            host.Dispose();
        }

        _hosts.Clear();
        _stop.Dispose();
    }

    private static async Task DelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopping: the loop ends
        }
    }

    private async Task AcceptAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                pipe?.Dispose();
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Another PasteFlow owns the pipe, or the system refused it for now: tried again in a moment
                pipe?.Dispose();
                await DelayAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var key = Interlocked.Increment(ref _nextHost);
            var host = new HostConnection(pipe);
            _hosts[key] = host;
            _ = RunHostAsync(key, host, cancellationToken);
        }
    }

    private async Task RunHostAsync(int key, HostConnection host, CancellationToken cancellationToken)
    {
        await host.ListenAsync(cancellationToken).ConfigureAwait(false);
        if (_hosts.TryRemove(key, out var removed))
        {
            removed.Dispose();
        }
    }

    /// <summary>
    /// One browser connected through its relay.
    /// </summary>
    private sealed class HostConnection : IDisposable
    {
        private readonly NamedPipeServerStream _pipe;
        private readonly SemaphoreSlim _writeGate = new(1, 1);
        private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _pending = new();

        public HostConnection(NamedPipeServerStream pipe)
        {
            _pipe = pipe;
        }

        public async Task ListenAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (await NativeMessages.ReadAsync(_pipe, cancellationToken).ConfigureAwait(false) is { } message)
                {
                    if (BrowserTabs.AnswerId(message) is { } id && _pending.TryRemove(id, out var waiting))
                    {
                        _ = waiting.TrySetResult(message);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or OperationCanceledException or ObjectDisposedException or InvalidDataException)
            {
                // The browser closed, or PasteFlow stops
            }
        }

        public async Task<IReadOnlyList<BrowserTab>> AskAsync(int id, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = answer;
            try
            {
                await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await NativeMessages.WriteAsync(_pipe, BrowserTabs.Request(id), cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _ = _writeGate.Release();
                }

                var finished = await Task.WhenAny(answer.Task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
                return finished == answer.Task ? BrowserTabs.Read(await answer.Task.ConfigureAwait(false)) : Array.Empty<BrowserTab>();
            }
            catch (Exception exception) when (exception is IOException or OperationCanceledException or ObjectDisposedException)
            {
                return Array.Empty<BrowserTab>();
            }
            finally
            {
                _ = _pending.TryRemove(id, out _);
            }
        }

        public void Dispose()
        {
            _pipe.Dispose();
            _writeGate.Dispose();
        }
    }
}
