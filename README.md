# CodexMonitor

CodexMonitor is a small personal Windows utility that displays current Codex usage limits in a compact WPF window. It shows the short-period quota, weekly quota, quota reset times, subscription type, last refresh time, and current read status.

Current version: `0.2.3`

![CodexMonitor dashboard showing Codex quota usage](assets/screenshots/codex-monitor-dashboard.png)

## Features

- Shows the remaining percentage for the approximately five-hour quota window (`5H`).
- Shows the remaining percentage for the seven-day quota window (`7D`).
- Shows the local reset timestamps for both quota windows.
- Shows the latest successful refresh time and read status.
- Shows the active ChatGPT subscription plan or token/API-key login type.
- Uses native Windows Acrylic when transparency effects are enabled and a dark translucent fallback otherwise.
- Displays `None` for quota values and refresh time, and `-` for reset times, when Codex uses token/API-key authentication.
- Reads data once at startup, then refreshes every three minutes by default.
- Provides a settings window for notification behavior, a 1-to-60-minute refresh interval, and visible data sections.
- Supports an immediate manual refresh.
- Keeps the latest valid snapshot when a later refresh fails.
- Remains active in the Windows notification area after the main window is closed.
- Restores the main window with a left click on the notification-area icon.
- Provides a dark notification-area menu for minimizing, opening settings, and exiting the application.
- Prevents duplicate background instances; launching the app again restores the already running window.

Version 0.2.3 reserves a floating-window interface but does not yet implement the floating window. It also does not include taskbar docking, an installer, automatic startup, or automatic updates.

## System requirements

- Windows 10 version 1809 or later; Windows 11 is recommended.
- x64 system.
- Codex for Windows installed and signed in.
- The framework-dependent package requires the .NET 10 Desktop Runtime x64. The self-contained package does not require a separately installed runtime.

## Tested Codex version

CodexMonitor 0.2.3 has been verified with:

- Codex for Windows: `codex-cli 0.147.0-alpha.6.6`

Other Codex versions may work, but the local app-server protocol can change between releases.

## Usage

1. Download either single EXE, or extract either ZIP package.
2. Run the downloaded single EXE directly, or run `CodexMonitor.exe` from the extracted ZIP.
3. The application locates the executable copy supplied by Codex for Windows, preferring `%LOCALAPPDATA%\OpenAI\Codex\bin`, and reads the current limits.
4. Select **Refresh now** to request fresh data immediately.

Closing the window hides it in the Windows notification area while background quota refreshes continue. Left-click the notification-area icon to restore the window. Right-click the icon to minimize the visible window, open **Settings**, or use **Exit application** to stop background work and exit. The floating-window item is intentionally unavailable in this version.

If CodexMonitor is already running, launching it again activates the existing window instead of starting another monitor process.

## Settings

The first run creates:

```text
%LOCALAPPDATA%\CodexMonitor\settings.json
```

Open **Settings** from the notification-area menu to change notifications, the automatic refresh interval, and visible data sections. The same values are stored in:

```json
{
  "schemaVersion": 2,
  "refreshIntervalMinutes": 3,
  "codexExecutable": null,
  "notifications": {
    "enabled": true
  },
  "display": {
    "showFiveHourQuota": true,
    "showWeeklyQuota": true,
    "showResetTimes": true,
    "showSubscription": true
  }
}
```

- `refreshIntervalMinutes`: 1 to 60 minutes. Changes made in the settings window take effect immediately and restart the interval from the save time without forcing an immediate quota request.
- `codexExecutable`: optional absolute path to `codex.exe`; normally leave this as `null`.
- `notifications.enabled`: controls all notification balloons; refresh errors are still logged when disabled.
- `display`: controls the main-window data sections. Refresh time and status always remain visible.

Schema 1 files remain compatible. If the JSON file is edited manually, restart the application to reload it.

## Implementation

The application uses .NET 10 and WPF and is divided into three main projects:

- `CodexMonitor.Core`: quota models, display formatting, and refresh state management.
- `CodexMonitor.Infrastructure`: Codex process communication, JSON parsing, settings, and logging.
- `CodexMonitor.App`: WPF windows, notification-area lifecycle, runtime settings, single-instance activation, and background refresh scheduling.

Quota reads start the local process:

```text
codex.exe app-server --listen stdio://
```

The application then initializes the line-delimited JSON protocol over standard input/output and sends the `initialized` notification. It calls `account/read` with token refresh disabled to obtain non-secret account type and plan metadata. ChatGPT logins continue with `account/rateLimits/read`; token, API-key, signed-out, and other non-ChatGPT account types do not request ChatGPT quota data. Quota parsing identifies the approximately 300-minute and 10,080-minute windows by `windowDurationMins` instead of relying on a fixed `primary`/`secondary` order.

This app-server interface has no stability guarantee for this project. A future Codex release may change method names or response fields. The integration is isolated in `CodexAppServerQuotaProvider` and `RateLimitResponseParser` so it can be maintained independently from the UI and application state.

## Permission and privacy boundary

The application:

- Runs with the current standard user's permissions and does not request administrator access.
- Does not directly open, read, or parse the Codex `auth.json` file, API-key files, token files, or the operating system credential store.
- Does not receive, save, display, or log API keys, access tokens, refresh tokens, or other authentication secrets.
- Delegates authentication entirely to the local Codex app-server and reads only the non-secret account type and subscription plan fields needed for display.
- The Codex app-server may access credentials it manages as part of normal Codex operation; CodexMonitor neither requests nor receives those credential values.
- Ignores account email addresses returned by the app-server and does not display, save, or log them.
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

## Changelog

The root [CHANGELOG.md](CHANGELOG.md) contains only the current release. For the complete history, see [src/CHANGELOG.md](src/CHANGELOG.md) or [GitHub Releases](https://github.com/LuoIsHere/CodexMonitor/releases).

## License

[MIT](LICENSE)

Copyright (c) 2026 luoishere
