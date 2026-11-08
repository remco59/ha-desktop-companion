# ADR 0007: Embed the Home Assistant frontend with WebView2

- Status: Accepted
- Date: 2026-11-08

## Context

Desktop Companion is a native Windows companion for Home Assistant. Reimplementing Home Assistant dashboard cards, entity toggles, graphs, and layout behaviour as WinUI controls would create a second dashboard renderer and drift away from the Home Assistant experience.

## Decision

Keep the application shell native with WinUI 3, and embed the real Home Assistant frontend in the Dashboard page using the WinUI 3 WebView2 control.

- WinUI 3 owns the application window, navigation, connection settings, tray, notifications, and Windows-specific companion features.
- WebView2 renders the user's Home Assistant dashboard and retains Home Assistant's own cards, themes, controls, and dashboard configuration.
- The dashboard navigates to the configured Home Assistant instance root, preserving any reverse-proxy path prefix.
- WebView2 uses a dedicated persistent user-data folder beneath the current Windows user's LocalAppData directory.
- The Home Assistant frontend performs its own sign-in in the embedded browser profile. This frontend session is separate from the native OAuth session used by REST and WebSocket clients.
- Native OAuth access and refresh tokens are never injected into the page, exposed through a JavaScript bridge, or stored in WebView2.
- New-window requests to the same Home Assistant origin stay in the embedded view; external destinations open in the system browser.
- WebView2 developer tools are disabled in the shipped app.
- Explicit Disconnect clears both native credentials and the dedicated WebView2 profile.

Use the Evergreen WebView2 Runtime. It is preinstalled on Windows 11; detect and present a helpful error if the runtime is missing on a supported system.

## Consequences

- The user sees the familiar Home Assistant dashboard rather than a parallel Desktop Companion imitation.
- The first embedded dashboard visit may require a separate Home Assistant frontend sign-in, even if native OAuth has already connected. That browser session persists across app restarts.
- The native connection and frontend browser session can expire independently and are managed independently.
- Future native Windows features can coexist around the embedded dashboard and use the authenticated native application services without altering Home Assistant's dashboard UI.
- Dashboard layout and entity control remain configured in Home Assistant itself; native dashboard editing is not part of the initial scope.
