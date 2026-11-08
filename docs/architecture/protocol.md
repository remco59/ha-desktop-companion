# Home Assistant communication contract

## Outbound requests

- REST: connection verification, entity snapshots, and Home Assistant service calls.
- WebSocket: authentication, entity state subscriptions, and event subscriptions.
- Optional MQTT: MQTT Discovery configuration and telemetry topics.

## Live state updates

The Home Assistant WebSocket client performs one authenticated connection:

1. Connect to `/api/websocket` derived from the configured base URL, preserving any reverse-proxy path prefix.
2. Require the `auth_required` handshake.
3. Send the access token in the WebSocket `auth` message.
4. Require `auth_ok`; an `auth_invalid` response is reported as an authentication failure.
5. Subscribe to `state_changed` events using a request ID.
6. Yield typed event objects containing a cloned JSON payload and the event timestamp where available.

The WebSocket client handles a single connection and cancellation. Reconnection, exponential backoff, connection status, and token refresh belong to an application-level connection supervisor and authentication provider. Tokens must never be logged.

## Native integration telemetry

Before implementing telemetry, define and test:

- Pairing and device identity.
- Authenticated WebSocket command name and payload schema.
- Sensor identifier and metadata validation.
- Numeric and textual state handling.
- Units and Home Assistant device/state classes.
- Update-rate limits and payload-size limits.
- Availability, last-seen time, and stale-data behaviour.
- Reconnect and pairing-revocation behaviour.

No arbitrary Windows command execution is part of the telemetry protocol. Remote actions must use a separate allowlisted action contract.
