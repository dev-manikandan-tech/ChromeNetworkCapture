using System.Collections.Concurrent;
using System.Text.Json;
using ChromeNetworkCapture.Models;

namespace ChromeNetworkCapture;

/// <summary>
/// Captures network events from Chrome via CDP and stores request/response state.
/// Uses Target.setAutoAttach with flatten mode to capture traffic from all tabs/pages.
/// </summary>
public sealed class NetworkCapture
{
    private readonly CdpClient _cdpClient;
    private readonly ConcurrentDictionary<string, NetworkRequestState> _requests = new();
    private readonly ConcurrentDictionary<string, bool> _attachedSessions = new();
    private readonly object _lock = new();
    private int _pageCount;
    private CancellationToken _cancellationToken;

    public string CurrentPageRef { get; private set; } = "page_0";
    public IReadOnlyDictionary<string, NetworkRequestState> Requests => _requests;

    public NetworkCapture(CdpClient cdpClient)
    {
        _cdpClient = cdpClient;
        _cdpClient.MessageReceived += OnCdpMessage;
    }

    /// <summary>
    /// Enables auto-attach to all page targets and starts capturing network traffic.
    /// Uses the browser-level connection with Target.setAutoAttach (flatten: true)
    /// so that Network.enable is sent to each page session individually.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;

        // Auto-attach to all existing and future page targets with flattened sessions
        await _cdpClient.SendAsync("Target.setAutoAttach", new
        {
            autoAttach = true,
            waitForDebuggerOnStart = false,
            flatten = true
        }, cancellationToken);

        // Also discover existing targets to attach to pages already open
        await _cdpClient.SendAsync("Target.setDiscoverTargets", new
        {
            discover = true
        }, cancellationToken);

