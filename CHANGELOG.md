# Changelog

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
