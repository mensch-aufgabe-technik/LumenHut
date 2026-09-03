using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LumenHut.Models;
using LumenHut.Services;

namespace LumenHut.Tests;

/// <summary>
/// Verifies the proxy setting is actually wired into browser launches: a minimal
/// HTTP proxy listens on loopback and must receive the page request.
/// </summary>
[Trait("Category", "Integration")]
public class ProxyIntegrationTests : IAsyncLifetime
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly ConcurrentBag<string> _requestLines = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptLoop;

    public Task InitializeAsync()
    {
        _listener.Start();
        _acceptLoop = Task.Run(AcceptLoopAsync);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        if (_acceptLoop != null)
            await Task.WhenAny(_acceptLoop, Task.Delay(2000));
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException)
            {
                return;
            }

            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

            var requestLine = await reader.ReadLineAsync();
            if (requestLine == null) return;
            _requestLines.Add(requestLine);

            // Drain headers so the browser considers the request sent.
            while (await reader.ReadLineAsync() is { Length: > 0 }) { }

            const string body = "<html><head><title>proxied</title></head><body>proxied</body></html>";
            var response = "HTTP/1.1 200 OK\r\n"
                           + "Content-Type: text/html\r\n"
                           + $"Content-Length: {Encoding.ASCII.GetByteCount(body)}\r\n"
                           + "Connection: close\r\n\r\n"
                           + body;
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
        }
    }

    [Fact]
    public async Task RunTests_WithProxyConfigured_RoutesTrafficThroughProxy()
    {
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        var proxy = ProxyConfig.Parse($"http://127.0.0.1:{port}");

        await using var service = new PlaywrightPerfService();
        // Plain http target: the proxy then sees the absolute-form GET instead of a CONNECT tunnel.
        var results = await service.RunTestsAsync("http://lumenhut-proxy-check.test/", new[] { "Chromium" }, proxy);

        var result = Assert.Single(results);
        Assert.False(result.Skipped, result.SkipReason);
        Assert.Contains(_requestLines, line => line.StartsWith("GET http://lumenhut-proxy-check.test/"));
    }
}
