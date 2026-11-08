# Desktop Companion — Product Plan & Development Handoff

**Status:** Living project plan  
**Last updated:** 2026-11-08  
**Repository:** https://github.com/remco59/ha-desktop-companion  
**Primary platform:** Windows 11 x64 · WinUI 3 · .NET 10 · WebView2

This document is the project-level source of truth for product intent, architecture constraints, delivery order, acceptance criteria, and the next implementation steps. Read this before starting a new development session. Keep it updated whenever scope, decisions, or delivery status changes.

Related documents:
- [Implementation roadmap](roadmap.md) — concise phase checklist.
- [Architecture overview](architecture/overview.md) — system boundaries and runtime principles.
- [Communication protocol](architecture/protocol.md) — Home Assistant REST/WebSocket and the telemetry design gate.
- [Development setup](development/setup.md) — local build and validation steps.
- [ADR 0007](architecture/decisions/0007-embedded-home-assistant-dashboard.md) — why the real Home Assistant frontend is embedded.

---

## 1. Product vision

Desktop Companion is a **native Windows companion for Home Assistant**, not a replacement Home Assistant frontend and not simply a browser wrapper.

It brings the Windows PC and the smart home together in one polished desktop application:

- A native Windows shell for navigation, settings, tray behaviour, notifications, and operating-system integration.
- The user's real Home Assistant dashboards, rendered by Home Assistant itself.
- Windows sensors exposed to Home Assistant as a first-class device.
- Native Windows capabilities such as volume, media, running applications, and carefully controlled system actions.
- A compact tray surface for frequent information and actions.

The result should feel like one coherent Windows application. Home Assistant remains the source of truth for smart-home dashboards, entities, automations, scenes, and service semantics. Desktop Companion adds the Windows capabilities and desktop ergonomics around it.

### Product experience principles

1. **Native outside, Home Assistant inside.** WinUI 3 owns the Windows application. WebView2 hosts the actual Home Assistant frontend for dashboard content.
2. **Dashboard first, not Home Assistant chrome.** The embedded surface should show the selected Lovelace dashboard without Home Assistant's own header/sidebar. Kiosk Mode is the supported mechanism.
3. **Do not recreate Lovelace.** Never build native lookalikes of Home Assistant cards, entity toggles, graphs, or dashboard editors. Keep the actual HA frontend interactive and visually faithful.
4. **Windows features should be genuinely native.** Sensors, audio, application management, notifications, shortcuts, and tray features belong in Windows services and WinUI surfaces.
5. **One coherent design system.** Charcoal/dark surfaces, restrained Home Assistant blue accents, clear hierarchy, compact but readable controls, accessible contrast, and proper high-DPI behaviour.
6. **Safe by default.** No arbitrary remote command execution. Destructive or disruptive system actions need explicit enablement and confirmation.
7. **Useful without MQTT.** The normal Home Assistant connection should work directly. MQTT may be an optional transport, not a mandatory dependency.
8. **Incremental, testable delivery.** Implement vertical slices with clear acceptance criteria. Do not ship sensor publishing or remote actions before their security contracts are defined.

### Non-goals

- Replacing Home Assistant or creating a second Lovelace renderer.
- Making every Home Assistant administration page available inside Desktop Companion.
- Exposing an unauthenticated inbound HTTP server on the Windows PC.
- Running arbitrary PowerShell, shell, or executable commands received from Home Assistant.
- Requiring MQTT for basic operation.
- Supporting multiple Windows users in one installation/session as an initial requirement.

---

## 2. Accepted architecture and product constraints

These are decisions, not open brainstorming topics. Any change should be proposed as a new ADR.

### Application boundaries

- **DesktopCompanion.App:** WinUI 3 shell, navigation, settings, WebView2 host, tray popup, notifications, and composition root.
- **DesktopCompanion.Core:** platform-independent models, contracts, and application concepts.
- **DesktopCompanion.Windows:** Windows APIs for sensors, audio, processes, notifications, shortcuts, and tray integration.
- **DesktopCompanion.HomeAssistant:** Home Assistant authentication, REST, WebSocket, services, and future telemetry adapters.
- **integrations/homeassistant:** Home Assistant custom integration for pairing/device registration and published Windows entities.
- **Optional MQTT adapter:** an alternate transport only where it adds value; not part of the first secure telemetry milestone.

