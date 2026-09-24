# Changelog

## 0.3.2 - 2026-09-24

### Added

- Added English and Traditional Chinese (Hong Kong) for the main window, settings, tray menu, notifications, and application messages. Language changes take effect after saving; Simplified Chinese remains the default.
- Added a Traditional Chinese (Hong Kong) README and links between all three README languages.

### Changed

- Moved the refresh schedule and shared monitoring state out of the main-window view model. The main and floating windows now subscribe independently to one application-owned monitor.
- Kept the floating window's existing English labels and account text across language changes.
- Saved language as an optional schema 4 setting; older configurations continue to use Simplified Chinese without changing other preferences or startup registration.
- Release ZIPs now include all three README languages.
- Expanded automated coverage for localization, shared refresh lifecycle, and shutdown cancellation.
