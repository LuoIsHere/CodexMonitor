# Complete Changelog

## 0.3.1 - 2026-09-20

### Added

- Added optional current-user startup at Windows sign-in, with a default-on option to start in the notification area and retain any enabled floating window.
- Added startup registration status, executable-path validation, and an explicit action to re-register a moved application.
- Added startup policy, isolated registration, settings failure and WPF background lifecycle tests.
- Added a Simplified Chinese README with language links in both versions.

### Changed

- Moved the first quota read and refresh scheduling into the application lifecycle so monitoring starts without showing the main window.
- Automatic second instances now exit without activating the running application; manual launches retain existing activation behavior.
- Startup registration changes only after an explicit startup edit is saved. Unrelated saves and application restarts do not restore missing entries or change Windows startup approval state.
- Settings now use schema 4; older configurations retain their existing options with startup disabled.
- Settings save errors remain visible in the dialog, including partial results when startup registration succeeds but configuration saving fails.
- Release ZIPs now include both README languages.
- Clarified system notification behavior in Settings and simplified the README.

## 0.3.0 - 2026-08-14

### Added

- Added a compact rounded translucent floating window for the 5-hour quota, 7-day quota, and latest successful refresh time.
- Added independent floating-window display settings for quota values, refresh time, reset times, and subscription type.
- Added unlocked mouse dragging with persisted window position and virtual-screen boundary recovery; enable the floating window from the notification-area menu or the floating-window settings page, then drag it with the left mouse button.
- Added a locked click-through mode using user-level Windows extended styles to keep the window above other windows while passing mouse input to the application underneath.
- Added notification-area commands to enable, lock, and unlock the floating window, ensuring it can be unlocked after mouse input starts passing through.
- Added schema 2 migration and schema 3 roundtrip coverage to the offline settings tests.

### Changed

- Replaced the reserved floating-window service with a lifecycle-managed implementation that shares the main refresh state.
- Upgraded `settings.json` to schema 3 while preserving schema 1 and schema 2 compatibility.
- Updated the settings window with separate general and floating-window pages.
- Aligned floating-window headings and values on shared rows with consistent font size and line height.

## 0.2.3 - 2026-08-14

### Added

- Added a dark settings window for notification, refresh-interval, and visible-content preferences.
- Added configurable visibility for the 5-hour quota, 7-day quota, reset times, and subscription type.
- Added a dark notification-area context menu with minimize, settings, reserved floating-window, and exit commands.
- Added single-instance activation so launching CodexMonitor again restores the existing application window.
- Added a reserved floating-window service contract without implementing the floating window itself.

### Changed

- Upgraded `settings.json` to schema 2 while preserving schema 1 compatibility and defaults.
- Automatic refresh intervals can now be changed from 1 to 60 minutes and take effect without restarting the application.
- Windows notifications can now be disabled while refresh failures continue to be written to the local log.
- Hidden quota sections now collapse their columns and adjacent dividers instead of leaving empty layout space.
- The repository-root changelog now contains only the current release.

## 0.2.2 - 2026-08-14

### Added

- Added a compact dark acrylic main window with a custom integrated title bar.
- Added a compact dark glass window that uses native Windows Acrylic when transparency effects are enabled and an app-rendered translucent fallback otherwise.
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