### Home Assistant dashboard

- Use the actual Home Assistant frontend inside WinUI 3 WebView2.
- The WebView2 profile is dedicated to Desktop Companion and stored below the current user's LocalAppData directory.
- The embedded frontend's browser session is separate from native OAuth credentials.
- Never inject native OAuth access or refresh tokens into JavaScript, page storage, or a WebView2 bridge.
- Keep the Home Assistant dashboard interactive.
- Append the Kiosk Mode `?kiosk` parameter when loading the configured instance URL.
- Preserve reverse-proxy path prefixes and existing query parameters.
- Respect an explicit `disable_km` opt-out.
- The Kiosk Mode frontend resource must be installed in Home Assistant through HACS. Without it, Home Assistant displays its normal frontend chrome.
- Dashboard selection should navigate to Home Assistant dashboard/view URLs; do not query dashboard configuration and redraw it natively.
- The initial Desktop Companion dashboard presets are Control (`desktop-companion/control`), Media (`desktop-companion/media`), and System (`desktop-companion/system`), with Control as the default.

### Connectivity and security

- The Windows application initiates outbound connections to Home Assistant.
- REST is used for snapshots, verification, and service calls.
- WebSocket is used for live state events and connection status.
- OAuth PKCE and refresh-token lifecycle are native application concerns.
- Refresh credentials are held in Windows Credential Manager (or another OS-protected store), never plaintext settings.
- No inbound unauthenticated listener.
- Keep transport and OS logic out of XAML code-behind wherever practical.
- Any future telemetry or remote-action protocol must define identity, authorization, replay protection, revocation, rate limits, message limits, availability, and reconnect behaviour before implementation.

### Lifecycle

- Closing the main window hides the app to the system tray.
- Explicit Exit fully stops background services and removes the tray icon.
- A full single-instance activation handoff remains to be implemented.
- Background monitoring must continue while the main window is hidden, but stop cleanly on explicit Exit.

---

## 3. Current implementation and verified baseline

This section separates implemented work from user-verified behaviour and remaining gaps.

### Working foundation

- Native WinUI 3 application shell and navigation.
- Native system-tray icon and compact tray popup.
- Main-window close hides to tray; explicit Exit shuts down the application.
- Persistent non-secret Home Assistant instance setting.
- Home Assistant OAuth PKCE flow and refresh-token lifecycle.
- Windows credential-store adapter.
- Authenticated Home Assistant REST client for connection checks, entity snapshots, and service calls.
- WebSocket client for authenticated `state_changed` subscriptions.
- Connection supervisor with status events and retry/backoff.
- Entity discovery was tested against the user's Home Assistant instance and returned **629 entities**.
- WebView2 hosts the real Home Assistant frontend with a persistent, dedicated browser profile.
- Explicit Disconnect clears the native credentials and the dedicated WebView2 profile.
- Kiosk Mode `?kiosk` dashboard presentation is implemented.
- The native dashboard selector, remembered selection, and safe relative URL composition are implemented in open PR #18; its initial presets match the three confirmed Home Assistant views (Control, Media, System).
- Windows CI for the latest PR #18 head passed; live dashboard switching and restart persistence still require Windows 11 acceptance.

### User-verified on Windows 11 — 2026-11-08

- The installed app connects to the real Home Assistant instance.
- The Home Assistant dashboard loads inside the native Desktop Companion window.
- Kiosk Mode hides Home Assistant's own navigation/header; the dashboard itself remains interactive.
- The clean dashboard presentation has been explicitly accepted by the user (screenshot shared 2026-11-08).

Treat this as the current accepted baseline. Do not re-open the decision to embed the real frontend or show its native HA chrome unless the user asks.

### Still incomplete or not yet acceptance-tested

