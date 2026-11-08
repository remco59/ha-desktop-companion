# ADR 0004: Home Assistant real-time client

- Status: Accepted
- Date: 2026-11-08

## Decision

Use Home Assistant's native WebSocket API for live state updates. The client authenticates using an access token supplied by the authentication/credential layer, then subscribes to `state_changed` events.

## Rationale

The dashboard and entity controls need timely state changes without repeatedly polling the REST API. Keeping WebSocket transport behind a Core interface makes it testable and replaceable.

## Boundaries

- The client owns one WebSocket connection and the protocol handshake.
- The client bounds message size and clones event JSON before disposing the parse document.
- The caller owns reconnection, backoff, connection status, and token refresh.
- Authentication tokens are never written to logs.
- OAuth PKCE and secure token storage are a separate authentication-provider responsibility.
