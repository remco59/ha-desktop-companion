# Implementation roadmap

## Phase 0 — Technical validation

- [x] Validate the WinUI 3 application and native tray host build.
- [ ] Verify Home Assistant OAuth/application authentication approach.
- [ ] Specify and prototype the authenticated sensor telemetry bridge.
- [ ] Verify basic Windows system sensor access without administrator elevation.
- [ ] Confirm installer and runtime deployment strategy.

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
- [ ] Implement the application host and background lifecycle.
- [ ] Add persistent settings and diagnostics.
- [ ] Establish the complete Windows CI/test matrix.

## Phase 2 — Home Assistant

- [ ] Implement OAuth/token lifecycle.
- [ ] Implement WebSocket connection and subscriptions.
- [ ] Add entity discovery, search, and favourites.
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

## Phase 5 — Dashboard

- [ ] Widget model and renderer.
- [ ] Full dashboard.
- [ ] Tray dashboard.
- [ ] Drag/drop editor and resizing.
- [ ] Layout persistence and templates.
- [ ] Sensors, entities, actions, audio, and application widgets.

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
