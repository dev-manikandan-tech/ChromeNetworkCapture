namespace ChromeNetworkCapture;

/// <summary>
/// Parses and holds command-line arguments for the capture tool.
/// </summary>
public sealed class CommandLineOptions
{
    public string? ChromePath { get; private set; }
    public string? StartUrl { get; private set; }
    public string OutputPath { get; private set; } = "";
    public string? LogPath { get; private set; }
    public int Port { get; private set; } = 9222;
    public int? Duration { get; private set; }
    public bool AttachOnly { get; private set; }
    public bool ShowHelp { get; private set; }
    public bool Verbose { get; private set; }

    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--chrome-path":
                case "-c":
                    options.ChromePath = GetNextArg(args, ref i);
                    break;

                case "--url":
                case "-u":
                    options.StartUrl = GetNextArg(args, ref i);
                    break;

                case "--output":
                case "-o":
                    options.OutputPath = GetNextArg(args, ref i) ?? "";
                    break;

                case "--log":
                case "-l":
                    options.LogPath = GetNextArg(args, ref i);
                    break;

                case "--port":
                case "-p":
                    if (int.TryParse(GetNextArg(args, ref i), out var port))
                        options.Port = port;
                    break;

                case "--duration":
                case "-d":
                    if (int.TryParse(GetNextArg(args, ref i), out var duration))
                        options.Duration = duration;
                    break;

                case "--attach":
                case "-a":
                    options.AttachOnly = true;
                    break;

                case "--verbose":
                case "-v":
                    options.Verbose = true;
                    break;

                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
            }
        }

        // Default output path if not specified
        if (string.IsNullOrEmpty(options.OutputPath))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            options.OutputPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"network_capture_{timestamp}.har");
        }

        // Default log path
        options.LogPath ??= Path.Combine(
            Path.GetDirectoryName(options.OutputPath) ?? ".",
            "chrome_capture.log");

        return options;
    }

    public static void PrintHelp()
    {
        var help = """
            ChromeNetworkCapture - Capture Chrome network traffic as HAR files

            Usage:
              ChromeNetworkCapture.exe [options]

            Options:
              -c, --chrome-path <path>   Path to Chrome executable (auto-detected if omitted)
              -u, --url <url>            URL to open in Chrome (default: about:blank)
              -o, --output <path>        Output HAR file path (default: Desktop/network_capture_<timestamp>.har)
              -l, --log <path>           Log file path (default: same directory as output)
              -p, --port <port>          Chrome debugging port (default: 9222)
              -d, --duration <seconds>   Auto-stop after N seconds (default: runs until Chrome closes)
              -a, --attach               Attach to existing Chrome instance (don't launch new one)
              -v, --verbose              Enable console output (in addition to log file)
              -h, --help                 Show this help message

            Examples:
              ChromeNetworkCapture.exe
                Launch Chrome and capture all traffic until Chrome is closed.

              ChromeNetworkCapture.exe -u https://example.com -o capture.har
                Open example.com and save capture to capture.har.

              ChromeNetworkCapture.exe -d 60 -o session.har
                Capture for 60 seconds then save and exit.

              ChromeNetworkCapture.exe -a -p 9222
                Attach to an already-running Chrome with debugging on port 9222.

            Notes:
              - The .exe runs silently in the background (no console window).
              - HAR file is saved when Chrome closes, duration expires, or the process is stopped.
              - All activity is logged to the log file for troubleshooting.
            """;

        try
        {
            Console.WriteLine(help);
        }
        catch
        {
            // Console may not be available
        }
    }

    private static string? GetNextArg(string[] args, ref int index)
    {
        if (index + 1 < args.Length)
        {
            index++;
            return args[index];
        }
        return null;
    }
}
