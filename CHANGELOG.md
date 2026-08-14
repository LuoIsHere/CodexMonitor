# Changelog

## 0.3.0 - 2026-08-14

### Added

- Added a compact rounded translucent floating window for the 5-hour quota, 7-day quota, and latest successful refresh time.
- Added independent floating-window display settings for quota values, refresh time, reset times, and subscription type.
- Added unlocked mouse dragging with persisted window position and screen-boundary recovery; enable the floating window from the notification-area menu or the floating-window settings page, then drag it with the left mouse button.
- Added a locked click-through mode that keeps the floating window above other windows while passing mouse input to the application underneath.
- Added notification-area commands to enable, lock, and unlock the floating window, ensuring it can be unlocked after mouse input starts passing through.

### Changed

- Upgraded `settings.json` to schema 3 while preserving schema 1 and schema 2 compatibility.
- Floating-window data now shares the existing refresh state and does not issue additional Codex requests.
- Floating-window headings and values now use shared rows with consistent baseline alignment.
