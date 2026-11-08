# ADR 0006: Home Assistant native OAuth and credential storage

- Status: Proposed
- Date: 2026-11-08

## Decision

Use Home Assistant's OAuth authorization-code flow with PKCE S256. A temporary IPv4 loopback listener binds to an OS-assigned port and receives the redirect. The corresponding client ID and redirect URI share the same loopback host and port, as required by Home Assistant's IndieAuth-style client validation.

Store only the refresh token in Windows Credential Manager. Keep short-lived access tokens in memory and refresh them shortly before expiry.

## Security properties

- Generate cryptographically random state and PKCE verifier values for every authorization attempt.
- Verify callback state using a constant-time comparison.
- Bind the callback listener only to IPv4 loopback and use an ephemeral port.
- Limit callback headers and close the listener after one request or failure.
- Never log authorization codes, verifiers, access tokens, refresh tokens, or token response bodies.
- Key credentials by a SHA-256 hash of the normalized Home Assistant instance URL.
- Do not store credentials in settings files, SQLite, or application logs.

## Consequences and limitations

- The loopback client ID is valid only for the authorization attempt that created it. Home Assistant refresh-token grants do not require the original client ID, so refresh uses the stable loopback origin.
- The first implementation uses IPv4 loopback only; IPv6 fallback can be added if a supported Windows configuration requires it.
- Sign-out deletes the locally stored refresh token. Server-side token revocation is a separate follow-up.
- A real Home Assistant authorization test is still required before calling the flow production-ready.