- Installer in-place upgrade behaviour has not yet been confirmed by a clean upgrade test. PR #14 changes the Inno Setup file handling and process detection; CI packaging success is not proof of a successful upgrade on a real Windows machine.
- A full single-instance activation handoff is missing.
- Dashboard definitions and last-selected dashboard ID are persisted in the existing non-secret settings document; broader SQLite-backed settings remain a later milestone. The first-run preset list is Control, Media, and System for the Desktop Companion dashboard.
- The Sensors, Actions, Applications & Media, and Notifications areas are still placeholders.
- The Windows sensor provider is only a small scaffold (uptime, computer name, Windows version).
- The Home Assistant custom integration is a config-flow scaffold; it does not yet pair a PC or create telemetry entities.
- No secure telemetry pairing protocol has been approved or implemented.
- No production-grade settings/database layer or sensor configuration UI exists.
- Automated unit/integration coverage is still limited and needs to grow with each vertical slice.

---

## 4. Delivery strategy and priority order

Build in the following order. A later phase may be split into multiple PRs, but do not skip its design or security gates.

### Milestone 0 — Stabilise the accepted preview

**Goal:** Make the current native shell, embedded dashboard, and installer a reliable baseline before adding more features.

Work:
- Complete review and integration of the current stacked dashboard/installer PRs after explicit user approval; do not merge automatically.
- Validate an actual in-place upgrade from an installed previous build, with Desktop Companion running and hidden in the tray.
- Validate clean install, in-place upgrade, uninstall, reinstall, retained settings, and explicit Disconnect behaviour.
- Confirm that `CloseApplicationsFilter` only targets `DesktopCompanion.App.exe` and that unchanged .NET runtime files are not needlessly replaced.
- Implement single-instance activation: launching a second instance should activate the existing process/window and pass the requested destination (Dashboard, Settings, etc.).
- Add basic structured diagnostics for startup, Home Assistant connection transitions, WebView2 failures, and installer/app version. Never log tokens, cookies, or private entity payloads.
- Add a minimal automated test project and test the pure URL, state, and service logic.

Acceptance:
- User can install, upgrade, launch, close-to-tray, re-open, and explicitly exit without orphan processes or locked-file installer errors.
- A second launch activates the existing app instead of creating duplicate tray icons/connections.
- Dashboard session remains isolated from native OAuth credentials.
- CI builds and tests the app; packaging produces a usable x64 installer.

### Milestone 1 — Finish the native dashboard experience

**Goal:** Make the accepted dashboard-only WebView feel intentionally integrated into Desktop Companion.

Current implementation note (2026-11-08): the native selector, default/last-selected state, URL composition, and unit-test coverage are being delivered on `feat/native-dashboard-selector`. Code completion and CI do not replace live Windows/Home Assistant validation.

Work:
- Add a native dashboard selector backed by typed dashboard definitions (friendly display name, relative path, optional icon, sort order, default marker).
- Include a default dashboard and remember the last selected dashboard ID in non-secret application settings.
- Navigate the existing WebView2 instance to the selected Home Assistant path, preserving the configured reverse-proxy prefix and Kiosk Mode parameter.
- Surface dashboard navigation/loading errors without clearing the persistent browser session.
- Provide a clear way to return to the default dashboard.
- Keep refresh and Open in browser actions; ensure Open in browser opens the currently selected dashboard.
- Consider per-dashboard kiosk preferences only if there is a real use case; default remains dashboard-only.

Acceptance:
- User can switch between at least two configured dashboards/views without seeing Home Assistant's header/sidebar.
- Cards and controls remain native Home Assistant components and fully interactive.
- Dashboard selection survives app restart.
- No second Home Assistant renderer or JavaScript bridge is introduced.

### Milestone 2 — Windows sensor engine (first true Windows-native feature)

**Goal:** Show reliable Windows PC telemetry in Desktop Companion and prepare it for Home Assistant publishing.

