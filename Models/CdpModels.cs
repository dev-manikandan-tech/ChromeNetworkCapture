using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChromeNetworkCapture.Models;

/// <summary>
/// Models for Chrome DevTools Protocol messages and network events.
/// </summary>
public sealed class CdpMessage
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Params { get; set; }
}

public sealed class CdpResponse
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("error")]
    public JsonElement? Error { get; set; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }
}

public sealed class CdpVersionInfo
{
    [JsonPropertyName("webSocketDebuggerUrl")]
    public string WebSocketDebuggerUrl { get; set; } = "";

    [JsonPropertyName("Browser")]
    public string Browser { get; set; } = "";
}

public sealed class CdpTarget
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("webSocketDebuggerUrl")]
    public string WebSocketDebuggerUrl { get; set; } = "";
}

/// <summary>
/// Tracks the state of a single network request through its lifecycle.
/// </summary>
public sealed class NetworkRequestState
{
    public string RequestId { get; set; } = "";
    public string Url { get; set; } = "";
    public string Method { get; set; } = "";
    public string HttpVersion { get; set; } = "";
    public Dictionary<string, string> RequestHeaders { get; set; } = new();
    public Dictionary<string, string> ResponseHeaders { get; set; } = new();
    public int StatusCode { get; set; }
    public string StatusText { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long ContentLength { get; set; }
    public string RemoteIpAddress { get; set; } = "";
    public int RemotePort { get; set; }
    public string Protocol { get; set; } = "";
    public string PostData { get; set; } = "";
    public string PostMimeType { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double DnsTime { get; set; } = -1;
    public double ConnectTime { get; set; } = -1;
    public double SslTime { get; set; } = -1;
    public double SendTime { get; set; }
    public double WaitTime { get; set; }
    public double ReceiveTime { get; set; }
    public double BlockedTime { get; set; } = -1;
    public bool ResponseReceived { get; set; }
    public bool LoadingFinished { get; set; }
    public string ResponseBody { get; set; } = "";
    public string PageRef { get; set; } = "";
}
