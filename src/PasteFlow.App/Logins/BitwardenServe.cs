using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using PasteFlow.App.Interop;
using PasteFlow.Core.Logins;

namespace PasteFlow.App.Logins;

/// <summary>
/// The local service of Bitwarden (bw serve), started once the vault is unlocked: it gives a password in a few tens of
/// milliseconds, where starting bw for each password takes about a second.
/// </summary>
/// <remarks>
/// <para>The service only listens on this computer (localhost), on a port chosen at random. The trade-off, accepted by
/// Antoine on 20 October 2026: while it runs, another program of this computer could read the vault through it.</para>
/// <para>It is stopped when the vault is locked and when PasteFlow closes, even abruptly (see
/// <see cref="ChildProcesses"/>). If it cannot start or stops answering, PasteFlow goes back to starting bw for each
/// password.</para>
/// </remarks>
internal sealed class BitwardenServe : IDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StartPollDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

    private readonly Process _process;
    private readonly HttpClient _client;
    private bool _isDisposed;

    private BitwardenServe(Process process, int port)
    {
        _process = process;
        _client = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{port}/"),
            Timeout = RequestTimeout,
        };
    }

    /// <summary>
    /// Gets a value indicating whether the service is still running.
    /// </summary>
    public bool IsRunning => !_isDisposed && !HasExited(_process);

    /// <summary>
    /// Starts the service with the session key of the unlocked vault, and waits until it answers with the vault
    /// unlocked.
    /// </summary>
    /// <param name="start">How to start bw, with the session key in its environment, without the words of the command.</param>
    /// <param name="cancellationToken">A token to cancel the start.</param>
    /// <returns>The service, or <see langword="null"/> if it could not start.</returns>
    public static async Task<BitwardenServe?> StartAsync(ProcessStartInfo start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);

        // Without this, the service could outlive PasteFlow with the vault open
        if (!ChildProcesses.CloseWithThisProcess())
        {
            return null;
        }

        var port = FreePort();
        start.ArgumentList.Add("serve");
        start.ArgumentList.Add("--hostname");
        start.ArgumentList.Add("localhost");
        start.ArgumentList.Add("--port");
        start.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.RedirectStandardOutput = false;
        start.RedirectStandardError = false;
        start.StandardOutputEncoding = null;

        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            return null;
        }

        if (process is null)
        {
            return null;
        }

        var serve = new BitwardenServe(process, port);
        var deadline = DateTimeOffset.UtcNow + StartTimeout;
        while (DateTimeOffset.UtcNow < deadline && serve.IsRunning)
        {
            if (await serve.StatusAsync(cancellationToken) == "unlocked")
            {
                return serve;
            }

            await Task.Delay(StartPollDelay, cancellationToken);
        }

        serve.Dispose();
        return null;
    }

    /// <summary>
    /// Gets the password of an account.
    /// </summary>
    /// <param name="id">The identifier of the account in the vault, already checked.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The password, or <see langword="null"/> if the service does not give it.</returns>
    public async Task<string?> GetPasswordAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        var answer = await GetAsync($"object/password/{Uri.EscapeDataString(id)}", cancellationToken);
        return answer is null ? null : BitwardenJson.ServedPassword(answer);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _client.Dispose();
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            // Already stopped
        }

        _process.Dispose();
    }

    private static int FreePort()
    {
        // The system gives a free port; it is released at once for the service to take it
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private async Task<string> StatusAsync(CancellationToken cancellationToken)
    {
        var answer = await GetAsync("status", cancellationToken);
        return answer is null ? string.Empty : BitwardenJson.ServedStatus(answer);
    }

    private async Task<string?> GetAsync(string path, CancellationToken cancellationToken)
    {
        if (_isDisposed)
        {
            return null;
        }

        try
        {
            using var response = await _client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(cancellationToken) : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ObjectDisposedException)
        {
            // Not started yet, stopped, or too slow
            return null;
        }
    }
}
