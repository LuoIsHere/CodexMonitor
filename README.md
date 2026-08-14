# CodexMonitor

CodexMonitor is a small personal Windows utility that displays current Codex usage limits in a compact WPF window. It shows the short-period quota, weekly quota, quota reset times, subscription type, last refresh time, and current read status.

Current version: `0.3.0`

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
- Provides a compact, rounded, translucent floating window that reuses the main refresh state without making extra quota requests.
- Lets the floating window show independently selected quota, refresh-time, reset-time, and subscription fields.
- Supports an unlocked draggable mode and a locked click-through, always-on-top mode.

Version 0.3.0 does not include taskbar docking, an installer, automatic startup, or automatic updates.

## System requirements

- Windows 10 version 1809 or later; Windows 11 is recommended.
- x64 system.
- Codex for Windows installed and signed in.
- The framework-dependent packages require the **.NET 10 Desktop Runtime x64**. Installing only the base .NET Runtime or ASP.NET Core Runtime is not sufficient for this WPF application.
- The self-contained packages include the required .NET 10 desktop runtime components and do not require a separately installed .NET runtime or SDK.

## Release packages and .NET 10

CodexMonitor targets `net10.0-windows` and uses WPF. End users only need the .NET 10 Desktop Runtime x64 when choosing a framework-dependent package. The .NET 10 SDK is required to build the source code, but it is not required to run any self-contained package.

| Package | Separate .NET 10 installation | Contents and intended use |
| --- | --- | --- |
| Self-contained single EXE | Not required | One large executable containing the application and required runtime components. This is the simplest download for most users. |
| Framework-dependent single EXE | **.NET 10 Desktop Runtime x64 required** | One smaller executable. Choose this when the required desktop runtime is already installed. |
| Self-contained ZIP | Not required | An extracted application directory containing `CodexMonitor.exe`, runtime files, README, changelog, and license. Useful when the packaged files should remain visible. |
| Framework-dependent ZIP | **.NET 10 Desktop Runtime x64 required** | The smallest archive. Extract it before use; the directory contains `CodexMonitor.exe`, managed dependencies, runtime metadata, and public documents. |

All four packages provide the same CodexMonitor features. The differences are whether the .NET 10 desktop runtime is bundled and whether the application is delivered as one EXE or as an extracted directory. Do not run an executable directly from inside a ZIP file; extract the ZIP first.

For the easiest setup, use the self-contained single EXE. If the .NET 10 Desktop Runtime x64 is already installed and download size matters, use the framework-dependent single EXE.

## Tested Codex version

CodexMonitor 0.3.0 has been verified with:

- Codex for Windows: `codex-cli 0.147.0-alpha.6.6`

Other Codex versions may work, but the local app-server protocol can change between releases.

## Usage

1. Download either single EXE, or extract either ZIP package.
2. Run the downloaded single EXE directly, or run `CodexMonitor.exe` from the extracted ZIP.
3. The application locates the executable copy supplied by Codex for Windows, preferring `%LOCALAPPDATA%\OpenAI\Codex\bin`, and reads the current limits.
4. Select **Refresh now** to request fresh data immediately.

Closing the window hides it in the Windows notification area while background quota refreshes continue. Left-click the notification-area icon to restore the window. Right-click the icon to minimize the visible window, enable or lock the floating window, open **Settings**, or use **Exit application** to stop background work and exit.

The floating window is disabled by default. When enabled and unlocked, drag it with the left mouse button. Locking it makes mouse input pass through to applications underneath and keeps the floating window above other windows. Use the notification-area menu to unlock it again.

If CodexMonitor is already running, launching it again activates the existing window instead of starting another monitor process.

## Floating window

![CodexMonitor floating window showing Codex quota usage](assets/screenshots/codex-monitor-floating-window.png)

The floating window is a compact view of the existing quota state. It shares the main window's refresh schedule and latest successful snapshot, so enabling it does not create another timer or make additional Codex requests. Its visible fields can be configured independently to include the 5-hour quota, 7-day quota, latest successful refresh time, quota reset times, and subscription type.

Enable or disable it from the notification-area menu, or from the **Floating window** page in **Settings**. While unlocked, hold the left mouse button anywhere on the floating window to drag it; the last position is saved and restored on the next launch.

Locking the floating window makes it click-through and always on top. Mouse input then goes directly to the window underneath, so the floating window cannot be selected or dragged. To unlock it, right-click the CodexMonitor notification-area icon and clear **Lock floating window**, or change the lock option in **Settings**.

## Settings

The first run creates:

```text
%LOCALAPPDATA%\CodexMonitor\settings.json
```

Open **Settings** from the notification-area menu to change notifications, the automatic refresh interval, main-window content, and floating-window behavior. The floating-window page controls its enabled and locked state separately from its visible fields. The same values are stored in:

```json
{
  "schemaVersion": 3,
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
  },
  "floatingWindow": {
    "enabled": false,
    "isLocked": false,
    "left": null,
    "top": null,
    "display": {
      "showFiveHourQuota": true,
      "showWeeklyQuota": true,
      "showLastRefreshTime": true,
      "showResetTimes": false,
      "showSubscription": false
    }
  }
}
```

- `refreshIntervalMinutes`: 1 to 60 minutes. Changes made in the settings window take effect immediately and restart the interval from the save time without forcing an immediate quota request.
- `codexExecutable`: optional absolute path to `codex.exe`; normally leave this as `null`.
- `notifications.enabled`: controls all notification balloons; refresh errors are still logged when disabled.
- `display`: controls the main-window data sections. Refresh time and status always remain visible.
- `floatingWindow`: controls whether the compact window is visible or click-through locked, remembers its last dragged position, and defines its independently visible fields.

Schema 1 and schema 2 files remain compatible. If the JSON file is edited manually, restart the application to reload it.

## Implementation

The application uses .NET 10 and WPF and is divided into three main projects:

- `CodexMonitor.Core`: quota models, display formatting, and refresh state management.
- `CodexMonitor.Infrastructure`: Codex process communication, JSON parsing, settings, and logging.
- `CodexMonitor.App`: WPF windows, notification-area lifecycle, floating-window interaction, runtime settings, single-instance activation, and background refresh scheduling.

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
- Uses standard user-level window styles for floating-window topmost and mouse click-through behavior; it does not install hooks or inject code into other processes.
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

## Disclaimer

This is a personal project created by a non-professional developer with assistance from OpenAI Codex. Although reasonable efforts have been made to review and test the code, software developed with AI assistance may still contain errors, security issues, compatibility problems, or unintended behavior.

To the best of the author's knowledge and intent, no malicious functionality has been intentionally included. This statement describes the author's intent and is not a guarantee that the software is free from defects or security risks.

Use this software at your own risk. You are responsible for reviewing the source code, understanding its permission and privacy boundaries, and deciding whether it is appropriate for your environment. The software is provided "as is," without warranty, as further described in the MIT License.
