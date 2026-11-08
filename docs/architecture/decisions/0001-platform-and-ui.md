# ADR 0001: Native Windows UI and platform

- Status: Accepted
- Date: 2026-11-08

## Decision

Use C# on .NET 10 LTS with WinUI 3 from Windows App SDK 2.6.5. Target Windows 11 x64 initially, with a minimum Windows build chosen deliberately. Use a native Win32 Shell notification-area host for tray integration.

## Rationale

The product depends heavily on Windows APIs: audio sessions, process/window management, startup, notifications, and tray behaviour. A native WinUI 3 shell keeps Windows platform integration direct. The Home Assistant dashboard itself is intentionally rendered by the official frontend inside WebView2; it is not a general-purpose browser shell and does not replace the native application chrome.

## Consequences

- The first release is Windows-only.
- The main window and tray pop-up share a single application process.
- Win32 interop is isolated behind interfaces in the Windows project.
- UI components do not call platform APIs directly.
