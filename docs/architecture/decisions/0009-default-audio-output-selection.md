# ADR 0009: Default Windows audio output-device selection

- **Status:** Accepted — defer system-default switching
- **Date:** 2026-11-09
- **Decision owners:** Desktop Companion project

## Context

Desktop Companion already reads and controls the current default playback endpoint through the documented Windows Core Audio APIs. The product roadmap also proposed allowing a user to choose a different system-default playback endpoint from inside the app.

The public MMDevice/Core Audio API documents endpoint enumeration and reading the default endpoint (IMMDeviceEnumerator::GetDefaultAudioEndpoint), but does not provide a supported public method for changing the system default endpoint. Windows itself provides that choice through the Sound settings UI.

A private COM interface commonly called IPolicyConfig is used by third-party examples to change the default endpoint. It is undocumented, is not part of the Windows SDK contract, and relies on private interface identifiers and vtable layouts.

## Decision

Do not implement system-default output-device switching through private or reverse-engineered COM interfaces.

Keep the existing documented Core Audio implementation for reading the default endpoint and changing its master volume/mute state. If a user needs to change the system default output, direct them to Windows Sound settings. Reconsider only if Microsoft publishes a supported API or if a separate, documented app-specific routing capability meets the product need.

## Consequences

- No dependency on undocumented Windows internals or version-sensitive COM layouts.
- The product does not silently change the user's system-wide audio routing.
- Default output-device switching remains deferred in the roadmap.
- Global media-session controls can proceed independently through the documented Windows System Media Transport Controls API.

## References

- [IMMDeviceEnumerator::GetDefaultAudioEndpoint](https://learn.microsoft.com/en-us/windows/desktop/api/mmdeviceapi/nf-mmdeviceapi-immdeviceenumerator-getdefaultaudioendpoint)
- [About MMDevice API](https://learn.microsoft.com/en-us/windows/win32/coreaudio/mmdevice-api)
