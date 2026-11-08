"""Home Assistant integration for Desktop Companion devices and Windows sensors."""

from __future__ import annotations

import secrets
import time
from typing import Any

import voluptuous as vol

from homeassistant import config_entries
from homeassistant.components import websocket_api
from homeassistant.core import HomeAssistant
from homeassistant.helpers import device_registry as dr
from homeassistant.helpers.dispatcher import async_dispatcher_send

from .const import (
    DOMAIN,
    MAX_DEVICES,
    MAX_SENSORS_PER_DEVICE,
    MIN_PUBLISH_INTERVAL_SECONDS,
    PLATFORMS,
    SIGNAL_SENSOR_CATALOG,
    SIGNAL_SENSOR_REMOVED,
    SIGNAL_SENSOR_UPDATED,
    WS_PUBLISH,
    WS_REGISTER_DEVICE,
    WS_START_SESSION,
    WS_UNPAIR_DEVICE,
)
from .protocol import (
    ProtocolValidationError,
    is_device_owner,
    is_next_sequence,
    is_publish_interval_elapsed,
    is_valid_session_nonce,
    normalize_device_registration,
    validate_readings,
)
from .runtime import DeviceRuntime, IntegrationRuntime


def _runtime_for(hass: HomeAssistant) -> IntegrationRuntime | None:
    """Return the active integration runtime, if configured."""
    runtimes: dict[str, IntegrationRuntime] = hass.data.get(DOMAIN, {})
    return next(iter(runtimes.values()), None)


async def async_setup(hass: HomeAssistant, config: dict[str, Any]) -> bool:
    """Register authenticated WebSocket commands."""
    hass.data.setdefault(DOMAIN, {})
    websocket_api.async_register_command(hass, websocket_register_device)
    websocket_api.async_register_command(hass, websocket_start_session)
    websocket_api.async_register_command(hass, websocket_publish)
    websocket_api.async_register_command(hass, websocket_unpair_device)
    return True


async def async_setup_entry(hass: HomeAssistant, entry: config_entries.ConfigEntry) -> None:
    """Set up the integration and sensor platform."""
    runtime = IntegrationRuntime.from_entry(entry)
    hass.data.setdefault(DOMAIN, {})[entry.entry_id] = runtime
    await hass.config_entries.async_forward_entry_setups(entry, PLATFORMS)


async def async_unload_entry(hass: HomeAssistant, entry: config_entries.ConfigEntry) -> bool:
    """Unload the integration."""
    if not await hass.config_entries.async_unload_platforms(entry, PLATFORMS):
        return False
    hass.data.get(DOMAIN, {}).pop(entry.entry_id, None)
    return True


@websocket_api.require_admin
@websocket_api.async_response
@websocket_api.websocket_command(
    {
        vol.Required("type"): WS_REGISTER_DEVICE,
        vol.Required("installation_id"): str,
        vol.Required("name"): str,
        vol.Required("app_version"): str,
        vol.Required("sensors"): vol.All(
            [
                vol.Schema(
                    {
                        vol.Required("sensor_id"): str,
                        vol.Required("name"): str,
                        vol.Required("category"): str,
                        vol.Optional("unit", default=""): str,
                    }
                )
            ],
            vol.Length(min=1, max=MAX_SENSORS_PER_DEVICE),
        ),
    }
)
async def websocket_register_device(
    hass: HomeAssistant,
    connection: websocket_api.ActiveConnection,
    msg: dict[str, Any],
) -> None:
    """Register or update a Windows PC; only a Home Assistant administrator may pair."""
    runtime = _runtime_for(hass)
    if runtime is None:
        connection.send_error(
            msg["id"],
            websocket_api.ERR_NOT_FOUND,
            "Add the Desktop Companion integration in Home Assistant first.",
        )
        return

    try:
        registration = normalize_device_registration(
            msg["installation_id"],
            msg["name"],
            msg["app_version"],
            msg["sensors"],
        )
    except ProtocolValidationError as err:
        connection.send_error(msg["id"], websocket_api.ERR_INVALID_FORMAT, str(err))
        return

    device_id = registration["device_id"]
    existing = runtime.devices.get(device_id)
    if existing is None and len(runtime.devices) >= MAX_DEVICES:
        connection.send_error(
            msg["id"],
            websocket_api.ERR_NOT_ALLOWED,
            "This integration already has the maximum number of paired PCs.",
        )
        return
    if existing is not None and existing.owner_user_id != connection.user.id:
        connection.send_error(
            msg["id"],
            websocket_api.ERR_NOT_ALLOWED,
            "This Windows installation is already paired to another Home Assistant user.",
        )
        return

    metadata = {
        "name": registration["name"],
        "app_version": registration["app_version"],
        "sensors": registration["sensors"],
        "owner_user_id": connection.user.id,
    }
    runtime.devices[device_id] = DeviceRuntime(
        metadata=metadata,
        owner_user_id=connection.user.id,
    )
    hass.config_entries.async_update_entry(
        runtime.entry,
        data={
            **runtime.entry.data,
            "devices": {
                key: value.metadata for key, value in runtime.devices.items()
            },
        },
    )

    dr.async_get(hass).async_get_or_create(
        config_entry_id=runtime.entry.entry_id,
        identifiers={(DOMAIN, device_id)},
        manufacturer="Desktop Companion",
        model="Windows PC",
        name=registration["name"],
        sw_version=registration["app_version"],
    )

    async_dispatcher_send(
        hass,
        f"{SIGNAL_SENSOR_CATALOG}_{runtime.entry.entry_id}",
        device_id,
        registration["sensors"],
    )
    connection.send_result(
        msg["id"],
        {
            "device_id": device_id,
            "name": registration["name"],
            "sensor_count": len(registration["sensors"]),
        },
    )


