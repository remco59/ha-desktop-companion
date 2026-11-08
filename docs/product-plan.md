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
- Append the Kiosk Mode \`?kiosk\` parameter when loading the configured instance URL.
- Preserve reverse-proxy path prefixes and existing query parameters.
- Respect an explicit \`disable_km\` opt-out.
- The Kiosk Mode frontend resource must be installed in Home Assistant through HACS. Without it, Home Assistant displays its normal frontend chrome.
- Dashboard selection should navigate to Home Assistant dashboard/view URLs; do not query dashboard configuration and redraw it natively.

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
- WebSocket client for authenticated \`state_changed\` subscriptions.
- Connection supervisor with status events and retry/backoff.
- Entity discovery was tested against the user's Home Assistant instance and returned **629 entities**.
- WebView2 hosts the real Home Assistant frontend with a persistent, dedicated browser profile.
- Explicit Disconnect clears the native credentials and the dedicated WebView2 profile.
- Kiosk Mode \`?kiosk\` dashboard presentation is implemented.
- Windows CI and Windows package/installer workflows build successfully.

### User-verified on Windows 11 — 2026-11-08

- The installed app connects to the real Home Assistant instance.
- The Home Assistant dashboard loads inside the native Desktop Companion window.
- Kiosk Mode hides Home Assistant's own navigation/header; the dashboard itself remains interactive.
- The clean dashboard presentation has been explicitly accepted by the user (screenshot shared 2026-11-08).

Treat this as the current accepted baseline. Do not re-open the decision to embed the real frontend or show its native HA chrome unless the user asks.

### Still incomplete or not yet acceptance-tested

- Installer in-place upgrade behaviour has not yet been confirmed by a clean upgrade test. PR #14 changes the Inno Setup file handling and process detection; CI packaging success is not proof of a successful upgrade on a real Windows machine.
- A full single-instance activation handoff is missing.
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
- Confirm that \`CloseApplicationsFilter\` only targets \`DesktopCompanion.App.exe\` and that unchanged .NET runtime files are not needlessly replaced.
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

Work:
- Add a native dashboard selector for available Home Assistant dashboards/views.
- Start with a simple configuration model: friendly display name + Home Assistant path/URL + optional icon.
- Include a sensible default (the Home Assistant default dashboard) and remember the last selected dashboard.
- Navigate the existing WebView2 instance to the selected Home Assistant path, preserving the configured reverse-proxy prefix and Kiosk Mode parameter.
- Add loading, offline, navigation-failure, and reconnect states that do not destroy the persisted browser session.
- Add a clear way to return to the default dashboard.
- Keep refresh and Open in browser actions; ensure Open in browser opens the currently selected dashboard.
- Consider per-dashboard kiosk preferences only if there is a real use case; default remains dashboard-only.

Acceptance:
- User can switch between at least two configured dashboards/views without seeing Home Assistant's header/sidebar.
- Cards and controls remain native Home Assistant components and fully interactive.
- Dashboard selection survives app restart.
- No second Home Assistant renderer or JavaScript bridge is introduced.

### Milestone 2 — Windows sensor engine (first true Windows-native feature)

**Goal:** Show reliable Windows PC telemetry in Desktop Companion and prepare it for Home Assistant publishing.

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
- Define stable sensor IDs, units, categories, precision, availability, sample timestamps, and error states.
- Create a provider registry so unavailable hardware providers do not break other sensors.
- Use OS-supported APIs/performance counters first. Keep hardware-vendor-specific sources behind separate providers.
- Define configurable sampling intervals and avoid high-frequency polling by default.
- Cache last known reading and mark it stale/unavailable when polling fails.
- Build a Sensors page with category, current value, unit, availability, and last-updated timestamp.
- Add an overview summary for CPU and memory after the sensor service is reliable.
- Add capability diagnostics (what the machine supports and why a sensor is unavailable).

Acceptance:
- Sensor readings are accurate enough for monitoring, timestamped, and refresh without freezing the UI.
- Missing GPU/temperature support does not break CPU, memory, storage, or network.
- CPU usage sampling does not report an unprimed first counter value as real usage.
- User can configure sampling cadence and disable individual sensors.
- App remains responsive with the window hidden in the tray.

### Milestone 3 — Secure Home Assistant device pairing and telemetry

**Goal:** Register the Windows PC as a Home Assistant device and expose selected Windows sensors as HA entities.

This milestone starts with a protocol/design ADR, not code.

Required design decisions:
- Pairing UX and identity (one-time code or equivalent short-lived pairing mechanism).
- How the Windows app authenticates to the Home Assistant integration without embedding a long-lived shared secret in plaintext.
- Whether messages use an authenticated outbound channel or a deliberately designed alternative. Do not introduce an inbound listener casually.
- Device ID stability, naming, re-pairing, secret rotation, and revocation.
- Replay protection, timestamps/nonces, payload limits, schema/version negotiation, and rate limits.
- Availability/heartbeat/last-seen and stale sensor behaviour.
- Sensor metadata mapping to Home Assistant device class, state class, unit, entity category, and suggested entity IDs.
- Behaviour on HA restart, Windows sleep/resume, connection loss, and credential expiration.

Implementation sequence after the ADR is accepted:
1. Config flow and pairing UI.
2. Device registry entry and unique device identity.
3. Authenticated telemetry message contract.
4. One or two sensors end-to-end (uptime and memory are good first candidates).
5. Availability and reconnect behaviour.
6. Sensor selection and publishing controls.
7. Expand to the full sensor set.

Acceptance:
- A paired PC appears as one device in Home Assistant.
- Selected sensors create stable entities and update without duplicate entities after restart/re-pair.
- Revoking a pairing stops publishing immediately.
- No arbitrary command execution is possible through the telemetry channel.
- The protocol has tests for invalid credentials, replay, oversized payloads, stale data, and reconnect.

### Milestone 4 — Native Windows control capabilities

**Goal:** Add useful Windows control features with safe, explicit behaviour.

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
| P1 | Native dashboard selector and remembered view | Natural continuation of the accepted dashboard experience | Current WebView2 host |
| P1 | Automated tests for dashboard URL/path/query handling | Protect reverse-proxy and Kiosk Mode behaviour | Test project |
| P1 | CPU/memory/storage/network sensor vertical slice | First differentiated native Windows feature | Sensor provider service |
| P1 | Telemetry protocol ADR | Required before publishing Windows entities to HA | Sensor model and security review |
| P2 | Device pairing + two initial HA sensor entities | Connect Windows monitoring to HA | Accepted telemetry ADR |
| P2 | Master volume/mute + now-playing | High-value Windows-native controls | Windows service layer |
| P2 | Tray favourites and quick actions | Make the app useful when hidden | Shared action model |
| P3 | Notifications, application library, per-app audio, multi-step actions | Broader product completion | Earlier native services |
| P3 | Persistence, accessibility, localisation, signing, v1.0 release | Stabilisation | Feature set mature |

### Immediate next implementation slice

After the current PR stack is reviewed/merged with the user's explicit approval:

**Build the native dashboard selector as one vertical slice.**

It should include:
- A small dashboard definition model (friendly name, path, optional icon, sort order).
- A native selector in the Dashboard page.
- Default and last-selected dashboard behaviour.
- URL composition tests for base paths, reverse proxies, existing query parameters, Kiosk Mode, and opt-out.
- No native recreation of HA dashboard cards or dashboard configuration APIs unless required to discover dashboard paths safely.

Then move to the first Windows sensor vertical slice (CPU + memory + uptime), before designing telemetry publication.

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

- Default branch is \`main\`.
- PR #14 — [installer upgrade handling](https://github.com/remco59/ha-desktop-companion/pull/14) — draft, not merged. It contains the \`replacesameversion\` and targeted forced-close/filter changes. CI packaging passed for the recorded head; real in-place upgrade acceptance remains outstanding.
- PR #15 — [dashboard-only Kiosk Mode](https://github.com/remco59/ha-desktop-companion/pull/15) — draft, not merged. It is stacked on the installer-fix branch. Windows CI and Windows packaging passed for the recorded head. The user installed it and confirmed the Home Assistant header/sidebar are hidden while the dashboard remains visible and interactive.
- Keep the PR stack intact until reviewed. The documentation handoff PR may be stacked on PR #15; do not merge or rebase the stack without checking current GitHub state.
- The current project plan is documentation, not permission to merge, release, or change production Home Assistant configuration.
