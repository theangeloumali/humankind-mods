"""odin_blob on a synthetic event laid out like Eventful's Neolithic05a (one Empire_Armies variable,
a SpawnArmy and an ApplyStatus both targeting it). Run: python -m unittest discover mod-tests/tests"""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools"))

from odin_blob import (VARIABLE_TYPE, add_units_variable, apply_status_effects, declared_variables,  # noqa: E402
                       int32, odin_string, retarget_status)


def named(name, value):
    return b"\x27" + odin_string(name) + odin_string(value)


def armies_variable():
    empty_prerequisites = b"\x01" + odin_string("ElementPrerequisites") + b"\x30" + int32(3) + int32(6) \
        + b"\x06" + (0).to_bytes(8, "little") + b"\x07\x05"
    return (b"\x02\x2f" + int32(5) + odin_string(VARIABLE_TYPE.format("Empire_Armies")) + int32(5)
            + named("Name", "Armies") + empty_prerequisites + named("EmpireID", "Empire") + b"\x05")


def event():
    variables = b"\x06" + (1).to_bytes(8, "little") + armies_variable() + b"\x07"
    spawn = named("TargetID", "Armies") + named("OwnerID", "Empire")
    status = named("TargetID", "Armies") + b"\x03" + odin_string("StatusDefinition") \
        + named("serializableElementName", "Status_Unit_Motivated01") + b"\x05"
    return variables + spawn + status


class OdinBlobTest(unittest.TestCase):
    def test_finds_only_status_effects(self):
        effects = [(target, status) for _, target, status in apply_status_effects(event())]
        self.assertEqual(effects, [("Armies", "Status_Unit_Motivated01")])

    def test_retarget_leaves_other_effects_alone(self):
        blob, count = retarget_status(event(), "Armies", "ArmyUnits")
        self.assertEqual(count, 1)
        self.assertEqual([t for _, t, _ in apply_status_effects(blob)], ["ArmyUnits"])
        self.assertIn(named("TargetID", "Armies") + named("OwnerID", "Empire"), blob)

    def test_adds_a_units_variable_as_a_second_array_element(self):
        blob = add_units_variable(event(), "Armies", "ArmyUnits")
        self.assertEqual(declared_variables(blob), {"Armies": "Armies", "ArmyUnits": "Units"})
        self.assertIn(b"\x06" + (2).to_bytes(8, "little") + b"\x02\x2f" + int32(5), blob)
        # The added node closes before the array does, and its nested node gets a fresh id.
        self.assertIn(named("EmpireID", "Empire") + b"\x05\x07", blob)
        self.assertEqual(blob.count(int32(6) + b"\x06"), 1)

    def test_refuses_a_missing_variable(self):
        with self.assertRaises(ValueError):
            add_units_variable(event(), "Cities", "CityUnits")


if __name__ == "__main__":
    unittest.main()
