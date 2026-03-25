using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using ChromeNetworkCapture.Models;

namespace ChromeNetworkCapture;

/// <summary>
/// Converts captured network request states into HAR 1.2 format.
/// </summary>
public static class HarGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Generates a HAR object from the captured network requests.
    /// </summary>
    public static HarRoot Generate(IReadOnlyDictionary<string, NetworkRequestState> requests)
    {
        var har = new HarRoot();

        // Collect unique page refs
        var pageRefs = new HashSet<string>();
        foreach (var req in requests.Values)
        {
            if (!string.IsNullOrEmpty(req.PageRef))
                pageRefs.Add(req.PageRef);
        }

        // Create page entries
        foreach (var pageRef in pageRefs.OrderBy(p => p))
        {
            har.Log.Pages.Add(new HarPage
            {
                Id = pageRef,
                Title = pageRef,
                StartedDateTime = requests.Values
                    .Where(r => r.PageRef == pageRef)
                    .OrderBy(r => r.StartTime)
                    .Select(r => r.StartTime.ToString("O"))
                    .FirstOrDefault() ?? DateTime.UtcNow.ToString("O"),
            });
        }

        // Create entries from captured requests
        foreach (var state in requests.Values.OrderBy(r => r.StartTime))
        {
            var entry = ConvertToHarEntry(state);
            har.Log.Entries.Add(entry);
        }

        Logger.Info($"HAR generated: {har.Log.Entries.Count} entries, {har.Log.Pages.Count} pages.");
        return har;
    }

    /// <summary>
    /// Saves the HAR data to a file.
    /// </summary>
    public static async Task SaveAsync(HarRoot har, string filePath,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(har, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        Logger.Info($"HAR file saved: {filePath} ({new FileInfo(filePath).Length:N0} bytes)");
    }

    private static HarEntry ConvertToHarEntry(NetworkRequestState state)
    {
        var totalTime = state.EndTime > state.StartTime
            ? (state.EndTime - state.StartTime).TotalMilliseconds
            : 0;

        var entry = new HarEntry
        {
            StartedDateTime = state.StartTime.ToString("O"),
            Time = totalTime,
            ServerIPAddress = state.RemoteIpAddress,
            Pageref = state.PageRef,
            Request = BuildRequest(state),
            Response = BuildResponse(state),
            Timings = new HarTimings
            {
                Blocked = state.BlockedTime,
                Dns = state.DnsTime,
                Connect = state.ConnectTime,
                Ssl = state.SslTime,
                Send = state.SendTime,
                Wait = state.WaitTime,
                Receive = Math.Max(0, totalTime - state.SendTime - state.WaitTime),
            },
        };

        return entry;
    }

    private static HarRequest BuildRequest(NetworkRequestState state)
    {
        var request = new HarRequest
        {
            Method = state.Method,
            Url = state.Url,
            HttpVersion = MapProtocol(state.Protocol),
            Headers = state.RequestHeaders
                .Select(h => new HarHeader { Name = h.Key, Value = h.Value })
                .ToList(),
            QueryString = ParseQueryString(state.Url),
        };

        if (!string.IsNullOrEmpty(state.PostData))
        {
            request.PostData = new HarPostData
            {
                MimeType = state.PostMimeType,
                Text = state.PostData,
            };
            request.BodySize = System.Text.Encoding.UTF8.GetByteCount(state.PostData);
        }

        return request;
    }

    private static HarResponse BuildResponse(NetworkRequestState state)
    {
        return new HarResponse
        {
            Status = state.StatusCode,
            StatusText = state.StatusText,
            HttpVersion = MapProtocol(state.Protocol),
            Headers = state.ResponseHeaders
                .Select(h => new HarHeader { Name = h.Key, Value = h.Value })
                .ToList(),
            Content = new HarContent
            {
                Size = state.ContentLength,
                MimeType = state.MimeType,
            },
            BodySize = state.ContentLength,
        };
    }

    private static string MapProtocol(string protocol)
    {
        return protocol switch
        {
            "h2" => "HTTP/2.0",
            "h3" => "HTTP/3.0",
            "http/1.1" => "HTTP/1.1",
            "http/1.0" => "HTTP/1.0",
            _ => string.IsNullOrEmpty(protocol) ? "HTTP/1.1" : protocol,
        };
    }

    private static List<HarQueryString> ParseQueryString(string url)
    {
        var result = new List<HarQueryString>();

        try
        {
            var uri = new Uri(url);
            var query = HttpUtility.ParseQueryString(uri.Query);

            foreach (string? key in query.AllKeys)
            {
                if (key != null)
                {
                    result.Add(new HarQueryString
                    {
                        Name = key,
                        Value = query[key] ?? "",
                    });
                }
            }
        }
        catch
        {
            // Invalid URL, skip query parsing
        }

        return result;
    }
}
