# Home Assistant communication contract

This document is a design checkpoint, not a published protocol specification.

## Outbound requests

- REST: connection verification, entity snapshots, and Home Assistant service calls.
- WebSocket: authentication, entity state subscriptions, and event subscriptions.
- Optional MQTT: MQTT Discovery configuration and telemetry topics.

## Native integration telemetry

Before implementation, define and test:

- Pairing and device identity.
- Authenticated WebSocket command name and payload schema.
- Sensor identifier and metadata validation.
- Numeric and textual state handling.
- Units and Home Assistant device/state classes.
- Update-rate limits and payload-size limits.
- Availability, last-seen time, and stale-data behaviour.
- Reconnect and pairing-revocation behaviour.

No arbitrary Windows command execution is part of the telemetry protocol. Remote actions must use a separate allowlisted action contract.