@websocket_api.async_response
@websocket_api.websocket_command(
    {
        vol.Required("type"): WS_START_SESSION,
        vol.Required("device_id"): str,
    }
)
async def websocket_start_session(
    hass: HomeAssistant,
    connection: websocket_api.ActiveConnection,
    msg: dict[str, Any],
) -> None:
    """Create a fresh in-memory nonce for an authenticated paired device."""
    runtime = _runtime_for(hass)
    device = runtime.devices.get(msg["device_id"]) if runtime else None
    if device is None:
        connection.send_error(
            msg["id"], websocket_api.ERR_NOT_FOUND, "The paired Desktop Companion device was not found."
        )
        return
    if device.owner_user_id != connection.user.id:
        connection.send_error(
            msg["id"],
            websocket_api.ERR_UNAUTHORIZED,
            "This Home Assistant user does not own the paired Windows device.",
        )
        return

    nonce = secrets.token_urlsafe(32)
    device.session_nonce = nonce
    device.last_sequence = 0
    device.last_publish_monotonic = 0.0
    connection.send_result(
        msg["id"],
        {
            "session_nonce": nonce,
            "next_sequence": 1,
            "minimum_interval_seconds": MIN_PUBLISH_INTERVAL_SECONDS,
        },
    )


@websocket_api.async_response
@websocket_api.websocket_command(
    {
        vol.Required("type"): WS_PUBLISH,
        vol.Required("device_id"): str,
        vol.Required("session_nonce"): str,
        vol.Required("sequence"): vol.All(int, vol.Range(min=1)),
        vol.Required("readings"): vol.All(
            [
                vol.Schema(
                    {
                        vol.Required("sensor_id"): str,
                        vol.Required("value"): vol.Any(int, float, str),
                    }
                )
            ],
            vol.Length(min=1, max=MAX_SENSORS_PER_DEVICE),
        ),
    }
)
async def websocket_publish(
    hass: HomeAssistant,
    connection: websocket_api.ActiveConnection,
    msg: dict[str, Any],
) -> None:
    """Accept bounded, owner-authenticated, replay-protected sensor updates."""
    runtime = _runtime_for(hass)
    device = runtime.devices.get(msg["device_id"]) if runtime else None
    if device is None:
        connection.send_error(
            msg["id"], websocket_api.ERR_NOT_FOUND, "The paired Desktop Companion device was not found."
        )
        return
    if not is_device_owner(device.owner_user_id, connection.user.id):
        connection.send_error(
            msg["id"],
            websocket_api.ERR_UNAUTHORIZED,
            "This Home Assistant user does not own the paired Windows device.",
        )
        return
    if not is_valid_session_nonce(device.session_nonce, msg["session_nonce"]):
        connection.send_error(
            msg["id"],
            websocket_api.ERR_UNAUTHORIZED,
            "The telemetry session is invalid. Reconnect and start a new session.",
        )
        return
    if not is_next_sequence(device.last_sequence, msg["sequence"]):
        connection.send_error(
            msg["id"],
            websocket_api.ERR_INVALID_FORMAT,
            "The telemetry sequence has already been used.",
        )
        return

    now = time.monotonic()
    if not is_publish_interval_elapsed(
        device.last_publish_monotonic,
        now,
        MIN_PUBLISH_INTERVAL_SECONDS,
    ):
        connection.send_error(
            msg["id"],
            websocket_api.ERR_NOT_ALLOWED,
            "Telemetry updates are limited to one batch per second.",
        )
        return

    registered_ids = {sensor["sensor_id"] for sensor in device.metadata["sensors"]}
    try:
        normalized = validate_readings(msg["readings"], registered_ids)
    except ProtocolValidationError as err:
        connection.send_error(msg["id"], websocket_api.ERR_INVALID_FORMAT, str(err))
        return

    device.readings.update(normalized)
    for sensor_id in normalized:
        device.reading_timestamps[sensor_id] = now
    device.last_seen_monotonic = now
    device.last_sequence = msg["sequence"]
    device.last_publish_monotonic = now

    async_dispatcher_send(
        hass,
        f"{SIGNAL_SENSOR_UPDATED}_{runtime.entry.entry_id}",
        msg["device_id"],
    )
    connection.send_result(
        msg["id"],
        {
            "device_id": msg["device_id"],
            "sequence": device.last_sequence,
            "accepted_count": len(normalized),
            "available": True,
        },
    )


@websocket_api.require_admin
@websocket_api.async_response
@websocket_api.websocket_command(
    {
        vol.Required("type"): WS_UNPAIR_DEVICE,
        vol.Required("device_id"): str,
    }
)
async def websocket_unpair_device(
    hass: HomeAssistant,
    connection: websocket_api.ActiveConnection,
    msg: dict[str, Any],
) -> None:
    """Revoke a paired Windows installation and remove its Home Assistant device."""
    runtime = _runtime_for(hass)
    device = runtime.devices.get(msg["device_id"]) if runtime else None
    if runtime is None or device is None:
        connection.send_error(
            msg["id"], websocket_api.ERR_NOT_FOUND, "The paired Desktop Companion device was not found."
        )
        return

    runtime.devices.pop(msg["device_id"], None)
    hass.config_entries.async_update_entry(
        runtime.entry,
        data={
            **runtime.entry.data,
            "devices": {
                key: value.metadata for key, value in runtime.devices.items()
            },
        },
    )

    async_dispatcher_send(
        hass,
        f"{SIGNAL_SENSOR_REMOVED}_{runtime.entry.entry_id}",
        msg["device_id"],
    )
    connection.send_result(msg["id"], {"device_id": msg["device_id"], "revoked": True})
