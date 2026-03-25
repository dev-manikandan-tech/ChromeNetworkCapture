using System.Text.Json.Serialization;

namespace ChromeNetworkCapture.Models;

/// <summary>
/// Root HAR object following the HAR 1.2 specification.
/// </summary>
public sealed class HarRoot
{
    [JsonPropertyName("log")]
    public HarLog Log { get; set; } = new();
}

public sealed class HarLog
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.2";

    [JsonPropertyName("creator")]
    public HarCreator Creator { get; set; } = new();

    [JsonPropertyName("entries")]
    public List<HarEntry> Entries { get; set; } = new();

    [JsonPropertyName("pages")]
    public List<HarPage> Pages { get; set; } = new();
}

public sealed class HarCreator
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "ChromeNetworkCapture";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";
}

public sealed class HarPage
{
    [JsonPropertyName("startedDateTime")]
    public string StartedDateTime { get; set; } = "";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("pageTimings")]
    public HarPageTimings PageTimings { get; set; } = new();
}

public sealed class HarPageTimings
{
    [JsonPropertyName("onContentLoad")]
    public double OnContentLoad { get; set; } = -1;

    [JsonPropertyName("onLoad")]
    public double OnLoad { get; set; } = -1;
}

public sealed class HarEntry
{
    [JsonPropertyName("startedDateTime")]
    public string StartedDateTime { get; set; } = "";

    [JsonPropertyName("time")]
    public double Time { get; set; }

    [JsonPropertyName("request")]
    public HarRequest Request { get; set; } = new();

    [JsonPropertyName("response")]
    public HarResponse Response { get; set; } = new();

    [JsonPropertyName("cache")]
    public HarCache Cache { get; set; } = new();

    [JsonPropertyName("timings")]
    public HarTimings Timings { get; set; } = new();

    [JsonPropertyName("serverIPAddress")]
    public string ServerIPAddress { get; set; } = "";

    [JsonPropertyName("pageref")]
    public string Pageref { get; set; } = "";
}

public sealed class HarRequest
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("httpVersion")]
    public string HttpVersion { get; set; } = "";

    [JsonPropertyName("cookies")]
    public List<HarCookie> Cookies { get; set; } = new();

    [JsonPropertyName("headers")]
    public List<HarHeader> Headers { get; set; } = new();

    [JsonPropertyName("queryString")]
    public List<HarQueryString> QueryString { get; set; } = new();

    [JsonPropertyName("headersSize")]
    public long HeadersSize { get; set; } = -1;

    [JsonPropertyName("bodySize")]
    public long BodySize { get; set; } = -1;

    [JsonPropertyName("postData")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HarPostData? PostData { get; set; }
}

public sealed class HarResponse
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("statusText")]
    public string StatusText { get; set; } = "";

    [JsonPropertyName("httpVersion")]
    public string HttpVersion { get; set; } = "";

    [JsonPropertyName("cookies")]
    public List<HarCookie> Cookies { get; set; } = new();

    [JsonPropertyName("headers")]
    public List<HarHeader> Headers { get; set; } = new();

    [JsonPropertyName("content")]
    public HarContent Content { get; set; } = new();

    [JsonPropertyName("redirectURL")]
    public string RedirectURL { get; set; } = "";

    [JsonPropertyName("headersSize")]
    public long HeadersSize { get; set; } = -1;

    [JsonPropertyName("bodySize")]
    public long BodySize { get; set; } = -1;
}

public sealed class HarContent
{
    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "";

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("compression")]
    public long Compression { get; set; }
}

public sealed class HarHeader
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

public sealed class HarCookie
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

public sealed class HarQueryString
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

public sealed class HarPostData
{
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

public sealed class HarCache { }

public sealed class HarTimings
{
    [JsonPropertyName("blocked")]
    public double Blocked { get; set; } = -1;

    [JsonPropertyName("dns")]
    public double Dns { get; set; } = -1;

    [JsonPropertyName("connect")]
    public double Connect { get; set; } = -1;

    [JsonPropertyName("send")]
    public double Send { get; set; } = 0;

    [JsonPropertyName("wait")]
    public double Wait { get; set; } = 0;

    [JsonPropertyName("receive")]
    public double Receive { get; set; } = 0;

    [JsonPropertyName("ssl")]
    public double Ssl { get; set; } = -1;
}
