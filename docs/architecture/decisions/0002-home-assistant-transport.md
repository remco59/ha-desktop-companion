# ADR 0002: Home Assistant and telemetry transport

- Status: Accepted with Phase 0 validation
- Date: 2026-11-08

## Decision

Use Home Assistant REST for requests and service calls and WebSocket for live state updates. Use a dedicated Home Assistant custom integration for native Windows device and sensor registration. Support MQTT Discovery as an optional telemetry transport, not a mandatory dependency.

## Rationale

Users should not need to install or configure an MQTT broker just to connect a Windows PC. The companion integration gives the project a Home Assistant-native place to register the PC and expose its sensors. MQTT remains useful for existing installations and power users.

## Security constraints

- Windows initiates outbound connections to Home Assistant.
- Never expose an unauthenticated command server on the Windows machine.
- Pairing secrets must not appear in URLs, logs, or plain-text settings.
- The telemetry contract must be authenticated, scoped to one paired device, validated, rate-limited, and resilient to reconnects.
- The integration must expose availability and stale-data handling.

## Validation required

Phase 0 must prove the end-to-end pairing and telemetry flow on a real Home Assistant instance before this transport is treated as production-ready.
