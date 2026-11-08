# Implementation roadmap

The detailed product vision, architecture constraints, acceptance criteria, priorities, and development handoff live in **[Product Plan & Development Handoff](product-plan.md)**. Read that document before starting a new implementation session.

Status legend:
- `[x]` implemented and/or verified as explicitly described.
- `[ ]` not yet complete or not yet acceptance-tested.
- Code complete and CI green does not automatically mean real-Windows acceptance is complete.

## Phase 0 — Stabilise the accepted preview

- [x] Native WinUI 3 shell, navigation, tray icon, and tray popup.
- [x] Keep the app running in the tray when the main window closes; explicit Exit stops it.
- [x] Home Assistant OAuth PKCE, token refresh, secure Windows credential storage.
- [x] Home Assistant REST client, entity discovery, and WebSocket state-change subscription.
- [x] Connection supervisor with reconnect/backoff and status events.
- [x] Embed the real Home Assistant frontend with WebView2 and a persistent, dedicated browser profile.
- [x] Keep native OAuth credentials separate from the embedded browser session.
- [x] Dashboard-only Kiosk Mode request via the `?kiosk` URL parameter.
- [x] User verified the dashboard-only view on Windows 11 (2026-11-08).
- [x] Windows CI and packaging workflows build successfully for the current preview.
- [ ] Review/integrate the current stacked dashboard and installer PRs (requires explicit user approval).
- [ ] Verify a real in-place installer upgrade on Windows, including when Desktop Companion is running in the tray.
- [ ] Implement single-instance activation handoff.
- [ ] Add structured diagnostics without logging tokens, cookies, or private entity payloads.
- [x] Add automated test project for dashboard URL composition and dashboard selection logic.
- [ ] Expand automated test coverage for remaining pure state and service logic.

## Phase 1 — Finish the native dashboard experience

- [x] Embed the actual Home Assistant dashboard rather than reimplementing Lovelace.
- [x] Hide Home Assistant's own header/sidebar with Kiosk Mode when its frontend resource is installed.
- [x] Add a native dashboard selector for configured Home Assistant dashboards/views.
- [x] Remember the selected dashboard and provide a return-to-default action.
- [x] Preserve reverse-proxy paths, existing query parameters, and Kiosk Mode behaviour when switching dashboards.
- [x] Add dashboard navigation/loading/error feedback while reusing the persistent WebView2 session.
- [ ] Verify Control, Media, and System switching, reconnect behaviour, and remembered selection against the live Home Assistant instance on Windows.

## Phase 2 — Windows sensor engine

- [x] Define core sensor descriptor and reading contracts.
- [x] Add the initial Windows system provider scaffold (uptime, hostname, Windows version).
- [x] Implement initial CPU, physical-memory, and uptime readings with stable IDs and units.
- [x] Implement storage capacity/used/available space and readiness per fixed/removable volume.
- [x] Implement network adapter status and receive/send throughput per non-loopback adapter; first counter sample is a baseline, not a fabricated rate.
- [ ] Add optional GPU and hardware temperature/fan providers only where supported, with capability diagnostics.
- [x] Add a two-second sampler with last-known reading retention and explicit stale status.
- [ ] Add provider registry, configurable sampling intervals, and sensor enable/disable preferences.
- [x] Build the initial native Sensors page and CPU/memory/uptime Overview summaries.
- [x] Compare CPU, memory, and uptime readings with Windows Task Manager on Windows 11; user accepted the values and approximately one-second display lag (2026-11-08).
- [ ] Verify storage values for each mounted volume and network rates/status against Windows tools on Windows 11.
- [ ] Verify sampling while in the tray and stale/error behaviour on Windows 11 hardware.

## Phase 3 — Secure Home Assistant pairing and telemetry

- [x] Add the Home Assistant custom-integration scaffold and initial config flow.
- [x] Write and approve the telemetry/pairing protocol in ADR 0008.
- [x] Define device identity, admin-authorised pairing, owner binding, nonce sessions, replay protection, revocation, rate limits, payload limits, and per-sensor availability.
- [x] Implement the Home Assistant custom WebSocket commands and Windows outbound telemetry client in draft PR #21.
- [x] Add Home Assistant device registration and dynamic sensor entities.
- [x] Add Windows sensor selection, Pair/Update, Unpair, and automatic selected-sensor publishing controls.
- [x] Add pure Python protocol tests to Windows CI.
- [ ] Validate live pairing, actual entities, Home Assistant restart/reconnect, and revocation on the user's instance.

## Phase 4 — Native Windows control

- [ ] Master volume and mute.
- [ ] Default audio output-device selection.
- [ ] Media transport controls and now-playing metadata.
- [ ] Application discovery and launch.
- [ ] Focus/minimise/close application controls.
- [ ] Per-application audio levels/output routing where supported.
- [ ] Safe system actions with explicit opt-in and confirmation.
- [ ] Shared typed action contracts and optional Home Assistant exposure; never accept arbitrary shell commands.

## Phase 5 — Actions and quick surfaces

- [ ] Shared action model with stable IDs, typed parameters, icons, and confirmation policies.
- [ ] Native tray favourites and quick actions.
- [ ] User-configurable action ordering and favourites.
- [ ] Compact tray dashboard surface for selected Home Assistant views, designed around WebView2 profile/session reuse.
- [ ] Multi-step actions with sequencing, failure handling, and cancellation.

## Phase 6 — Notifications and event handling

- [ ] Configurable Windows toast notifications for selected Home Assistant and companion events.
- [ ] Priorities, quiet hours, per-event preferences, cooldowns, and deduplication.
- [ ] Safe notification actions that open the relevant native page or Home Assistant dashboard.

## Phase 7 — Applications, media, and desktop integration

- [ ] Application library, discovery, favourites, and launch/status.
- [ ] Now-playing metadata and global media controls.
- [ ] Audio-session and output-device information.
- [ ] Configurable global keyboard shortcuts with conflict detection.
- [ ] Optional Windows startup preference (opt-in).
- [ ] Privacy controls for what PC information is exposed to Home Assistant.

## Phase 8 — Settings, persistence, accessibility, and release

- [ ] SQLite persistence for non-secret settings, dashboard definitions, sensor preferences, actions, and favourites.
- [ ] Keep credentials and pairing secrets in OS-protected storage.
- [ ] Settings for startup, tray behaviour, dashboards, sampling, notifications, privacy, and diagnostics.
- [ ] Export/import of non-secret configuration.
- [ ] Keyboard navigation, screen-reader names, contrast, high-DPI, and multi-monitor validation.
- [ ] Localisation infrastructure (English and Dutch first).
- [ ] Installer clean-install, upgrade, uninstall, and reinstall validation.
- [ ] Evaluate code signing and a repeatable update/release process.
- [ ] Security review, performance profiling, hardware compatibility matrix, crash diagnostics, and v1.0 documentation.

## Next implementation slice

PR #21 is the first secure Home Assistant pairing and sensor-publishing slice, stacked on PR #20. It implements ADR 0008, device registration, selected sensor entities, a separate outbound telemetry WebSocket and per-sensor availability. Live HA pairing, reconnect, restart and revoke tests remain to be completed.

After that acceptance, complete the provider registry, configurable sampling intervals, sensor enable/disable preferences and capability diagnostics. Then proceed to native Windows audio and media controls.

See [the Product Plan](product-plan.md) for scope, implementation notes, acceptance criteria, and the session restart checklist.
