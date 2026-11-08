"""Unit tests for the pure Desktop Companion telemetry protocol helpers."""

from __future__ import annotations

import math
import unittest

from custom_components.desktop_companion.protocol import (
    ProtocolValidationError,
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
