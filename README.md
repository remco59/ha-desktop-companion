# Desktop Companion

A native Windows companion for Home Assistant.

Desktop Companion brings your Windows PC and smart home together in one approachable control centre. It is designed around a full Windows application and a configurable system-tray dashboard, with shared sensors, actions, widgets, and configuration.

> **Project status:** Early development. The initial foundation is being established; features described here are planned, not yet implemented.

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
