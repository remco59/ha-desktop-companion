"""Sensor entities for paired Desktop Companion Windows devices."""

from __future__ import annotations

import time
from datetime import timedelta
from typing import Any

from homeassistant.components.sensor import (
    SensorDeviceClass,
    SensorEntity,
    SensorEntityDescription,
    SensorStateClass,
)
from homeassistant.config_entries import ConfigEntry
from homeassistant.const import UnitOfDataRate, UnitOfInformation, UnitOfTime
from homeassistant.core import HomeAssistant, callback
from homeassistant.helpers import device_registry as dr
from homeassistant.helpers import entity_registry as er
from homeassistant.helpers.device_registry import DeviceInfo
from homeassistant.helpers.dispatcher import async_dispatcher_connect
from homeassistant.helpers.entity_platform import AddEntitiesCallback
from homeassistant.helpers.event import async_track_time_interval

from .const import (
    AVAILABILITY_REFRESH_SECONDS,
    DOMAIN,
    SIGNAL_SENSOR_CATALOG,
    SIGNAL_SENSOR_REMOVED,
    SIGNAL_SENSOR_UPDATED,
    STALE_AFTER_SECONDS,
)
from .runtime import IntegrationRuntime


def _description(sensor: dict[str, str]) -> SensorEntityDescription:
    """Build a Home Assistant entity description from validated metadata."""
    unit = sensor["unit"]
    device_class = None
    state_class = None
    native_unit = unit or None

    if unit == "%":
        state_class = SensorStateClass.MEASUREMENT
    elif unit == "B":
        device_class = SensorDeviceClass.DATA_SIZE
        state_class = SensorStateClass.MEASUREMENT
        native_unit = UnitOfInformation.BYTES
    elif unit == "B/s":
        device_class = SensorDeviceClass.DATA_RATE
        state_class = SensorStateClass.MEASUREMENT
        native_unit = UnitOfDataRate.BYTES_PER_SECOND
    elif unit == "s" and sensor["sensor_id"] == "system_uptime_seconds":
        device_class = SensorDeviceClass.DURATION
        state_class = SensorStateClass.MEASUREMENT
        native_unit = UnitOfTime.SECONDS

    return SensorEntityDescription(
        key=sensor["sensor_id"],
        name=sensor["name"],
        native_unit_of_measurement=native_unit,
        device_class=device_class,
        state_class=state_class,
    )


class DesktopCompanionSensor(SensorEntity):
    """One Home Assistant sensor backed by the latest paired Windows reading."""

    _attr_has_entity_name = True

    def __init__(
        self,
        runtime: IntegrationRuntime,
        device_id: str,
        sensor: dict[str, str],
    ) -> None:
        self._runtime = runtime
        self._device_id = device_id
        self._sensor_id = sensor["sensor_id"]
        self.entity_description = _description(sensor)
        self._attr_unique_id = f"{device_id}_{self._sensor_id}"

    @property
    def device_info(self) -> DeviceInfo:
        """Return the owning Windows PC device."""
        device = self._runtime.devices.get(self._device_id)
        if device is None:
            return DeviceInfo(identifiers={(DOMAIN, self._device_id)})
        metadata = device.metadata
        return DeviceInfo(
            identifiers={(DOMAIN, self._device_id)},
            name=metadata["name"],
            manufacturer="Desktop Companion",
            model="Windows PC",
            sw_version=metadata["app_version"],
        )

    @property
    def native_value(self) -> int | float | str | None:
        """Return the latest numeric or text state."""
        device = self._runtime.devices.get(self._device_id)
        if device is None:
            return None
        return device.readings.get(self._sensor_id)

    @property
    def available(self) -> bool:
        """Mark this sensor unavailable after 15 seconds without a fresh reading."""
        device = self._runtime.devices.get(self._device_id)
        if device is None:
            return False
        last_updated = device.reading_timestamps.get(self._sensor_id)
        return last_updated is not None and time.monotonic() - last_updated <= STALE_AFTER_SECONDS


async def async_setup_entry(
    hass: HomeAssistant,
    entry: ConfigEntry,
    async_add_entities: AddEntitiesCallback,
) -> None:
    """Set up existing paired entities and listen for new registrations."""
    runtime: IntegrationRuntime = hass.data[DOMAIN][entry.entry_id]
    entities: dict[str, list[DesktopCompanionSensor]] = {}

    async def async_remove_sensor_entities(removed: list[DesktopCompanionSensor]) -> None:
        entity_registry = er.async_get(hass)
        for entity in removed:
            await entity.async_remove(force_remove=True)
            if entity.entity_id and entity_registry.async_get(entity.entity_id):
                entity_registry.async_remove(entity.entity_id)

    async def async_remove_device_entities(device_id: str, removed: list[DesktopCompanionSensor]) -> None:
        await async_remove_sensor_entities(removed)
        device = dr.async_get(hass).async_get_device(
            identifiers={(DOMAIN, device_id)},
            connections=set(),
        )
        if device is not None:
            dr.async_get(hass).async_remove_device(device.id)

    @callback
    def add_device(device_id: str, sensors: list[dict[str, Any]]) -> None:
        """Add new sensor entities and remove entities no longer selected."""
        device = runtime.devices.get(device_id)
        if device is None:
            return

        desired_ids = {sensor["sensor_id"] for sensor in sensors}
        current = entities.setdefault(device_id, [])
        removed = [entity for entity in current if entity._sensor_id not in desired_ids]
        retained = [entity for entity in current if entity._sensor_id in desired_ids]
        entities[device_id] = retained
        if removed:
            hass.async_create_task(async_remove_sensor_entities(removed))
        for entity in retained:
            entity.async_write_ha_state()

        known = {entity._sensor_id for entity in retained}
        new_entities = [
            DesktopCompanionSensor(runtime, device_id, sensor)
            for sensor in sensors
            if sensor["sensor_id"] not in known
        ]
        entities[device_id].extend(new_entities)
        if new_entities:
            async_add_entities(new_entities)

    @callback
    def update_device(device_id: str) -> None:
        """Write updated states for one device."""
        for entity in entities.get(device_id, []):
            entity.async_write_ha_state()

    @callback
    def remove_device(device_id: str) -> None:
        """Remove all entity registry entries for a revoked PC."""
        removed = entities.pop(device_id, [])
        hass.async_create_task(async_remove_device_entities(device_id, removed))

    entry.async_on_unload(
        async_dispatcher_connect(
            hass,
            f"{SIGNAL_SENSOR_CATALOG}_{entry.entry_id}",
            add_device,
        )
    )
    entry.async_on_unload(
        async_dispatcher_connect(
            hass,
            f"{SIGNAL_SENSOR_UPDATED}_{entry.entry_id}",
            update_device,
        )
    )
    entry.async_on_unload(
        async_dispatcher_connect(
            hass,
            f"{SIGNAL_SENSOR_REMOVED}_{entry.entry_id}",
            remove_device,
        )
    )

    for device_id, device in runtime.devices.items():
        add_device(device_id, device.metadata["sensors"])

    @callback
    def refresh_availability(_now) -> None:
        for device_entities in entities.values():
            for entity in device_entities:
                entity.async_write_ha_state()

    entry.async_on_unload(
        async_track_time_interval(
            hass,
            refresh_availability,
            timedelta(seconds=AVAILABILITY_REFRESH_SECONDS),
        )
    )
