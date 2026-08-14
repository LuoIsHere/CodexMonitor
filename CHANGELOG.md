# Changelog

## 0.2.3 - 2026-08-14

### Added

- Added a dark settings window for notification, refresh-interval, and visible-content preferences.
- Added configurable visibility for the 5-hour quota, 7-day quota, reset times, and subscription type.
- Added a dark notification-area context menu with minimize, settings, reserved floating-window, and exit commands.
- Added single-instance activation so launching CodexMonitor again restores the existing application window.
- Added a reserved floating-window service contract without implementing the floating window itself.

### Changed

- Automatic refresh intervals can now be changed from 1 to 60 minutes and take effect without restarting the application.
- Windows notifications can now be disabled while refresh failures continue to be written to the local log.
- The repository-root changelog now contains only the current release.
