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

    // Set up duration-based auto-stop if specified
    if (options.Duration.HasValue)
    {
        Logger.Info($"Will auto-stop after {options.Duration.Value} seconds.");
        cts.CancelAfter(TimeSpan.FromSeconds(options.Duration.Value));
    }

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
            Logger.Info("Capture period ended.");
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
            Logger.Info("Capture period ended.");
        }
    }

    // Stop capture and generate HAR
    try
    {
        await capture.StopAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
        Logger.Warning($"Error stopping capture (Chrome may have already closed): {ex.Message}");
    }

    var har = HarGenerator.Generate(capture.Requests);
    await HarGenerator.SaveAsync(har, options.OutputPath, CancellationToken.None);

    Logger.Info("ChromeNetworkCapture completed successfully.");
}
catch (Exception ex)
{
    Logger.Error($"Fatal error: {ex}");
    Environment.ExitCode = 1;
}
