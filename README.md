# Desktop Companion

A native Windows companion for Home Assistant.

Desktop Companion brings your Windows PC and smart home together in one approachable control centre. It is designed around a full Windows application and a configurable system-tray dashboard, with shared sensors, actions, widgets, and configuration.

> **Project status:** Early preview. The native WinUI 3 shell, tray experience, Home Assistant OAuth/REST/WebSocket connection, embedded WebView2 dashboard, and Windows system sensors are implemented in the current draft PR stack. The user has verified the dashboard and CPU/memory/uptime readings on Windows 11. Secure Home Assistant device pairing and selected-sensor publishing are implemented in draft PR #21; live pairing against a real Home Assistant instance is still an acceptance test. Native Windows controls and the rest of the roadmap remain in development. See the [Product Plan & Development Handoff](docs/product-plan.md) for current status and next steps.

## Dashboard presentation

The embedded dashboard opens in dashboard-only mode by requesting the Home Assistant Kiosk Mode frontend resource with the `?kiosk` URL parameter. Install [**Kiosk Mode**](https://github.com/NemesisRE/kiosk-mode) through HACS in Home Assistant to hide Home Assistant's own header and navigation while keeping the actual Lovelace dashboard interactive. Without that frontend resource, Home Assistant displays its regular interface. The native Desktop Companion shell remains visible.

## Project plan

The [Product Plan & Development Handoff](docs/product-plan.md) is the source of truth for the product vision, architecture constraints, current verified baseline, delivery phases, and next implementation slice. The [implementation roadmap](docs/roadmap.md) provides a concise checklist.

## Product goals

- Control Home Assistant entities, scenes, scripts, and automations from Windows.
- Publish Windows system and hardware telemetry as Home Assistant entities.
- Control Windows audio, applications, media, and selected system actions.
- Provide a full dashboard and a compact, configurable tray pop-up.
- Keep MQTT optional.
- Make common tasks discoverable for beginners while allowing advanced configuration.

## Initial technical direction

- C# / .NET 10 LTS
- WinUI 3 / Windows App SDK
- MVVM and dependency-injected application services
- Home Assistant REST and WebSocket APIs
- A companion Home Assistant custom integration for native sensor registration
- Optional MQTT Discovery transport
- SQLite for local configuration and dashboard layouts
- GitHub Actions for build, test, and release automation

## Repository layout

- `src/` — Windows application and shared .NET projects
- `integrations/homeassistant/` — Home Assistant custom integration
- `tests/` — automated tests
- `docs/` — architecture, decisions, and user/developer documentation
- `installer/` — Windows installer assets and scripts

## Development status

See [the implementation roadmap](docs/roadmap.md) and [architecture overview](docs/architecture/overview.md).

## Home Assistant integration

To prepare Home Assistant for Windows sensor pairing:

1. Copy `integrations/homeassistant/custom_components/desktop_companion` into `<config>/custom_components/desktop_companion`.
2. Restart Home Assistant.
3. Open **Settings → Devices & services → Add integration** and add **Desktop Companion** once.
4. Connect the Windows app to the same Home Assistant instance with an administrator account.
5. Open **Sensors**, choose the readings to expose, and select **Pair with Home Assistant**.
6. The Windows PC should appear as one Desktop Companion device. Only the selected sensors are published.

The first pairing/publishing implementation is in draft PR #21 and has not yet been validated against the user's live Home Assistant instance. Do not use the workflow as a production security certification until that acceptance is complete. The app makes outbound connections only; it does not open an inbound Windows port.

## Building

The Windows application is built on Windows with the .NET 10 SDK, Visual Studio 2026 (or an equivalent WinUI 3 toolchain), and Windows App SDK 2.6.5.

## Open source

This project is licensed under the MIT License. Third-party dependencies retain their respective licences; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Disclaimer

Desktop Companion is an independent community project and is not affiliated with or endorsed by Nabu Casa or the Home Assistant project.
