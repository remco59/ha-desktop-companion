"""Pure validation helpers for the Desktop Companion telemetry protocol."""

from __future__ import annotations

import json
import math
import re
import uuid
from typing import Any

from .const import (
    ALLOWED_SENSOR_CATEGORIES,
    ALLOWED_SENSOR_UNITS,
    MAX_APP_VERSION_LENGTH,
    MAX_DEVICE_NAME_LENGTH,
    MAX_PAYLOAD_BYTES,
    MAX_SENSOR_ID_LENGTH,
    MAX_SENSOR_NAME_LENGTH,
    MAX_SENSORS_PER_DEVICE,
    MAX_TEXT_STATE_LENGTH,
)

_SENSOR_ID_PATTERN = re.compile(r"^[a-z0-9_]{1,128}$")
MAX_ABSOLUTE_NUMERIC_VALUE = 1e18


class ProtocolValidationError(ValueError):
    """Raised when a telemetry message violates the protocol."""


def normalize_device_registration(
    installation_id: str,
    name: str,
    app_version: str,
    sensors: list[dict[str, Any]],
) -> dict[str, Any]:
    """Validate and normalize the device registration payload."""
    try:
        device_id = str(uuid.UUID(installation_id))
    except (ValueError, TypeError, AttributeError) as err:
        raise ProtocolValidationError("The installation ID must be a valid UUID.") from err

    normalized_name = _normalize_label(name, MAX_DEVICE_NAME_LENGTH, "device name")
    if not isinstance(app_version, str) or not app_version or len(app_version) > MAX_APP_VERSION_LENGTH:
        raise ProtocolValidationError("The app version is invalid.")

    if not isinstance(sensors, list) or not 1 <= len(sensors) <= MAX_SENSORS_PER_DEVICE:
        raise ProtocolValidationError(
            f"Register between 1 and {MAX_SENSORS_PER_DEVICE} sensors."
        )

    normalized_sensors: list[dict[str, str]] = []
    seen_ids: set[str] = set()
    for sensor in sensors:
        if not isinstance(sensor, dict):
            raise ProtocolValidationError("Each sensor descriptor must be an object.")

        sensor_id = sensor.get("sensor_id")
        if (
            not isinstance(sensor_id, str)
            or len(sensor_id) > MAX_SENSOR_ID_LENGTH
            or not _SENSOR_ID_PATTERN.fullmatch(sensor_id)
        ):
            raise ProtocolValidationError("A sensor ID contains invalid characters.")

        if sensor_id in seen_ids:
            raise ProtocolValidationError("Sensor IDs must be unique.")
        seen_ids.add(sensor_id)

        sensor_name = _normalize_label(
            sensor.get("name"), MAX_SENSOR_NAME_LENGTH, "sensor name"
        )
        category = sensor.get("category")
        unit = sensor.get("unit", "")
        if not isinstance(category, str) or category not in ALLOWED_SENSOR_CATEGORIES:
            raise ProtocolValidationError("A sensor category is not supported.")
        if not isinstance(unit, str) or unit not in ALLOWED_SENSOR_UNITS:
            raise ProtocolValidationError("A sensor unit is not supported.")

        normalized_sensors.append(
            {
                "sensor_id": sensor_id,
                "name": sensor_name,
                "category": category,
                "unit": unit,
            }
        )

    return {
        "device_id": device_id,
        "name": normalized_name,
        "app_version": app_version,
        "sensors": normalized_sensors,
    }


def validate_readings(
    readings: list[dict[str, Any]],
    registered_sensor_ids: set[str],
) -> dict[str, int | float | str]:
    """Validate a bounded reading batch and return its normalized state map."""
    if not isinstance(readings, list) or not 1 <= len(readings) <= MAX_SENSORS_PER_DEVICE:
        raise ProtocolValidationError("The telemetry batch has an invalid reading count.")

    try:
        payload_size = len(
            json.dumps(readings, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        )
    except (TypeError, ValueError, UnicodeEncodeError) as err:
        raise ProtocolValidationError("The telemetry batch is not valid JSON data.") from err

    if payload_size > MAX_PAYLOAD_BYTES:
        raise ProtocolValidationError("The telemetry batch exceeds the 32 KiB limit.")

    normalized: dict[str, int | float | str] = {}
    for item in readings:
        if not isinstance(item, dict):
            raise ProtocolValidationError("Each reading must be an object.")

        sensor_id = item.get("sensor_id")
        if not isinstance(sensor_id, str) or sensor_id not in registered_sensor_ids:
            raise ProtocolValidationError("The telemetry batch contains an unregistered sensor ID.")

        if sensor_id in normalized:
            raise ProtocolValidationError("A sensor can only appear once in a telemetry batch.")

        value = item.get("value")
        if isinstance(value, bool):
            raise ProtocolValidationError("Boolean values are not valid sensor states.")
        if isinstance(value, (int, float)):
            try:
                numeric_value = float(value)
            except (OverflowError, ValueError) as err:
                raise ProtocolValidationError("Numeric sensor values are outside the supported range.") from err
            if not math.isfinite(numeric_value) or abs(numeric_value) > MAX_ABSOLUTE_NUMERIC_VALUE:
                raise ProtocolValidationError("Numeric sensor values must be finite and within range.")
        elif isinstance(value, str):
            if len(value) > MAX_TEXT_STATE_LENGTH or any(ord(char) < 32 for char in value):
                raise ProtocolValidationError("Text sensor values must be short printable text.")
        else:
            raise ProtocolValidationError("A sensor value must be numeric or text.")

        normalized[sensor_id] = value

    return normalized


def _normalize_label(value: Any, maximum_length: int, label: str) -> str:
    if not isinstance(value, str):
        raise ProtocolValidationError(f"The {label} must be text.")

    normalized = " ".join(value.split())
    if not normalized or len(normalized) > maximum_length:
        raise ProtocolValidationError(f"The {label} must contain 1–{maximum_length} characters.")
    if any(ord(char) < 32 for char in normalized):
        raise ProtocolValidationError(f"The {label} contains control characters.")
    return normalized