Current implementation note (2026-11-08): CPU + physical-memory + uptime are implemented in draft PR #19 and were compared with Windows Task Manager on Windows 11; the user confirmed CPU (about 23% vs 21.9%), memory (55% vs 54.7%), and uptime (9h22) are consistent. Approximately one second of display lag is accepted, and the two-second sampling cadence remains unchanged. Per-volume storage and per-adapter network throughput/status are being delivered in stacked draft PR #20.

Initial sensor set:
- CPU utilisation.
- Memory used/available and percentage.
- Storage capacity/used/free by volume.
- Network throughput and adapter status.
- System uptime.
- Hostname and Windows version (already scaffolded).
- GPU utilisation/memory where reliably available through supported providers.
- Temperatures/fan speeds are optional hardware-dependent sensors; report unavailable honestly rather than inventing values.

Work:
- [x] Define stable CPU, physical-memory, and uptime sensor IDs, units, categories, and sample timestamps.
- [x] Read CPU cumulative counters with GetSystemTimes and physical-memory status with GlobalMemoryStatusEx.
- [x] Prime CPU counters before publishing a percentage; never report an unprimed first sample as a real reading.
- [x] Add a two-second background sampling loop, retain last-known readings, and mark retained readings stale after a failed refresh.
- [x] Build the first native Sensors page with current CPU, memory, uptime, computer name, Windows version, and last-sample feedback.
- [x] Add Overview summaries for CPU, memory, and uptime.
- [ ] Add a provider registry, configurable sampling intervals, and sensor enable/disable preferences.
- [x] Enumerate fixed/removable Windows volumes with stable IDs and read capacity, used, available space and readiness.
- [x] Enumerate non-loopback network adapters with stable IDs, current operational status, and receive/send throughput in bytes per second.
- [x] Prime adapter counters before calculating throughput; receive/send rates share one cached counter sample and invalid/reset counters do not become readings.
- [ ] Add capability diagnostics for optional hardware providers; keep GPU, temperatures, and fan speeds unavailable until a supported provider exists.

Acceptance:
- [x] CPU and physical-memory readings were compared with Windows Task Manager on Windows 11 and accepted by the user (2026-11-08); around one second of display lag is acceptable.
- [x] Uptime display was compared with Windows Task Manager and matched (9h22 at validation).
- Sensor readings are accurate enough for monitoring, timestamped, and refresh without freezing the UI.
- Missing GPU/temperature support does not break CPU, memory, storage, or network.
- CPU usage sampling does not report an unprimed first counter value as real usage.
- User can configure sampling cadence and disable individual sensors.
- App remains responsive with the window hidden in the tray.

### Milestone 3 — Secure Home Assistant device pairing and telemetry

**Goal:** Register the Windows PC as a Home Assistant device and expose only selected Windows sensors as HA entities.

Current implementation note (2026-11-08): the first end-to-end pairing/publication slice is being delivered in draft PR #21, stacked on PR #20. The Home Assistant integration registers a paired PC and dynamic sensor entities. Desktop Companion uses a separate authenticated outbound WebSocket, an administrator-authorised pairing command, an owner-bound nonce session, monotonic sequence numbers, bounded payloads, and an explicit selected-sensor list. Device metadata is persisted in the HA config entry; readings and session nonces stay in memory. The Sensors page includes the pairing and publishing controls. CI must validate C# and Python protocol tests; actual pairing, restart/reconnect and revocation still require the user's live HA instance.

#### Accepted protocol decision

ADR 0008 defines the implementation contract:

- Pairing is explicitly initiated in Desktop Companion and requires an authenticated Home Assistant administrator.
- A random installation UUID identifies the PC; it is not a credential and no MAC address, hardware serial, or machine GUID is collected.
- The HA integration binds the paired installation to the authenticated HA user ID.
- A new outbound telemetry WebSocket session obtains a cryptographically random in-memory nonce.
- Each publication contains an increasing sequence number and only registered sensor IDs.
- The first release is bounded to 100 sensors, 32 KiB per reading batch, short text states, finite numeric values, and at most one publish per second.
- HA marks each sensor unavailable after 15 seconds without a fresh value.
- Admin-authorised revocation removes its paired device and entities.
- No inbound Windows listener, MQTT requirement, native-token WebView bridge, or remote command execution.

