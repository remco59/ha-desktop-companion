# Desktop Companion

A native Windows companion for Home Assistant.

Desktop Companion brings your Windows PC and smart home together in one approachable control centre. It is designed around a full Windows application and a configurable system-tray dashboard, with shared sensors, actions, widgets, and configuration.

> **Project status:** Early preview. The native WinUI 3 shell, tray experience, Home Assistant OAuth/REST/WebSocket connection, and embedded WebView2 dashboard are implemented. Dashboard-only Kiosk Mode has been verified by the user on Windows 11. Windows sensor publishing, pairing, native Windows controls, and the broader roadmap remain in development. See the [Product Plan & Development Handoff](docs/product-plan.md) for current status and next steps.

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

## Building

The Windows application is built on Windows with the .NET 10 SDK, Visual Studio 2026 (or an equivalent WinUI 3 toolchain), and Windows App SDK 2.6.5. Build instructions will be expanded as the project foundation lands.

## Open source

This project is licensed under the MIT License. Third-party dependencies retain their respective licences; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Disclaimer

Desktop Companion is an independent community project and is not affiliated with or endorsed by Nabu Casa or the Home Assistant project.
