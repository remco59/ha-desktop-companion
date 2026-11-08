"""Unit tests for the pure Desktop Companion telemetry protocol helpers."""

from __future__ import annotations

import math
import sys
import types
import unittest
from pathlib import Path

# Import only the pure protocol module. The integration package __init__ imports
# Home Assistant runtime dependencies, which are deliberately absent in this
# lightweight unit-test job.
integration_root = Path(__file__).resolve().parents[1] / "custom_components"
integration_package = integration_root / "desktop_companion"
sys.modules.setdefault(
    "custom_components",
    types.ModuleType("custom_components"),
)
if "custom_components" not in sys.modules:
    raise RuntimeError("Could not initialise the custom_components namespace.")
sys.modules["custom_components"].__path__ = [str(integration_root)]
package = types.ModuleType("custom_components.desktop_companion")
package.__path__ = [str(integration_package)]
package.__package__ = "custom_components.desktop_companion"
sys.modules.setdefault("custom_components.desktop_companion", package)

from custom_components.desktop_companion.protocol import (
    ProtocolValidationError,
    is_device_owner,
    is_next_sequence,
    is_publish_interval_elapsed,
    is_valid_session_nonce,
    normalize_device_registration,
    validate_readings,
)


class RegistrationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.installation_id = "d0b9f7b6d7e8462bbbc63a2fca7fd121"
        self.sensors = [
            {"sensor_id": "cpu_usage_percent", "name": "CPU usage", "category": "processor", "unit": "%"},
            {"sensor_id": "memory_used_bytes", "name": "Memory used", "category": "memory", "unit": "B"},
        ]

    def test_normalizes_installation_id_and_device_name(self) -> None:
        result = normalize_device_registration(
            self.installation_id, "  Remco   PC  ", "1.0.0", self.sensors
        )
        self.assertEqual(result["device_id"], "d0b9f7b6-d7e8-462b-bbc6-3a2fca7fd121")
        self.assertEqual(result["name"], "Remco PC")

    def test_rejects_invalid_installation_id(self) -> None:
        with self.assertRaises(ProtocolValidationError):
            normalize_device_registration("not-a-guid", "PC", "1.0", self.sensors)

    def test_rejects_duplicate_sensor_ids(self) -> None:
        with self.assertRaises(ProtocolValidationError):
            normalize_device_registration(
                self.installation_id, "PC", "1.0", self.sensors + [self.sensors[0]]
            )

    def test_rejects_unsupported_units_and_categories(self) -> None:
        with self.assertRaises(ProtocolValidationError):
            normalize_device_registration(
                self.installation_id, "PC", "1.0", [{**self.sensors[0], "unit": "arbitrary"}]
            )
        with self.assertRaises(ProtocolValidationError):
            normalize_device_registration(
                self.installation_id, "PC", "1.0", [{**self.sensors[0], "category": "command"}]
            )

    def test_rejects_sensor_id_outside_pattern(self) -> None:
        invalid = [{**self.sensors[0], "sensor_id": "sensor;call_service"}]
        with self.assertRaises(ProtocolValidationError):
            normalize_device_registration(self.installation_id, "PC", "1.0", invalid)

    def test_rejects_too_many_sensor_descriptors(self) -> None:
        many = [
            {"sensor_id": f"sensor_{index}", "name": f"Sensor {index}", "category": "system", "unit": ""}
            for index in range(101)
        ]
        with self.assertRaises(ProtocolValidationError):
            normalize_device_registration(self.installation_id, "PC", "1.0", many)


class SessionSecurityTests(unittest.TestCase):
    def test_device_owner_is_bound_to_the_authorised_user(self) -> None:
        self.assertTrue(is_device_owner("ha-user-1", "ha-user-1"))
        self.assertFalse(is_device_owner("ha-user-1", "ha-user-2"))
        self.assertFalse(is_device_owner("", "ha-user-1"))

    def test_session_nonce_requires_exact_ascii_match(self) -> None:
        self.assertTrue(is_valid_session_nonce("nonce-123", "nonce-123"))
        self.assertFalse(is_valid_session_nonce("nonce-123", "nonce-124"))
        self.assertFalse(is_valid_session_nonce("nonce-123", "nøne-123"))
        self.assertFalse(is_valid_session_nonce("nonce-123", "x" * 257))
        self.assertFalse(is_valid_session_nonce(None, "nonce-123"))

    def test_sequence_must_be_strictly_increasing_integer_not_boolean(self) -> None:
        self.assertTrue(is_next_sequence(3, 4))
        self.assertFalse(is_next_sequence(3, 3))
        self.assertFalse(is_next_sequence(3, 2))
        self.assertFalse(is_next_sequence(3, True))
        self.assertFalse(is_next_sequence(3, 4.0))

    def test_minimum_publish_interval_and_monotonic_clock(self) -> None:
        self.assertTrue(is_publish_interval_elapsed(0.0, 10.0, 1.0))
        self.assertTrue(is_publish_interval_elapsed(10.0, 11.0, 1.0))
        self.assertFalse(is_publish_interval_elapsed(10.0, 10.5, 1.0))
        self.assertFalse(is_publish_interval_elapsed(10.0, 9.0, 1.0))
        self.assertFalse(is_publish_interval_elapsed(10.0, math.inf, 1.0))


class ReadingTests(unittest.TestCase):
    def setUp(self) -> None:
        self.registered = {"cpu_usage_percent", "memory_used_bytes"}

    def test_accepts_numeric_states(self) -> None:
        result = validate_readings(
            [
                {"sensor_id": "cpu_usage_percent", "value": 21.5},
                {"sensor_id": "memory_used_bytes", "value": 123456},
            ],
            self.registered,
        )
        self.assertEqual(result["cpu_usage_percent"], 21.5)
        self.assertEqual(result["memory_used_bytes"], 123456)

    def test_accepts_text_states(self) -> None:
        result = validate_readings(
            [{"sensor_id": "cpu_usage_percent", "value": "Normal"}],
            self.registered,
        )
        self.assertEqual(result["cpu_usage_percent"], "Normal")

    def test_rejects_unknown_sensor(self) -> None:
        with self.assertRaises(ProtocolValidationError):
            validate_readings([{"sensor_id": "unknown", "value": 1}], self.registered)

    def test_rejects_boolean_and_non_finite_values(self) -> None:
        for value in (True, math.nan, math.inf):
            with self.subTest(value=value), self.assertRaises(ProtocolValidationError):
                validate_readings([{"sensor_id": "cpu_usage_percent", "value": value}], self.registered)

    def test_rejects_duplicate_and_oversized_batches(self) -> None:
        duplicate = [
            {"sensor_id": "cpu_usage_percent", "value": 1},
            {"sensor_id": "cpu_usage_percent", "value": 2},
        ]
        oversized = [{"sensor_id": "cpu_usage_percent", "value": "x" * 40000}]
        with self.assertRaises(ProtocolValidationError):
            validate_readings(duplicate, self.registered)
        with self.assertRaises(ProtocolValidationError):
            validate_readings(oversized, self.registered)

    def test_rejects_out_of_range_numeric_values(self) -> None:
        with self.assertRaises(ProtocolValidationError):
            validate_readings([{"sensor_id": "cpu_usage_percent", "value": 1e100}], self.registered)


if __name__ == "__main__":
    unittest.main()
