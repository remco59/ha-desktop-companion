# Home Assistant communication contract

## Outbound requests

- REST: connection verification, entity snapshots, and Home Assistant service calls.
- WebSocket: authentication, entity state subscriptions, events, and the separate sensor telemetry command channel.
- Optional MQTT: not required for the first release; evaluate only if it adds value later.

## Live state updates

The Home Assistant state WebSocket client performs one authenticated connection:

1. Connect to `/api/websocket` derived from the configured base URL, preserving any reverse-proxy path prefix.
2. Require the `auth_required` handshake.
3. Send the access token in the WebSocket `auth` message.
4. Require `auth_ok`; an `auth_invalid` response is reported as an authentication failure.
5. Subscribe to `state_changed` events using a request ID.
6. Yield typed event objects containing a cloned JSON payload and the event timestamp where available.

The existing client handles one connection and cancellation. Reconnection, exponential backoff, connection status, and token refresh belong to the application-level connection supervisor and authentication provider. Tokens must never be logged.

## Native Windows sensor pairing and telemetry

The first protocol is defined in [ADR 0008](decisions/0008-secure-home-assistant-telemetry.md) and implemented in draft PR #21.

### Pairing

1. The user installs `integrations/homeassistant/custom_components/desktop_companion` under Home Assistant's `custom_components` directory, restarts Home Assistant, and adds the Desktop Companion integration once.
2. The user connects Desktop Companion using the normal OAuth PKCE flow.
3. In the Sensors page, the user chooses which discovered Windows sensors may be shared and selects **Pair with Home Assistant**.
4. The Windows app sends a random installation UUID, machine display name, app version, and bounded sensor catalogue over its separate authenticated outbound WebSocket.
5. The pairing command requires Home Assistant administrator privileges. The integration binds the device to the authenticated Home Assistant user and adds it to the device registry.
6. The integration stores non-secret registration metadata in its config entry. It stores sensor values, per-sensor timestamps, session nonces, and replay counters in memory only.

The installation UUID is an identifier, not a secret. No hardware serial, MAC address, or machine GUID is collected.

### Session and publishing

- The telemetry WebSocket is separate from the state-change subscription WebSocket; a telemetry command can never consume an event from the existing event stream.
- On each new authenticated telemetry connection, Desktop Companion requests a fresh session nonce.
- Every publish carries the device ID, session nonce, monotonically increasing sequence number, and only the selected sensor readings.
- The integration verifies owner user ID, nonce, strictly increasing sequence, sensor registration, value type/range, payload size, and minimum interval.
- Maximum 100 registered sensors and 32 KiB per readings batch; publish rate is limited to one batch per second. The Windows sampler currently runs every two seconds.
- Numeric finite values and short printable text are supported. Booleans, NaN, infinity, unknown IDs, duplicate readings, control characters, and oversized payloads are rejected.
- Stale Windows readings are not republished as fresh values.
- Each sensor becomes unavailable 15 seconds after its last valid reading.

### Revocation and recovery

- An administrator can revoke the paired Windows PC from Desktop Companion's **Unpair PC** action.
- Revoke removes the persisted device registration, sensor entities, and device registry record.
- A new telemetry session rotates the in-memory nonce, invalidating the previous session.
- After Home Assistant or Windows reconnect, the Windows client authenticates again, starts a new session and resumes publishing. If the pairing is no longer present, it must be paired again.
- The integration binds a device to the HA user who paired it. A different user cannot publish or take over the same installation ID.

### Security boundaries

- Desktop Companion initiates all network connections; no inbound Windows HTTP or WebSocket listener is created.
- Native OAuth access and refresh tokens are never exposed to WebView2, page storage, telemetry payloads, or logs.
- The Home Assistant session nonce and live telemetry values are memory-only.
- Telemetry payloads are data only. There is no shell, PowerShell, process-launch, template, or arbitrary service-call field.
- Pairing and unpairing require an administrator. Publishing requires the owner user.

## Remote actions are a separate protocol

No arbitrary Windows command execution is part of sensor telemetry. Future remote actions must use a separate, allowlisted, typed action contract with explicit confirmation policies, authorisation, replay protection, and auditability.
