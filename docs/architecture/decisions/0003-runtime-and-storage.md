# ADR 0003: Runtime lifecycle and local storage

- Status: Accepted
- Date: 2026-11-08

## Decision

Use one interactive Windows application process with background hosted services. Use SQLite for non-secret settings and user configuration. Store credentials in Windows Credential Manager or DPAPI-protected storage.

## Rationale

The app needs user-session capabilities such as audio control, window activation, and application launching. A Windows Service would complicate permissions and installation and would not be suitable as the only runtime.

## Consequences

- Sensor and connection workers continue when the main window is hidden.
- Explicit Exit terminates background workers.
- Features requiring an interactive session are unavailable before user login.
- Secrets are stored separately from ordinary application data.