Work:
- [x] Approve the telemetry and pairing protocol in ADR 0008.
- [x] Implement stable installation identity and admin-authorised device registration.
- [x] Bind paired devices to the Home Assistant user who authorised pairing.
- [x] Implement owner-checked session creation, nonce rotation, sequence/replay protection, rate limits, payload limits and sensor-ID validation.
- [x] Register the PC in the Home Assistant device registry and create dynamic sensor entities.
- [x] Add individual sensor freshness/availability tracking and administrative revocation.
- [x] Add a separate outbound telemetry WebSocket client; retain the existing state-change WebSocket for its own purpose.
- [x] Add a Sensors-page publishing control with per-sensor selection, Pair/Update and Unpair actions.
- [x] Persist the random installation ID, paired device ID and non-secret sensor selection; do not persist session nonces or readings.
- [x] Add Python protocol unit tests and run them in Windows CI.
- [ ] Validate the pairing flow and published entities against the user's live Home Assistant instance.
- [ ] Validate Home Assistant restart, Windows sleep/resume, reconnect, revocation, non-admin pairing rejection and replay rejection end-to-end.

Acceptance:
- A paired PC appears as one device in Home Assistant.
- Only user-selected sensors create stable entities and update without duplicate entities after restart/re-pair.
- Each sensor becomes unavailable after 15 seconds without a fresh reading.
- Revoking the pairing stops publishing and removes the PC entities/device.
- No arbitrary command execution is possible through the telemetry channel.
- Protocol tests cover invalid identities, metadata, values, replay/sequence, rate limits, oversized batches, stale data and reconnect.
- Live pairing and lifecycle behaviour are confirmed on the user's HA installation before this milestone is called accepted.

### Milestone 4 — Native Windows control capabilities

**Goal:** Add useful Windows control features with safe, explicit behaviour.

Current implementation note (2026-11-08): native master volume and mute are being delivered in draft PR #22, stacked on Home Assistant sensor-publishing PR #21. The Applications & Media page reads and controls the default Windows playback endpoint using Core Audio's IAudioEndpointVolume API. The code includes finite/clamped volume conversion and unit tests; real device and keyboard-volume synchronization still require Windows acceptance.

Suggested order:
1. Master volume and mute.
2. Default output device selection.
3. Media transport controls and now-playing metadata.
4. Application discovery and launch.
5. Focus/minimise/close application controls.
6. Per-application audio levels/output routing where supported.
7. Selected safe system actions (lock, sleep, restart/shutdown only with explicit local confirmation and opt-in).

Architecture:
- Windows APIs live in DesktopCompanion.Windows.
- UI and Home Assistant integrations call shared application services.
- Use stable action IDs and typed parameters; do not accept raw shell commands or arbitrary executable paths from HA.
- Separate local-only actions from actions exposed to Home Assistant.
- Define confirmation policies and allowlists for disruptive actions.

Acceptance:
- Each feature works from the native app first.
- Optional Home Assistant exposure uses the same typed action contract.
- Disruptive actions cannot execute silently from an unauthorised or stale request.
- Unsupported device/API capabilities are shown clearly.

### Milestone 5 — Actions and quick surfaces

**Goal:** Make common combinations accessible without opening the full application.

Work:
- Create a shared action model (name, icon, category, typed operation, parameters, confirmation policy).
- Add native quick actions in the tray popup.
- Allow user-defined favourites and ordering.
- Add a compact tray dashboard/surface only after the main dashboard selector and WebView2 lifecycle are stable.
- If a compact surface embeds Home Assistant content, reuse the same security/session rules and avoid creating competing WebView2 profiles.
- Later: multi-step actions with explicit sequencing, failure handling, and cancellation.

Acceptance:
- User can run favourite actions from the tray without opening the main window.
- Each action has predictable feedback and error handling.
- No arbitrary scripts or unaudited action payloads are accepted.

### Milestone 6 — Notifications and event handling

**Goal:** Surface useful Home Assistant and Windows events without becoming noisy.

