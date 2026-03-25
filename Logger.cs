namespace ChromeNetworkCapture;

/// <summary>
/// Simple file-based logger for silent background execution.
/// Writes to a log file instead of console when running as a background process.
/// </summary>
public static class Logger
{
    private static string? _logFilePath;
    private static readonly object Lock = new();
    private static bool _consoleEnabled;

    public static void Initialize(string? logFilePath = null, bool enableConsole = false)
    {
        _logFilePath = logFilePath;
        _consoleEnabled = enableConsole;

        if (!string.IsNullOrEmpty(_logFilePath))
        {
            var directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    public static void Info(string message) => Log("INFO", message);
    public static void Warning(string message) => Log("WARN", message);
    public static void Error(string message) => Log("ERROR", message);

    private static void Log(string level, string message)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = $"[{timestamp}] [{level}] {message}";

        lock (Lock)
        {
            if (_consoleEnabled)
            {
                try
                {
                    Console.WriteLine(logLine);
                }
                catch
                {
                    // Console may not be available in WinExe mode
                }
            }

            if (!string.IsNullOrEmpty(_logFilePath))
            {
                try
                {
                    File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
                }
                catch
                {
                    // Swallow file write errors to prevent crashing the capture
                }
            }
        }
    }
}
