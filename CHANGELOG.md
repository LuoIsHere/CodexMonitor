# Changelog

## 0.3.4 - 2026-11-26

### Fixed

- Fixed an intermittent `git.exe` "The application was unable to start correctly (0xc0000142)" dialog during refreshes. Newer Codex versions start `git` when the app-server starts, and stopping the app-server immediately after a read could interrupt it while it was still loading. CodexMonitor now closes the app-server's input and lets it exit on its own, and stops its process tree only if it is still running after 5 seconds.

For all changes in earlier releases, see the [complete changelog](docs/CHANGELOG.md).
