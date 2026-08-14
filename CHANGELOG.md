# Changelog

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
