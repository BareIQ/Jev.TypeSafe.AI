using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TypeSafe.AI.Tests.Support;

/// <summary>A request as received by the loopback server.</summary>
internal sealed record LoopbackRequest(string Method, string Path, IReadOnlyDictionary<string, string[]> Headers, string Body)
{
    public string[] HeaderValues(string name) => Headers.TryGetValue(name, out string[]? values) ? values : [];
}

/// <summary>A real HTTP server on the loopback interface, for tests that must exercise the real transport.</summary>
internal sealed class LoopbackServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<HttpListenerContext, LoopbackRequest, Task> _handler;
    private readonly Task _loop;
    private readonly List<LoopbackRequest> _requests = [];
    private readonly object _gate = new();

    private LoopbackServer(int port, Func<HttpListenerContext, LoopbackRequest, Task> handler)
    {
        _handler = handler;
        Uri = new Uri($"http://127.0.0.1:{port}");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();
        _loop = Task.Run(AcceptLoopAsync);
    }

    public Uri Uri { get; }

    public IReadOnlyList<LoopbackRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests.ToArray();
            }
        }
    }

    public static LoopbackServer Start(Func<HttpListenerContext, LoopbackRequest, Task> handler)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return new LoopbackServer(FreePort(), handler);
            }
            catch (HttpListenerException) when (attempt < 5)
            {
                // The port was taken between probing and binding; try another.
            }
        }
    }

    /// <summary>A port nothing is listening on.</summary>
    public static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try
        {
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        finally
        {
            probe.Stop();
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Abort();
        try
        {
            _loop.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // The accept loop ends with an exception when the listener is aborted.
        }

        _stop.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (_stop.IsCancellationRequested)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            string body;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }

            var headers = context.Request.Headers.AllKeys
                .Where(key => key is not null)
                .ToDictionary(key => key!, key => context.Request.Headers.GetValues(key!) ?? [], StringComparer.OrdinalIgnoreCase);
            var request = new LoopbackRequest(context.Request.HttpMethod, context.Request.Url!.AbsolutePath, headers, body);
            lock (_gate)
            {
                _requests.Add(request);
            }

            await _handler(context, request);
        }
        catch (Exception) when (_stop.IsCancellationRequested)
        {
            // The server is shutting down.
        }
        catch (HttpListenerException)
        {
            // The client went away; nothing to answer.
        }
        catch (ObjectDisposedException)
        {
            // The client went away; nothing to answer.
        }
    }
}