        Logger.Info("Network capture started. Auto-attaching to all page targets...");
    }

    /// <summary>
    /// Stops capturing by disabling the Network domain on all attached sessions.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sessionId in _attachedSessions.Keys)
        {
            try
            {
                await _cdpClient.SendAsync("Network.disable",
                    cancellationToken: cancellationToken, sessionId: sessionId);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error disabling network on session {sessionId}: {ex.Message}");
            }
        }
        Logger.Info($"Network capture stopped. Captured {_requests.Count} requests.");
    }

    /// <summary>
    /// Enables Network and Page domains on a newly attached page target session.
    /// </summary>
    private async Task EnableNetworkOnSessionAsync(string sessionId)
    {
        try
        {
            await _cdpClient.SendAsync("Network.enable", new
            {
                maxTotalBufferSize = 100 * 1024 * 1024,
                maxResourceBufferSize = 50 * 1024 * 1024
            }, _cancellationToken, sessionId);

            await _cdpClient.SendAsync("Page.enable",
                cancellationToken: _cancellationToken, sessionId: sessionId);

            _attachedSessions.TryAdd(sessionId, true);
            Logger.Info($"Network capture enabled on session: {sessionId}");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to enable network on session {sessionId}: {ex.Message}");
        }
    }

    private void OnCdpMessage(CdpResponse message)
    {
        if (message.Method == null || message.Params == null)
            return;

        try
        {
            switch (message.Method)
            {
                case "Target.attachedToTarget":
                    HandleTargetAttached(message.Params.Value);
                    break;
                case "Target.detachedFromTarget":
                    HandleTargetDetached(message.Params.Value);
                    break;
                case "Network.requestWillBeSent":
                    HandleRequestWillBeSent(message.Params.Value);
                    break;
                case "Network.responseReceived":
                    HandleResponseReceived(message.Params.Value);
                    break;
                case "Network.loadingFinished":
                    HandleLoadingFinished(message.Params.Value);
                    break;
                case "Network.loadingFailed":
                    HandleLoadingFailed(message.Params.Value);
                    break;
                case "Network.dataReceived":
                    HandleDataReceived(message.Params.Value);
                    break;
                case "Page.frameNavigated":
                    HandleFrameNavigated(message.Params.Value);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Error handling {message.Method}: {ex.Message}");
        }
    }

    private void HandleTargetAttached(JsonElement parameters)
    {
        var sessionId = parameters.TryGetProperty("sessionId", out var sid)
            ? sid.GetString() : null;

        if (string.IsNullOrEmpty(sessionId))
            return;

        if (parameters.TryGetProperty("targetInfo", out var targetInfo) &&
            targetInfo.TryGetProperty("type", out var type) &&
            type.GetString() == "page")
        {
            var url = targetInfo.TryGetProperty("url", out var u) ? u.GetString() : "";
            Logger.Info($"Page target attached (session: {sessionId}, url: {url})");
            _ = EnableNetworkOnSessionAsync(sessionId);
        }
    }

    private void HandleTargetDetached(JsonElement parameters)
    {
        var sessionId = parameters.TryGetProperty("sessionId", out var sid)
            ? sid.GetString() : null;

        if (!string.IsNullOrEmpty(sessionId))
        {
            _attachedSessions.TryRemove(sessionId, out _);
            Logger.Info($"Target session detached: {sessionId}");
        }
    }

    private void HandleRequestWillBeSent(JsonElement parameters)
    {
        var requestId = parameters.GetProperty("requestId").GetString() ?? "";
        var request = parameters.GetProperty("request");
        var url = request.GetProperty("url").GetString() ?? "";
        var method = request.GetProperty("method").GetString() ?? "";

        var state = new NetworkRequestState
        {
            RequestId = requestId,
            Url = url,
            Method = method,
            StartTime = DateTime.UtcNow,
            PageRef = CurrentPageRef,
        };

        // Extract request headers
        if (request.TryGetProperty("headers", out var headersElement))
        {
            foreach (var header in headersElement.EnumerateObject())
            {
                state.RequestHeaders[header.Name] = header.Value.GetString() ?? "";
            }
        }

        // Extract post data if present
        if (request.TryGetProperty("postData", out var postData))
        {
            state.PostData = postData.GetString() ?? "";
        }

        if (request.TryGetProperty("headers", out var reqHeaders) &&
            reqHeaders.TryGetProperty("Content-Type", out var contentType))
        {
            state.PostMimeType = contentType.GetString() ?? "";
        }

        _requests.AddOrUpdate(requestId, state, (_, existing) =>
        {
            // Handle redirects: update with new request info
            existing.Url = url;
            existing.Method = method;
            existing.RequestHeaders = state.RequestHeaders;
            return existing;
        });
    }

    private void HandleResponseReceived(JsonElement parameters)
    {
        var requestId = parameters.GetProperty("requestId").GetString() ?? "";

        if (!_requests.TryGetValue(requestId, out var state))
            return;

        var response = parameters.GetProperty("response");

        state.StatusCode = response.GetProperty("status").GetInt32();
        state.StatusText = response.GetProperty("statusText").GetString() ?? "";
        state.MimeType = response.TryGetProperty("mimeType", out var mime)
            ? mime.GetString() ?? "" : "";

        if (response.TryGetProperty("protocol", out var protocol))
        {
            state.Protocol = protocol.GetString() ?? "";
        }

        if (response.TryGetProperty("remoteIPAddress", out var ip))
        {
            state.RemoteIpAddress = ip.GetString() ?? "";
        }

        if (response.TryGetProperty("remotePort", out var port))
        {
            state.RemotePort = port.GetInt32();
        }

        // Extract response headers
        if (response.TryGetProperty("headers", out var headersElement))
        {
            foreach (var header in headersElement.EnumerateObject())
            {
                state.ResponseHeaders[header.Name] = header.Value.GetString() ?? "";
            }
        }

        // Extract timing information
        if (response.TryGetProperty("timing", out var timing))
        {
            if (timing.TryGetProperty("dnsStart", out var dnsStart) &&
                timing.TryGetProperty("dnsEnd", out var dnsEnd))
            {
                var ds = dnsStart.GetDouble();
                var de = dnsEnd.GetDouble();
                if (ds >= 0 && de >= 0)
                    state.DnsTime = de - ds;
            }

            if (timing.TryGetProperty("connectStart", out var connectStart) &&
                timing.TryGetProperty("connectEnd", out var connectEnd))
            {
                var cs = connectStart.GetDouble();
                var ce = connectEnd.GetDouble();
                if (cs >= 0 && ce >= 0)
                    state.ConnectTime = ce - cs;
            }

            if (timing.TryGetProperty("sslStart", out var sslStart) &&
                timing.TryGetProperty("sslEnd", out var sslEnd))
            {
                var ss = sslStart.GetDouble();
                var se = sslEnd.GetDouble();
                if (ss >= 0 && se >= 0)
                    state.SslTime = se - ss;
            }

            if (timing.TryGetProperty("sendStart", out var sendStart) &&
                timing.TryGetProperty("sendEnd", out var sendEnd))
            {
                state.SendTime = Math.Max(0, sendEnd.GetDouble() - sendStart.GetDouble());
            }

            if (timing.TryGetProperty("sendEnd", out var se2) &&
                timing.TryGetProperty("receiveHeadersEnd", out var receiveHeadersEnd))
            {
                state.WaitTime = Math.Max(0, receiveHeadersEnd.GetDouble() - se2.GetDouble());
            }
        }

        state.ResponseReceived = true;
    }

    private void HandleLoadingFinished(JsonElement parameters)
    {
        var requestId = parameters.GetProperty("requestId").GetString() ?? "";

        if (!_requests.TryGetValue(requestId, out var state))
            return;

        state.EndTime = DateTime.UtcNow;
        state.LoadingFinished = true;

        if (parameters.TryGetProperty("encodedDataLength", out var dataLength))
        {
            state.ContentLength = dataLength.GetInt64();
        }
    }

    private void HandleLoadingFailed(JsonElement parameters)
    {
        var requestId = parameters.GetProperty("requestId").GetString() ?? "";

        if (!_requests.TryGetValue(requestId, out var state))
            return;

        state.EndTime = DateTime.UtcNow;
        state.LoadingFinished = true;

        if (!state.ResponseReceived)
        {
            state.StatusCode = 0;
            state.StatusText = parameters.TryGetProperty("errorText", out var err)
                ? err.GetString() ?? "Failed" : "Failed";
        }
    }

    private void HandleDataReceived(JsonElement parameters)
    {
        var requestId = parameters.GetProperty("requestId").GetString() ?? "";

        if (!_requests.TryGetValue(requestId, out var state))
            return;

        if (parameters.TryGetProperty("dataLength", out var dataLength))
        {
            state.ReceiveTime += 1; // approximate, as precise timing needs wallTime
        }
    }

    private void HandleFrameNavigated(JsonElement parameters)
    {
        if (parameters.TryGetProperty("frame", out var frame) &&
            frame.TryGetProperty("parentId", out _))
        {
            // Skip sub-frame navigations
            return;
        }

        lock (_lock)
        {
            _pageCount++;
            CurrentPageRef = $"page_{_pageCount}";
        }
    }
}
