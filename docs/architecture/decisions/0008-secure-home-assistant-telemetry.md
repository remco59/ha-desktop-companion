# ADR 0008: Secure Windows sensor pairing and telemetry

- Status: Accepted for implementation; Phase 0 end-to-end validation remains required
- Date: 2026-11-08

## Context

Desktop Companion already authenticates to Home Assistant using OAuth PKCE and stores the refresh credential in Windows Credential Manager. The Windows sensor engine samples locally, but Home Assistant has no paired PC device or sensor entities yet.

The Windows application must initiate all network connections. No inbound listener, MQTT broker requirement, long-lived pairing secret in settings, or token bridge into WebView2 is acceptable.

## Decision

Use the existing authenticated Home Assistant WebSocket API and a custom command namespace supplied by the Desktop Companion Home Assistant integration.

### Pairing and identity

1. The user installs and adds the Desktop Companion integration once in Home Assistant.
2. The user explicitly selects Pair with Home Assistant in the Windows app.
3. The app sends a random installation UUID, device name, app version, and a bounded catalogue of sensor metadata over the authenticated WebSocket connection.
4. The Home Assistant integration accepts registration only through an administrator-authorised WebSocket command.
5. The integration records the authenticated Home Assistant user ID as the owner and registers the PC in the Home Assistant device registry.
6. The random installation UUID is a stable identifier, not a credential. No hardware serial number, MAC address, or machine GUID is collected.

No separate shared pairing password is introduced. The existing OAuth access token authenticates the WebSocket session; the refresh token remains in Windows Credential Manager.

### Publishing

- Desktop Companion starts a telemetry session after each WebSocket connection or reconnect.
- Home Assistant returns a cryptographically random, in-memory session nonce. The nonce is rotated on a new session and is not written to disk.
- Every publication includes the nonce and a strictly increasing sequence number.
- Home Assistant verifies the paired device, owner user ID, nonce, sequence, registered sensor IDs, state types, and rate limits before accepting the batch.
- The publisher sends only user-selected sensor values. The first release caps a batch at 100 readings and 32 KiB, with a minimum one-second interval.
- Numeric and short text states are supported. Arbitrary attributes, commands, templates, shell instructions, and entity metadata changes are not accepted in telemetry messages.
- Sensor metadata is registered during pairing and persisted as non-secret config-entry data. Sensor values and session nonces remain in memory only.
- The integration exposes a device and sensor entities with timestamps and availability. A device becomes unavailable when no valid batch has arrived for 15 seconds.

### Revocation and ownership

- An administrator can revoke a paired device from Home Assistant; revocation removes its stored registration and sensor entities.
- Publishing is accepted only from the same Home Assistant user that paired the device.
- A new session invalidates the previous nonce for that device.
- Replaying a sequence number, using an old nonce, publishing an unknown sensor ID, or sending malformed/oversized data is rejected.

### Transport and lifecycle

- The Windows app initiates an authenticated outbound secure WebSocket connection to Home Assistant's existing WebSocket endpoint, preserving reverse-proxy path prefixes.
- The telemetry command channel is separate from the existing state_changed subscription connection, so it cannot consume or discard state events.
- The sender reconnects through the existing token provider. On a new socket it authenticates and obtains a new telemetry nonce before publishing.
- MQTT remains optional and is not part of this first implementation.

## Security properties

- No inbound HTTP/WebSocket listener is opened on Windows.
- No OAuth access or refresh token is exposed to WebView2, JavaScript, logs, or telemetry payloads.
- Pairing requires an authenticated Home Assistant administrator action initiated by the user.
- Publish/unpair commands enforce the paired owner or administrator as appropriate.
- Installation ID, sensor IDs, value types, metadata lengths, rate, sequence numbers, and payload size are validated.
- Telemetry values are data only; there is no remote command execution path.
- Stale sensor readings are not silently republished as fresh values.

## Consequences

- Home Assistant must have the Desktop Companion custom integration installed and configured before pairing.
- The Windows app must reconnect the telemetry session after a WebSocket disconnect or Home Assistant restart.
- If the pairing user's Home Assistant permissions are removed, publication stops until an administrator re-pairs the PC.
- The initial protocol is deliberately local-instance and single-user oriented; multiple owners and multi-user Windows sessions are out of scope.

## Phase 0 acceptance

- Pair one Windows 11 PC to a real Home Assistant instance using an administrator account.
- Confirm that the device and selected sensor entities appear in the Home Assistant device registry and entity registry.
- Compare at least CPU, memory, uptime, one storage volume, and one active network adapter with the local Sensors page.
- Restart Home Assistant and verify session recovery and fresh availability.
- Revoke the device and verify that further publication is rejected and its sensor entities are removed.
- Verify user/owner rejection, replay rejection, invalid sensor IDs, rate limits, message-size limits, and stale/unavailable handling.
