> [!Attention]
> CodexMonitor is a personal project. It is not affiliated with OpenAI, and no OpenAI staff are involved in the project.

# CodexMonitor

[简体中文](README_cn.md)

A small Windows utility for viewing Codex quota, reset times, subscription type and refresh status from the notification area or a compact floating window.

Version: `0.3.1`

![CodexMonitor dashboard](assets/screenshots/codex-monitor-dashboard.png)

## Features

- Remaining quota for the approximately five-hour (`5H`) and seven-day (`7D`) windows, with local reset times.
- One read at startup, manual refresh, and an adjustable 1–60 minute interval (default: 3 minutes).
- Last successful data retained after a failed read, with status and optional notifications.
- Notification-area controls for restoring the main window, settings, floating-window controls and exit.
- A draggable floating window with independent display settings, remembered position, and locked click-through/topmost mode. Both windows share one refresh service.
- Optional startup at Windows sign-in for the current user, with an option to start in the notification area.
- Single-instance operation: manual launches restore the running application; automatic launches leave it undisturbed.

Token/API-key accounts display `None` for quota and refresh time, and `-` for reset times.

## Requirements and downloads

Windows 10 version 1809 or later, x64, with Codex for Windows installed and signed in. Windows 11 is recommended.

| Package | Separate runtime required |
| --- | --- |
| Self-contained single EXE or ZIP | No |
| Framework-dependent single EXE or ZIP | .NET 10 Desktop Runtime x64 |

EXE filenames follow these patterns (`<version>` is the release version, such as `0.3.1`):

- Self-contained: `CodexMonitor-<version>-win-x64-self-contained.exe`
- Framework-dependent: `CodexMonitor-<version>-win-x64-framework-dependent.exe`

ZIP packages use the same names with a `.zip` extension.

All packages have the same features. Extract ZIP packages before running `CodexMonitor.exe`. Single EXE packages can be run directly. Building from source requires the .NET 10 SDK.

Verified Codex versions:

| Codex version | CodexMonitor |
| --- | --- |
| `codex-cli 0.147.0-alpha.6.6` | 0.3.0 |
| `codex-cli 0.155.0-alpha.2.6` | 0.3.1 |

The local app-server protocol may change between Codex releases.

## Usage

Run the application to open the dashboard. Use **Refresh now** for an immediate read. Closing the main window keeps monitoring active in the notification area; left-click its icon to restore the window. Right-click for settings and **Exit application**.

![CodexMonitor floating window](assets/screenshots/codex-monitor-floating-window.png)

Enable the floating window from the notification-area menu or Settings. Drag it while unlocked. Locking enables mouse click-through and keeps it on top; unlock it from the notification-area menu or Settings. Its fields can be configured separately from the main window.

## Start at Windows sign-in

In **Settings → General**, enable **登录 Windows 后自动启动** and save. This is off by default and requires no administrator privileges. Keep **启动后最小化到托盘** checked to show only the notification-area icon and any enabled floating window. Uncheck it to show the main window on automatic startup. Manual launches always open or restore the application window.

The startup entry changes only when you explicitly save a startup change. **Windows Startup apps settings still control whether it runs**; CodexMonitor does not re-enable entries disabled in Windows Settings or Task Manager.

After moving the application, run it from its new location, select **重新登记当前路径（保存后执行）** in Settings and save.

## Settings and logs

The default locations are:

```text
%LOCALAPPDATA%\CodexMonitor\settings.json
%LOCALAPPDATA%\CodexMonitor\logs\codex-monitor.log
```

Settings control startup, notifications, refresh interval and window contents. Older configurations retain their options and default to startup disabled. Manual JSON edits take effect after restarting; enable Windows startup through the settings window.

When system notifications are enabled:

- Closing the main window shows a background-running notice, at most once per application session.
- Failed refreshes show a short reason; details are written to the log.

The optional `codexExecutable` setting accepts a path to `codex.exe`. Automatic discovery prefers the copy under `%LOCALAPPDATA%\OpenAI\Codex\bin`. `CODEX_MONITOR_HOME` can override the settings/log directory.

## Implementation and permissions

- `CodexMonitor.Core`: models, display formatting and refresh state.
- `CodexMonitor.Infrastructure`: Codex process communication, JSON parsing, settings, logging and user startup registration.
- `CodexMonitor.App`: WPF views, tray lifecycle, single-instance activation and refresh scheduling.

Quota reads use the local `codex.exe app-server` over standard input/output. The child process closes after each read; the last successful snapshot stays in memory.

CodexMonitor runs with standard user permissions. Authentication is delegated to Codex; the monitor does not directly read credential files or request token values. Account email fields are ignored. Local logs contain results and error text; inspect them before sharing because errors may contain machine-specific details.

Startup registration uses only the application's `CodexMonitor` value under the current user's Windows Run key.

## Build and test

On Windows with the .NET 10 SDK:

```powershell
dotnet build .\CodexMonitor.sln -c Release
dotnet run --project .\tests\CodexMonitor.Tests\CodexMonitor.Tests.csproj -c Release
```

Tests use simulated startup storage and temporary settings, not the user's actual startup entry. The WPF lifecycle test takes about a minute to verify the real refresh timer without displaying windows. The repository has no third-party NuGet dependencies; basic builds use the installed SDK and desktop targeting packs.

An optional live Codex probe is available:

```powershell
dotnet run --project .\tests\CodexMonitor.Tests\CodexMonitor.Tests.csproj -c Release -- --live
```

To create the four release packages in `artifacts/`:

```powershell
powershell.exe -NoProfile -File .\build\Publish.ps1
```

Self-contained publishing may download Microsoft runtime packs from NuGet.org. ZIP packages include both README languages, the current changelog and license.

## Project

Inspired by [CodexQuotaMonitor](https://github.com/DiMY-CN/CodexQuotaMonitor) for quota display and [TrafficMonitor](https://github.com/zhongyang219/TrafficMonitor) for an accessible Windows monitor.

[Current changelog](CHANGELOG.md) · [Complete history](src/CHANGELOG.md) · [GitHub Releases](https://github.com/LuoIsHere/CodexMonitor/releases)

[MIT License](LICENSE) · Copyright (c) 2026 luoishere

This is a personal project developed with assistance from OpenAI Codex. It may contain defects and is provided without warranty under the MIT License.
