# Architecture overview

## Product shape

Desktop Companion is a Windows-first Home Assistant companion composed of:

1. A native WinUI 3 desktop application.
2. Shared .NET application and domain libraries.
3. Windows-specific capability providers.
4. A Home Assistant custom integration for native device/sensor registration.
5. An optional MQTT telemetry transport.

The first release targets one Home Assistant instance and one interactive Windows user session.

## Runtime principles

- The native desktop shell and background work share application services but have separate lifecycles.
- Closing the main window must not stop background monitoring.
- Explicit Exit shuts down the application host.
- Sensors, actions, entities, and widgets have stable identifiers and shared definitions.
- UI widgets call application services; they must not contain their own Windows or Home Assistant transport logic.
- No inbound unauthenticated HTTP listener is exposed by the Windows application.

## Projects

- `DesktopCompanion.Core`: domain models and interfaces; no Windows or UI dependencies.
- `DesktopCompanion.HomeAssistant`: Home Assistant REST/WebSocket and telemetry transport adapters.
- `DesktopCompanion.Windows`: Windows sensor, audio, process, shortcut, and tray adapters.
- `DesktopCompanion.App`: WinUI 3 presentation layer and composition root.

## Home Assistant connectivity

The Windows application initiates outbound connections to Home Assistant. The REST API is used for requests and service calls; the WebSocket API is used for live state subscriptions and events. A dedicated integration provides device registration and the authenticated telemetry contract. MQTT Discovery is an optional alternate telemetry transport.

The exact pairing and WebSocket message contract is a Phase 0 design gate. Do not ship sensor publishing until authentication, authorization, replay protection, device identity, availability, and reconnect behaviour are specified and tested.

## Local persistence

SQLite will hold non-secret configuration: dashboard layouts, widget configuration, sensor preferences, application entries, action definitions, and user preferences. Refresh/access credentials and pairing secrets must be stored in Windows Credential Manager or protected using DPAPI, never in plain-text SQLite or logs.

## UI

The application shell, navigation, connection settings, notifications, and tray experience are native WinUI 3. The Home Assistant dashboard is rendered by the actual Home Assistant frontend inside WebView2; Desktop Companion must not recreate Lovelace cards, entity toggles, or dashboard controls as native lookalikes. Native Windows surfaces may wrap, launch, or complement Home Assistant views, while calls that need native Windows integration go through application services.

The WebView2 dashboard uses a dedicated persistent profile under the current Windows user's LocalAppData folder. Home Assistant handles its own frontend sign-in within that profile. This session is separate from the native OAuth credentials used for REST and WebSocket access; access and refresh tokens must never be injected into dashboard JavaScript or exposed through a page bridge. The default Evergreen WebView2 Runtime is used, with a clear error when the runtime is unavailable.
