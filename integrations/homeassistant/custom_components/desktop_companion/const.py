"""Constants for the Desktop Companion integration."""

from __future__ import annotations

from homeassistant.const import Platform

DOMAIN = "desktop_companion"
PLATFORMS = [Platform.SENSOR]

SIGNAL_SENSOR_CATALOG = f"{DOMAIN}_sensor_catalog"
SIGNAL_SENSOR_REMOVED = f"{DOMAIN}_sensor_removed"
SIGNAL_SENSOR_UPDATED = f"{DOMAIN}_sensor_updated"

WS_REGISTER_DEVICE = f"{DOMAIN}/register_device"
WS_START_SESSION = f"{DOMAIN}/start_session"
WS_PUBLISH = f"{DOMAIN}/publish"
WS_UNPAIR_DEVICE = f"{DOMAIN}/unpair_device"

MAX_DEVICES = 5
MAX_SENSORS_PER_DEVICE = 100
MAX_PAYLOAD_BYTES = 32 * 1024
MAX_TEXT_STATE_LENGTH = 255
MAX_DEVICE_NAME_LENGTH = 64
MAX_SENSOR_NAME_LENGTH = 64
MAX_SENSOR_ID_LENGTH = 128
MAX_APP_VERSION_LENGTH = 32
MIN_PUBLISH_INTERVAL_SECONDS = 1.0
STALE_AFTER_SECONDS = 15
AVAILABILITY_REFRESH_SECONDS = 5

ALLOWED_SENSOR_CATEGORIES = frozenset(
    {"processor", "memory", "system", "storage", "network"}
)
ALLOWED_SENSOR_UNITS = frozenset({"", "%", "B", "s", "B/s"})
