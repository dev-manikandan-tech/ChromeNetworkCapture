using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ChromeNetworkCapture.Models;

namespace ChromeNetworkCapture;

/// <summary>
/// Client for communicating with Chrome via the DevTools Protocol over WebSocket.
/// </summary>
public sealed class CdpClient : IAsyncDisposable
{
    private readonly ClientWebSocket _webSocket = new();
    private readonly int _port;
    private int _messageId;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    public event Action<CdpResponse>? MessageReceived;

    public CdpClient(int port = 9222)
    {
        _port = port;
    }

    /// <summary>
    /// Connects to Chrome's DevTools Protocol WebSocket endpoint.
    /// Retries connection for up to the specified timeout to allow Chrome to start.
    /// </summary>
    public async Task ConnectAsync(int timeoutSeconds = 30, CancellationToken cancellationToken = default)
    {
        var endTime = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        string? wsUrl = null;

        while (DateTime.UtcNow < endTime)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

                // Try to get the browser WebSocket URL
                var versionJson = await httpClient.GetStringAsync(
                    $"http://localhost:{_port}/json/version", cancellationToken);
                var versionInfo = JsonSerializer.Deserialize<CdpVersionInfo>(versionJson);

                if (versionInfo?.WebSocketDebuggerUrl != null)
                {
                    wsUrl = versionInfo.WebSocketDebuggerUrl;
                    break;
                }

                // Fallback: get first page target
                var targetsJson = await httpClient.GetStringAsync(
                    $"http://localhost:{_port}/json", cancellationToken);
                var targets = JsonSerializer.Deserialize<List<CdpTarget>>(targetsJson);
                var pageTarget = targets?.FirstOrDefault(t => t.Type == "page");

                if (pageTarget?.WebSocketDebuggerUrl != null)
                {
                    wsUrl = pageTarget.WebSocketDebuggerUrl;
                    break;
                }
            }
            catch
            {
                // Chrome not ready yet, retry
            }

            await Task.Delay(500, cancellationToken);
        }

        if (wsUrl == null)
        {
            throw new TimeoutException(
                $"Could not connect to Chrome DevTools on port {_port} within {timeoutSeconds} seconds. " +
                "Ensure Chrome is running with --remote-debugging-port.");
        }

        Logger.Info($"Connecting to CDP WebSocket: {wsUrl}");
        await _webSocket.ConnectAsync(new Uri(wsUrl), cancellationToken);
        Logger.Info("CDP WebSocket connected.");

        // Start receiving messages
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveTask = ReceiveLoopAsync(_receiveCts.Token);
    }

    /// <summary>
    /// Sends a CDP command and returns the message ID.
    /// </summary>
    public async Task<int> SendAsync(string method, object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref _messageId);

        var message = new Dictionary<string, object> { { "id", id }, { "method", method } };
        if (parameters != null)
        {
            message["params"] = parameters;
        }

        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);

        return id;
    }

    /// <summary>
    /// Background loop that receives CDP messages from the WebSocket.
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 64];
        var messageBuilder = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   _webSocket.State == WebSocketState.Open)
            {
                messageBuilder.Clear();
                WebSocketReceiveResult result;

                do
                {
                    result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Logger.Info("CDP WebSocket closed by server.");
                        return;
                    }

                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                var json = messageBuilder.ToString();

                try
                {
                    var response = JsonSerializer.Deserialize<CdpResponse>(json);
                    if (response != null)
                    {
                        MessageReceived?.Invoke(response);
                    }
                }
                catch (JsonException ex)
                {
                    Logger.Warning($"Failed to parse CDP message: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            Logger.Info("CDP WebSocket connection closed (Chrome likely exited).");
        }
        catch (Exception ex)
        {
            Logger.Error($"CDP receive error: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _receiveCts?.Cancel();

        if (_webSocket.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            catch
            {
                // Ignore close errors
            }
        }

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch
            {
                // Ignore
            }
        }

        _receiveCts?.Dispose();
        _webSocket.Dispose();
    }
}
