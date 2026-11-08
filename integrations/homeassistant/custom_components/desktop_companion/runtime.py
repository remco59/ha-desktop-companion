"""Runtime storage for paired Desktop Companion devices."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass
class DeviceRuntime:
    """In-memory runtime state for one paired Windows installation."""

    metadata: dict[str, Any]
    owner_user_id: str
    readings: dict[str, int | float | str] = field(default_factory=dict)
    reading_timestamps: dict[str, float] = field(default_factory=dict)
    last_seen_monotonic: float | None = None
    session_nonce: str | None = None
    last_sequence: int = 0
    last_publish_monotonic: float = 0.0


@dataclass
class IntegrationRuntime:
    """Runtime state scoped to the integration config entry."""

    entry: Any
    devices: dict[str, DeviceRuntime] = field(default_factory=dict)

    @classmethod
    def from_entry(cls, entry: Any) -> IntegrationRuntime:
        """Restore non-secret device metadata from the config entry."""
        devices: dict[str, DeviceRuntime] = {}
        for device_id, metadata in entry.data.get("devices", {}).items():
            owner_user_id = metadata.get("owner_user_id")
            if not owner_user_id:
                continue
            devices[device_id] = DeviceRuntime(
                metadata=dict(metadata),
                owner_user_id=owner_user_id,
            )
        return cls(entry=entry, devices=devices)
