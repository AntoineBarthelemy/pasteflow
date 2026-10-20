using System.IO;
using System.IO.Pipes;
using PasteFlow.Core.Browser;

namespace PasteFlow.App.Browser;

/// <summary>
/// PasteFlow started by a browser for its extension (native messaging): it only relays the messages between the
/// extension, on its standard input and output, and the PasteFlow running in the notification area, through the pipe
/// of <see cref="BrowserBridge"/>. It has no window nor icon, and ends with the browser.
/// </summary>
internal static class NativeHost
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Tells whether PasteFlow was started by a browser for its extension: the browser gives the origin of the
    /// extension, such as "chrome-extension://…/".
    /// </summary>
    /// <param name="args">The arguments of the command line.</param>
    /// <returns><see langword="true"/> if PasteFlow must act as the relay; otherwise <see langword="false"/>.</returns>
    public static bool IsStartedByBrowser(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return args.Any(arg => arg.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Relays the messages until the browser closes.
    /// </summary>
    /// <param name="pipeName">The pipe of PasteFlow.</param>
    /// <returns>The exit code: 0.</returns>
    public static int Run(string pipeName)
    {
        using var browserIn = Console.OpenStandardInput();
        using var browserOut = Console.OpenStandardOutput();
        RelayAsync(browserIn, browserOut, pipeName, CancellationToken.None).GetAwaiter().GetResult();
        return 0;
    }

    /// <summary>
    /// Relays the messages of the browser to PasteFlow and back, connecting again to PasteFlow when it restarts.
    /// </summary>
    /// <param name="browserIn">What the browser sends.</param>
    /// <param name="browserOut">What is sent to the browser.</param>
    /// <param name="pipeName">The pipe of PasteFlow.</param>
    /// <param name="cancellationToken">A token to stop relaying.</param>
    /// <returns>A task completed once the browser closed its side.</returns>
    internal static async Task RelayAsync(Stream browserIn, Stream browserOut, string pipeName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(browserIn);
        ArgumentNullException.ThrowIfNull(browserOut);

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var outGate = new SemaphoreSlim(1, 1);
        using var connection = new PipeHolder();
        var fromPasteFlow = RelayFromPasteFlowAsync(browserOut, outGate, pipeName, connection, stop.Token);
        try
        {
            while (await NativeMessages.ReadAsync(browserIn, stop.Token).ConfigureAwait(false) is { } message)
            {
                // An answer while PasteFlow is away is simply lost: PasteFlow asks again at the next shortcut
                await connection.TrySendAsync(message, stop.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or InvalidDataException)
        {
            // The browser closed its side
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(false);
        }

        await fromPasteFlow.ConfigureAwait(false);
    }

    private static async Task RelayFromPasteFlowAsync(Stream browserOut, SemaphoreSlim outGate, string pipeName, PipeHolder connection, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.ConnectAsync(ConnectTimeout, cancellationToken).ConfigureAwait(false);
                connection.Set(pipe);
                while (await NativeMessages.ReadAsync(pipe, cancellationToken).ConfigureAwait(false) is { } message)
                {
                    await outGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        await NativeMessages.WriteAsync(browserOut, message, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        _ = outGate.Release();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException or TimeoutException or InvalidDataException or UnauthorizedAccessException)
            {
                // PasteFlow is not running, or stopped: tried again in a moment
            }
            finally
            {
                connection.Set(null);
            }

            try
            {
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// The pipe to PasteFlow, when connected, shared between the two directions of the relay.
    /// </summary>
    private sealed class PipeHolder : IDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private Stream? _pipe;

        public void Set(Stream? pipe)
        {
            Volatile.Write(ref _pipe, pipe);
        }

        public async Task TrySendAsync(string message, CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _pipe) is not { } pipe)
            {
                return;
            }

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await NativeMessages.WriteAsync(pipe, message, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
                // PasteFlow just stopped: the message is lost
            }
            finally
            {
                _ = _gate.Release();
            }
        }

        public void Dispose()
        {
            _gate.Dispose();
        }
    }
}
