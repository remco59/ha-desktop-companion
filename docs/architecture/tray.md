# Native tray host and pop-up

## Behaviour

- The application owns a native notification-area icon through `Shell_NotifyIconW`.
- A hidden native callback window receives shell events.
- Left-click, double-click, keyboard selection, and the context gesture open/toggle the compact dashboard.
- The pop-up is a WinUI 3 window using the context-menu presenter, positioned near the pointer and clamped to the nearest display's work area.
- Escape and window deactivation hide the pop-up.
- Closing the main application window hides it; it does not terminate the process.
- The tray pop-up offers an explicit Exit action that disposes the tray icon and exits the application.
- The application re-adds the notification-area icon after Explorer recreates the taskbar.

## Implementation boundaries

- Native notification-area interop lives in `DesktopCompanion.Windows`.
- The WinUI tray dashboard and application lifecycle live in `DesktopCompanion.App`.
- The tray UI does not own Windows or Home Assistant business logic.
- The initial pop-up displays only real local data (machine name and uptime) and honest disconnected/placeholder states.

## Current limitations

- The tray icon currently uses the standard Windows application icon; project artwork will replace it later.
- The pop-up is a UI shell; volume, sensor, and Home Assistant controls are not wired yet.
- Single-instance prevention and activation handoff are not implemented yet.
- Explorer restart recovery is implemented but requires runtime testing on Windows.
