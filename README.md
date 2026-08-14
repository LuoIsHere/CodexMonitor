# CodexMonitor

CodexMonitor is a small personal Windows utility that displays the current Codex usage limits in a regular WPF window. It shows the short-period quota, weekly quota, reset countdowns, last refresh time, and current read status.

Current version: `0.2.0`

## Features

- Shows the remaining percentage and reset countdown for the approximately five-hour quota window (`5H`).
- Shows the remaining percentage and reset countdown for the weekly quota window (`WK`).
- Shows the latest successful refresh time (`REF`) and read status.
- Reads data once at startup, then refreshes every three minutes by default.
- Uses a one-second UI timer only to update local countdowns; it does not query Codex every second.
- Supports an immediate manual refresh.
- Keeps the latest valid snapshot when a later refresh fails.
- Remains active in the Windows notification area after the main window is closed.
- Restores the main window with a left click on the notification-area icon.
- Provides notification-area commands to open the window, refresh immediately, or exit the application.

Version 0.2.0 does not include taskbar docking, a settings UI, an installer, automatic startup, or automatic updates.

## System requirements

- Windows 10 version 1809 or later; Windows 11 is recommended.
- x64 system.
- Codex for Windows installed and signed in.
- The framework-dependent package requires the .NET 10 Desktop Runtime x64. The self-contained package does not require a separately installed runtime.

## Tested Codex version

CodexMonitor 0.2.0 has been verified with:

- Codex for Windows: `codex-cli 0.147.0-alpha.6.6`

Other Codex versions may work, but the local app-server protocol can change between releases.

## Usage

1. Download either single EXE, or extract either ZIP package.
2. Run the downloaded single EXE directly, or run `CodexMonitor.exe` from the extracted ZIP.
3. The application locates the executable copy supplied by Codex for Windows, preferring `%LOCALAPPDATA%\OpenAI\Codex\bin`, and reads the current limits.
4. Select **Refresh now** to request fresh data immediately.

Closing the window hides it in the Windows notification area while background quota refreshes continue. Left-click the notification-area icon to restore the window. Use **Exit** from the icon's context menu to stop background work and exit the application.

## Settings

The first run creates:

```text
%LOCALAPPDATA%\CodexMonitor\settings.json
```

Version 0.2.0 has no settings UI. Exit the application and edit the file manually:

```json
{
  "schemaVersion": 1,
  "refreshIntervalMinutes": 3,
  "codexExecutable": null
}
```

- `refreshIntervalMinutes`: 1 to 60 minutes. Values outside this range are clamped automatically.
- `codexExecutable`: optional absolute path to `codex.exe`; normally leave this as `null`.

Restart the application after changing settings.

## Implementation

The application uses .NET 10 and WPF and is divided into three main projects:

- `CodexMonitor.Core`: quota models, display formatting, and refresh state management.
- `CodexMonitor.Infrastructure`: Codex process communication, JSON parsing, settings, and logging.
- `CodexMonitor.App`: the WPF window, notification-area lifecycle, background refresh ownership, and one-second countdown updates.

Quota reads start the local process:

```text
codex.exe app-server --listen stdio://
```

The application then initializes the line-delimited JSON protocol over standard input/output, sends the `initialized` notification, and calls `account/rateLimits/read`. It identifies the approximately 300-minute and 10,080-minute windows by `windowDurationMins` instead of relying on a fixed `primary`/`secondary` order.

This app-server interface has no stability guarantee for this project. A future Codex release may change method names or response fields. The integration is isolated in `CodexAppServerQuotaProvider` and `RateLimitResponseParser` so it can be maintained independently from the UI and application state.

## Permission and privacy boundary

The application:

- Runs with the current standard user's permissions and does not request administrator access.
- Does not read the Codex `auth.json` file or login tokens.
- Does not send quota, account, or device data to third parties.
- Starts a local Codex app-server child process and closes it after the read completes.
- Keeps its own user-level process running while the main window is hidden in the notification area.
- Writes only its local settings and log directory.
- Has no telemetry, automatic update, browser access, or remote-control feature.

Logs are stored at:

```text
%LOCALAPPDATA%\CodexMonitor\logs\codex-monitor.log
```

Logs contain refresh results and compact error messages, not complete protocol responses or tokens. Local error text may still contain machine-specific information, so inspect logs before sharing them publicly.

## Build and test

.NET 10 SDK is required:

```powershell
dotnet build .\CodexMonitor.sln -c Release
dotnet run --project .\tests\CodexMonitor.Tests\CodexMonitor.Tests.csproj -c Release
```

The project has no third-party NuGet dependencies. The repository-level `NuGet.Config` disables external package sources so the basic build can complete offline.

Run the live Codex probe with:

```powershell
dotnet run --project .\tests\CodexMonitor.Tests\CodexMonitor.Tests.csproj -c Release -- --live
```

Create release artifacts with Windows PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\Publish.ps1
```

The first self-contained publish may download Microsoft runtime packs from NuGet.org. Application code has no third-party package dependencies.

The script writes these packages to `artifacts/`:

- Self-contained single EXE.
- Framework-dependent single EXE; requires the .NET 10 Desktop Runtime x64.
- Self-contained ZIP.
- Framework-dependent ZIP.

## Inspiration

The initial product ideas were inspired by:

- [DiMY-CN/CodexQuotaMonitor](https://github.com/DiMY-CN/CodexQuotaMonitor): presenting Codex quota windows and obtaining quota data through the local Codex app-server.
- [zhongyang219/TrafficMonitor](https://github.com/zhongyang219/TrafficMonitor): the concept of a lightweight, readily accessible Windows monitor.

## License

[MIT](LICENSE)

Copyright (c) 2026 luoishere
