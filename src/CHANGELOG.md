# Complete Changelog

## 0.2.2 - 2026-08-14

### Added

- Added a compact dark acrylic main window with a custom integrated title bar.
- Added a controllable dark Acrylic Blur through the Windows composition API, with Windows 11 DWM Desktop Acrylic as a fallback.
- Added a three-column `5H`, `7D`, and quota-reset-time layout with subtle vertical dividers.
- Added separate 5-hour and 7-day reset timestamps, using `-` when a reset time is unavailable.
- Added the current subscription type to the left side of a dedicated footer surface.
- Added the latest data-read time and refresh status to the right side of the footer.
- Added custom refresh, minimize, and close-to-notification-area controls to the title bar.
- Added one Windows notification per failed refresh attempt.
- Added this source-level changelog as the complete version history.

### Changed

- Removed detailed refresh errors from the main window; detailed failures remain in the rotating local log.
- Kept the latest successful quota snapshot visible when a later refresh fails.
- Limited the repository-root changelog to the current `0.2.x` release series.

## 0.2.1 - 2026-08-14

### Added

- Added the active ChatGPT subscription plan or token/API-key login type to the main window.
- Added account metadata reads through the local Codex app-server without directly accessing authentication files or credentials.

### Changed

- Token, API-key, signed-out, and other non-ChatGPT account types now display `None` for quota values, reset countdowns, and refresh time.
- ChatGPT quota reads now reuse the account metadata result and are skipped when ChatGPT subscription quota does not apply.
- Expanded the documented privacy boundary for authentication files, credential stores, API keys, tokens, and account email addresses.

## 0.2.0 - 2026-08-14

### Added

- Added a Windows notification-area icon that remains available when the main window is hidden.
- Added left-click restoration of the main window from the notification-area icon.
- Added notification-area commands to open the window, refresh quota data immediately, and exit the application.
- Added a one-time notification explaining that CodexMonitor continues running after the window is closed.
- Added a custom application icon for the executable, main window, and notification area.

### Changed

- Closing the main window now hides it instead of exiting, allowing scheduled quota refreshes to continue in the background.
- Exiting from the notification-area menu now cancels active refresh work, disposes the icon, and closes the application cleanly.
- Updated the application icon to use a transparent background in the executable, main window, and notification area.

## 0.1.0 - 2026-08-14

### Added

- Added a regular WPF window that displays the available Codex quota windows, remaining percentages, reset countdowns, last refresh time, and current status.
- Added local quota reads through the Codex app-server without reading authentication files or tokens directly.
- Added an automatic data refresh interval of three minutes by default.
- Added one-second local countdown updates without requesting fresh quota data every second.
- Added a **Refresh now** action for immediate manual updates.
- Added retention of the latest successful quota snapshot when a later refresh fails.
- Added a local `settings.json` file for the refresh interval and an optional custom `codex.exe` path.
- Added local diagnostic logging.
- Added self-contained single-EXE, framework-dependent single-EXE, self-contained ZIP, and framework-dependent ZIP release formats for win-x64.
