using ChromeNetworkCapture;

var options = CommandLineOptions.Parse(args);

if (options.ShowHelp)
{
    CommandLineOptions.PrintHelp();
    return;
}

// Initialize logger (writes to file; optionally to console with --verbose)
Logger.Initialize(options.LogPath, enableConsole: options.Verbose);
Logger.Info("ChromeNetworkCapture starting...");
Logger.Info($"Output HAR: {options.OutputPath}");
Logger.Info($"Debugging port: {options.Port}");
Logger.Info($"Flush interval: {options.FlushInterval}s");

using var cts = new CancellationTokenSource();

// Handle graceful shutdown via Ctrl+C or process termination
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Logger.Info("Shutdown signal received.");
    cts.Cancel();
};

AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    if (!cts.IsCancellationRequested)
        cts.Cancel();
};

try
{
    using var launcher = new ChromeLauncher(options.Port);
    await using var cdpClient = new CdpClient(options.Port);

    // Either launch Chrome or attach to an existing instance
    if (!options.AttachOnly)
    {
        launcher.Launch(options.ChromePath, options.StartUrl);
    }
    else
    {
        launcher.AttachToExisting();
        Logger.Info($"Attach mode: connecting to existing Chrome on port {options.Port}");
    }

    // Connect to Chrome DevTools Protocol
    await cdpClient.ConnectAsync(timeoutSeconds: 30, cts.Token);

    // Start capturing network events
    var capture = new NetworkCapture(cdpClient);
    await capture.StartAsync(cts.Token);

    // Start the incremental HAR flush loop in the background
    var flushTask = FlushLoopAsync(capture, options.OutputPath, options.FlushInterval, cts.Token);

    // Wait until Chrome exits or cancellation is triggered
    if (!options.AttachOnly && launcher.IsRunning)
    {
        Logger.Info("Waiting for Chrome to close...");
        try
        {
            await launcher.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Logger.Info("Capture stopped.");
        }
    }
    else
    {
        // In attach mode, just wait for cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            Logger.Info("Capture stopped.");
        }
    }

    // Cancel the flush loop
    cts.Cancel();
    try { await flushTask; } catch (OperationCanceledException) { }

    // Stop capture
    try
    {
        await capture.StopAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
        Logger.Warning($"Error stopping capture (Chrome may have already closed): {ex.Message}");
    }

    // Final flush to ensure all captured data is saved
    var har = HarGenerator.Generate(capture.Requests);
    await HarGenerator.SaveAsync(har, options.OutputPath, CancellationToken.None);

    Logger.Info("ChromeNetworkCapture completed successfully.");
}
catch (Exception ex)
{
    Logger.Error($"Fatal error: {ex}");
    Environment.ExitCode = 1;
}

/// <summary>
/// Periodically generates and saves the HAR file while capture is running.
/// This ensures the HAR file is always up-to-date on disk, even if Chrome
/// crashes or the process is killed unexpectedly.
/// </summary>
static async Task FlushLoopAsync(NetworkCapture capture, string outputPath,
    int intervalSeconds, CancellationToken cancellationToken)
{
    Logger.Info($"Incremental HAR flush started (every {intervalSeconds}s to {outputPath})");
    var lastCount = 0;

    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            break;
        }

        var currentCount = capture.Requests.Count;
        if (currentCount == lastCount)
            continue; // No new requests, skip flush

        try
        {
            var har = HarGenerator.Generate(capture.Requests);
            await HarGenerator.SaveAsync(har, outputPath, CancellationToken.None);
            Logger.Info($"Incremental flush: {currentCount} requests saved.");
            lastCount = currentCount;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Incremental flush failed: {ex.Message}");
        }
    }
}
