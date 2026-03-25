# ChromeNetworkCapture

A .NET 8 tool that launches Chrome, captures all network traffic via the Chrome DevTools Protocol (CDP), and saves it as a HAR (HTTP Archive) file. Runs silently in the background as a `.exe` with no console window.

## Features

- Launches Chrome with remote debugging enabled
- Captures all network requests/responses via CDP WebSocket
- Generates HAR 1.2 compliant files
- Runs silently in the background (no console window on Windows)
- Supports attaching to an already-running Chrome instance
- Auto-stop after a configurable duration
- Cross-platform Chrome detection (Windows, macOS, Linux)
- Single-file `.exe` publishing (self-contained, no .NET runtime needed)

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for building)
- Google Chrome or Chromium-based browser installed

## Build

```bash
# Restore and build
dotnet restore
dotnet build

# Publish as a single-file .exe for Windows (runs silently, no console)
dotnet publish -c Release -r win-x64 --self-contained -o ./publish

# Publish for Linux
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish

# Publish for macOS
dotnet publish -c Release -r osx-x64 --self-contained -o ./publish
```

## Usage

```bash
# Basic: Launch Chrome and capture until it closes
ChromeNetworkCapture.exe

# Open a specific URL
ChromeNetworkCapture.exe -u https://example.com -o capture.har

# Capture for 60 seconds then auto-save
ChromeNetworkCapture.exe -d 60 -o session.har

# Attach to an already-running Chrome (launched with --remote-debugging-port=9222)
ChromeNetworkCapture.exe -a -p 9222

# Verbose mode (also prints to console)
ChromeNetworkCapture.exe -v -u https://example.com

# Custom Chrome path
ChromeNetworkCapture.exe -c "C:\Program Files\Google\Chrome\Application\chrome.exe"
```

## Command-Line Options

| Option | Short | Description | Default |
|---|---|---|---|
| `--chrome-path` | `-c` | Path to Chrome executable | Auto-detected |
| `--url` | `-u` | URL to open in Chrome | `about:blank` |
| `--output` | `-o` | Output HAR file path | `Desktop/network_capture_<timestamp>.har` |
| `--log` | `-l` | Log file path | Same directory as output |
| `--port` | `-p` | Chrome debugging port | `9222` |
| `--duration` | `-d` | Auto-stop after N seconds | Runs until Chrome closes |
| `--attach` | `-a` | Attach to existing Chrome | Launches new Chrome |
| `--verbose` | `-v` | Enable console output | File logging only |
| `--help` | `-h` | Show help message | |

## How It Works

1. **Chrome Launch**: Starts Chrome with `--remote-debugging-port` to enable the DevTools Protocol
2. **CDP Connection**: Connects to Chrome's WebSocket debugging endpoint
3. **Network Capture**: Enables the `Network` and `Page` CDP domains to receive all network events
4. **Event Processing**: Tracks each request through its lifecycle (request sent -> response received -> loading finished)
5. **HAR Generation**: Converts captured network states into HAR 1.2 format with full timing data
6. **Save**: Writes the HAR file when Chrome closes, duration expires, or the process receives a stop signal

## Silent Background Execution

The project is configured with `<OutputType>WinExe</OutputType>`, which means:
- On Windows: No console window is shown when the `.exe` runs
- The tool operates entirely in the background
- All output goes to the log file (check `--log` path)
- To stop capture: close Chrome, wait for duration, or terminate the process

## Output

The generated `.har` file can be opened in:
- Chrome DevTools (Network tab -> Import HAR)
- [HAR Viewer](http://www.softwareishard.com/har/viewer/)
- Any HAR-compatible analysis tool

## Project Structure

```
ChromeNetworkCapture/
  Program.cs                 # Entry point and orchestration
  ChromeLauncher.cs          # Chrome process management
  CdpClient.cs               # CDP WebSocket communication
  NetworkCapture.cs           # Network event capture and state tracking
  HarGenerator.cs             # HAR file generation
  CommandLineOptions.cs       # CLI argument parsing
  Logger.cs                   # File-based logging
  Models/
    HarModels.cs              # HAR 1.2 data models
    CdpModels.cs              # CDP message models
```
