# ADR 0005: Home Assistant connection supervisor

- Status: Accepted
- Date: 2026-11-08

## Decision

Place connection lifecycle management above the single-connection WebSocket client. The supervisor obtains credentials through an injected async provider, owns retry/backoff, emits connection status, and forwards received Home Assistant events to application services.

## Behaviour

- Start with a Connecting state.
- Mark Connected immediately after authentication and the state-change subscription are accepted.
- Retry transient network, HTTP, I/O, and WebSocket failures with capped exponential backoff and jitter.
- Stop retrying on an explicit authentication rejection and expose AuthenticationRequired.
- Stop promptly on cancellation and publish Disconnected.
- Treat unexpected protocol/programming failures as Faulted rather than retrying indefinitely.
- Isolate event-subscriber exceptions so UI/application observers cannot crash the connection loop.

## Boundaries

- OAuth PKCE, refresh-token exchange, and secure storage are supplied by a separate authentication provider.
- The supervisor does not log tokens or persist credentials.
- The supervisor handles one Home Assistant instance.
