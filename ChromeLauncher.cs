using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChromeNetworkCapture;

/// <summary>
/// Launches Chrome with remote debugging enabled.
/// </summary>
public sealed class ChromeLauncher : IDisposable
{
    private Process? _chromeProcess;
    private readonly int _debuggingPort;
    private readonly string? _userDataDir;

    public int DebuggingPort => _debuggingPort;

    public ChromeLauncher(int debuggingPort = 9222, string? userDataDir = null)
    {
        _debuggingPort = debuggingPort;
        _userDataDir = userDataDir;
    }

    /// <summary>
    /// Finds the Chrome executable on the current platform.
    /// </summary>
    public static string FindChromePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string[] windowsPaths =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome Beta", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            };

            foreach (var path in windowsPaths)
            {
                if (File.Exists(path))
                    return path;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string[] linuxPaths =
            {
                "/usr/bin/google-chrome",
                "/usr/bin/google-chrome-stable",
                "/usr/bin/chromium-browser",
                "/usr/bin/chromium",
                "/snap/bin/chromium",
            };

            foreach (var path in linuxPaths)
            {
                if (File.Exists(path))
                    return path;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string[] macPaths =
            {
                "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
                "/Applications/Chromium.app/Contents/MacOS/Chromium",
                "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
            };

            foreach (var path in macPaths)
            {
                if (File.Exists(path))
                    return path;
            }
        }

        throw new FileNotFoundException(
            "Chrome executable not found. Please install Google Chrome or specify the path using --chrome-path.");
    }

    /// <summary>
    /// Launches Chrome with remote debugging and returns the process.
    /// </summary>
    public Process Launch(string? chromePath = null, string? startUrl = null)
    {
        chromePath ??= FindChromePath();
        startUrl ??= "about:blank";

        var userDataDir = _userDataDir ?? Path.Combine(Path.GetTempPath(), $"chrome-capture-{_debuggingPort}");

        var arguments = string.Join(" ", new[]
        {
            $"--remote-debugging-port={_debuggingPort}",
            $"--user-data-dir=\"{userDataDir}\"",
            "--no-first-run",
            "--no-default-browser-check",
            startUrl
        });

        var startInfo = new ProcessStartInfo
        {
            FileName = chromePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };

        _chromeProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start Chrome process.");

        Logger.Info($"Chrome launched (PID: {_chromeProcess.Id}) on debugging port {_debuggingPort}");

        return _chromeProcess;
    }

    /// <summary>
    /// Attaches to an already-running Chrome instance on the specified debugging port.
    /// Does not launch a new process.
    /// </summary>
    public void AttachToExisting()
    {
        Logger.Info($"Attaching to existing Chrome on debugging port {_debuggingPort}");
    }

    /// <summary>
    /// Checks if the Chrome process is still running.
    /// </summary>
    public bool IsRunning => _chromeProcess != null && !_chromeProcess.HasExited;

    /// <summary>
    /// Waits for the Chrome process to exit.
    /// </summary>
    public async Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        if (_chromeProcess != null)
        {
            await _chromeProcess.WaitForExitAsync(cancellationToken);
        }
    }

    public void Dispose()
    {
        if (_chromeProcess != null && !_chromeProcess.HasExited)
        {
            try
            {
                _chromeProcess.Kill(entireProcessTree: true);
                _chromeProcess.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error killing Chrome process: {ex.Message}");
            }
        }

        _chromeProcess?.Dispose();
    }
}
