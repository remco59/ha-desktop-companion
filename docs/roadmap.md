# Implementation roadmap

## Phase 0 — Technical validation

- [x] Validate the WinUI 3 application and native tray host build.
- [x] Verify Home Assistant OAuth/application authentication approach.
- [ ] Specify and prototype the authenticated sensor telemetry bridge.
- [ ] Verify basic Windows system sensor access without administrator elevation.
- [x] Confirm the WinUI 3 WebView2 hosting strategy and Evergreen runtime model.
- [x] Add WebView2 runtime availability/error handling to the dashboard host.

## Phase 1 — Foundation

- [x] Create repository and solution structure.
- [x] Add initial WinUI 3 navigation shell.
- [x] Define core sensor and Home Assistant contracts.
- [x] Add first Windows system sensor provider scaffold.
- [x] Add Home Assistant REST client foundation.
- [x] Add Home Assistant integration scaffold.
- [x] Add native tray icon host and tray pop-up window.
- [x] Keep the main application alive when its window is closed.
- [ ] Implement a full single-instance activation handoff.
- [x] Implement the initial application host and background lifecycle.
- [x] Add persistent non-secret instance settings; diagnostics remain open.
- [x] Establish the first Windows CI build.

## Phase 2 — Home Assistant

- [x] Implement OAuth PKCE/token lifecycle (pending live-instance acceptance test).
- [x] Implement a one-connection WebSocket client and state-change subscription.
- [x] Add a connection supervisor with reconnect/backoff and status events.
- [x] Add the authenticated entity REST query service for native integrations; use the Home Assistant frontend for dashboard browsing and entity controls.
- [ ] Add service, scene, and script controls.
- [ ] Complete integration pairing and telemetry entities.

## Phase 3 — Windows monitoring

- [ ] CPU, memory, GPU, storage, network, uptime, and application sensors.
- [ ] Hardware provider architecture and capability diagnostics.
- [ ] Sensor configuration and update intervals.
- [ ] Native integration publishing.
- [ ] Optional MQTT Discovery publishing.

## Phase 4 — Windows control

- [ ] Master volume and mute.
- [ ] Per-application audio and output-device selection.
- [ ] Application discovery, launching, focus, and close.
- [ ] Safe system actions.
- [ ] Shared action engine and Home Assistant exposure.

## Phase 5 — Home Assistant dashboard experience

- [x] Embed the actual Home Assistant frontend in a native WinUI 3 window using WebView2.
- [x] Keep the Home Assistant frontend's cards, entity controls, layouts, and styling intact.
- [x] Persist the WebView2 profile per Windows user and clear it on explicit disconnect.
- [x] Request Home Assistant dashboard-only kiosk presentation through the Kiosk Mode `?kiosk` URL parameter.
- [ ] Add a native dashboard selector that can switch among Home Assistant dashboards/views.
- [ ] Add a compact tray dashboard surface that hosts selected Home Assistant views.
- [ ] Add native Windows companion surfaces (system sensors, media, quick actions) around the embedded Home Assistant dashboard.

## Phase 6 — Advanced features

- [ ] Notifications and actionable events.
- [ ] Media controls and playback metadata.
- [ ] Global keyboard shortcuts.
- [ ] Application library and favourites.
- [ ] Multi-step actions and richer widgets.

## Phase 7 — Stabilisation

- [ ] Hardware compatibility matrix.
- [ ] Security review.
- [ ] Performance and reconnect tests.
- [ ] Installer, upgrade, and uninstall validation.
- [ ] Documentation and v1.0 release.