Work:
- Windows toast notifications for selected Home Assistant events and companion states.
- Configurable per-event enablement, priority, and quiet hours.
- Actionable notifications only for safe, clearly defined actions.
- Deduplicate repeated events and provide cooldowns.
- Keep notification delivery independent of whether the main window is visible.
- Allow the user to open the relevant dashboard/view from a notification.

Acceptance:
- Notifications are opt-in/configurable and never flood the desktop on reconnect loops.
- Clicking a notification opens the correct native page or Home Assistant dashboard.
- Quiet hours and per-category controls are respected.

### Milestone 7 — Applications, media, and desktop integration

**Goal:** Make the PC feel like a first-class Home Assistant device.

Work:
- Application library with discovery, favourites, launch, and status.
- Now-playing metadata and global media controls.
- Audio session and output-device information.
- Global keyboard shortcuts, with configurable key bindings and conflict detection.
- Windows startup preference (off by default until user opts in).
- Optional presence/session information, with privacy controls.

Acceptance:
- Features use documented Windows APIs and degrade gracefully on unsupported hardware.
- User can choose what is exposed to Home Assistant.
- Shortcuts do not steal common system/application combinations by default.

### Milestone 8 — Settings, persistence, accessibility, and release quality

**Goal:** Turn the preview into a maintainable v1.0 application.

Work:
- Add SQLite for non-secret configuration: dashboard definitions, sensor preferences, action definitions, favourites, and app preferences.
- Keep all credentials and pairing secrets in OS-protected storage.
- Settings for startup, tray behaviour, dashboard selection, polling intervals, notifications, privacy, and diagnostics.
- Add export/import for non-secret configuration.
- Ensure keyboard navigation, screen-reader names, contrast, high-DPI, multi-monitor placement, and responsive resizing.
- Add localisation infrastructure (English and Dutch first; do not mix languages unpredictably in one screen).
- Installer: install/upgrade/uninstall/reinstall, per-user permissions, repair, and clear error reporting.
- Code signing and update/release process can be evaluated before public v1.0.
- Security review, performance profiling, compatibility matrix, crash diagnostics, and end-user documentation.

Acceptance:
- Settings survive app upgrades and are removed only according to the uninstaller's explicit user choice.
- No secrets appear in logs, exports, or plaintext files.
- App behaves correctly across display scaling and monitor changes.
- Installer lifecycle is tested on a clean Windows 11 machine.
- Release artifacts, versioning, changelog, and support instructions are repeatable.

---

## 5. Prioritised backlog

The next work should be selected from this order unless new user feedback changes priority:

| Priority | Work item | Why now | Depends on |
|---|---|---|---|
| P0 | Finish review of the current dashboard/installer PR stack | Keep the verified preview stable | User approval to merge |
| P0 | Real in-place installer upgrade test | Previous installer failures were user-visible | PR #14 candidate build |
| P1 | Validate native dashboard selector and remembered view (PR #18) on Windows | Confirm the new three-view workflow end-to-end | Current WebView2 host and live HA instance |
| P1 | Automated tests for dashboard URL/path/query handling | Protect reverse-proxy and Kiosk Mode behaviour | Test project |
| P1 | CPU/memory/storage/network sensor vertical slice | First differentiated native Windows feature | Sensor provider service |
| P1 | Telemetry protocol ADR | Required before publishing Windows entities to HA | Sensor model and security review |
| P2 | Device pairing + two initial HA sensor entities | Connect Windows monitoring to HA | Accepted telemetry ADR |
| P2 | Master volume/mute + now-playing | High-value Windows-native controls | Windows service layer |
| P2 | Tray favourites and quick actions | Make the app useful when hidden | Shared action model |
| P3 | Notifications, application library, per-app audio, multi-step actions | Broader product completion | Earlier native services |
| P3 | Persistence, accessibility, localisation, signing, v1.0 release | Stabilisation | Feature set mature |

### Immediate next implementation slice

Draft PR #21 contains the secure HA pairing and selected-sensor publishing slice; live HA pairing/restart/revocation acceptance is still required. Draft PR #22 begins Phase 4 with native master volume and mute. After CI review and Windows hardware acceptance, proceed with default output-device selection and global media-session controls, then finish the provider registry and sensor settings.

---

## 6. Engineering and collaboration rules

- Work autonomously on well-defined tasks. Ask only when a decision materially changes product behaviour, security, or data loss risk.
- Prefer one coherent vertical slice per pull request.
- Draft PRs are the default while work is in progress. Do not merge any PR without the user's explicit permission for that task.
- Do not create releases or label an installer as production-ready until its relevant Windows install/upgrade test has passed.
- Each PR must state: user-facing change, architecture impact, security impact, tests run, CI evidence, and what still requires real Windows validation.
- Keep roadmap, product plan, README, ADRs, and implementation aligned. Never mark work complete based solely on a code change.
- Preserve the existing WebView2 user profile and native OAuth separation.
- Use real supported Home Assistant/Windows contracts. Avoid brittle DOM/CSS injection into the HA frontend.
- No arbitrary command execution; no inbound unauthenticated listener.
- Update this document's **Current implementation and verified baseline**, **Immediate next implementation slice**, and the roadmap checklist when work is completed or reprioritised.

### Per-session restart checklist

At the start of a new session:
1. Read this file first.
2. Read [the roadmap](roadmap.md) and relevant ADRs.
3. Inspect open PRs and their CI status; do not assume this document's PR numbers or branch heads are still current.
4. Check whether there are user test results newer than this file.
5. Continue from the highest-priority unfinished backlog item.
6. Give the user a concise plan before a substantial change and report changed files, PR, tests, and blockers.

---

## 7. Current repository handoff (2026-11-08)

At the time of writing:

- Default branch is `main`.
- The integrated baseline is `main` at `5740d72dba1a9e2a496abd11661b43835e1587c3`. GitHub showed no open pull requests during the 2026-11-08 review; the earlier PR #14/#15 stack references above this handoff are historical and have been integrated.
- Windows CI for the current main commit passed in run [#42522021504](https://github.com/remco59/ha-desktop-companion/actions/runs/42522021504). Windows Package also passed for the preceding dashboard PR head [#42521816660](https://github.com/remco59/ha-desktop-companion/actions/runs/42521816660).
- Real in-place installer upgrade acceptance remains outstanding and must stay visible until tested on Windows.
- PR #18 — [native Home Assistant dashboard selector](https://github.com/remco59/ha-desktop-companion/pull/18) — open draft on `feat/native-dashboard-selector`. The user confirmed the Control dashboard opens in the embedded dashboard view on Windows 11 (2026-11-08); multi-view persistence and reconnect acceptance remain to be verified. Do not merge without explicit user permission.
- PR #19 — [Windows CPU, memory, and uptime sensors](https://github.com/remco59/ha-desktop-companion/pull/19) — draft, stacked on PR #18. Windows CI and installer packaging passed; the user compared CPU, memory, and uptime with Windows Task Manager on Windows 11 and accepted the readings, including roughly one-second display lag.
- PR #20 — [per-volume storage and per-adapter network sensors](https://github.com/remco59/ha-desktop-companion/pull/20) — draft, stacked on PR #19. Adds fixed/removable volume capacity/used/available/status and adapter status plus receive/send rates, with dynamic cards on the Sensors page.
- PR #21 — [secure Home Assistant pairing and sensor publishing](https://github.com/remco59/ha-desktop-companion/pull/21) — draft, stacked on PR #20. Implements ADR 0008, HA device/entity registration, session-secured telemetry, Windows sensor selection/publishing controls, and Python protocol tests. Live pairing acceptance remains outstanding.
- PR #22 — [native Windows master volume and mute](https://github.com/remco59/ha-desktop-companion/pull/22) — draft, stacked on PR #21. Adds Core Audio endpoint volume/mute controls to Applications & Media; real playback-device validation remains open.
- The current project plan is documentation, not permission to merge, release, or change production Home Assistant configuration.
