"""descriptor_check compiles effects the way the game does (Amplitude.Framework Compiler.GeneratePath and
CompileDescriptors), on contracts shaped like the game's: Sigma Pack's Wetu carried an empire effect on a
district and broke turn 54 (soak d); a property missing on the effect's target makes the game drop every mod.
Run: mod-tests/.venv/bin/python -m pytest mod-tests/tests"""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools"))

from descriptor_check import problems  # noqa: E402

CONTRACTS = {
    "District": {"properties": ["foodproduced"], "references": {"settlement": "Settlement", "neighbours": "District"}},
    "Settlement": {"properties": ["experiencegainonunitcreation", "foodproduced"], "references": {"empire": "Empire", "districts": "District"}},
    "Empire": {"properties": ["moneynet"], "references": {"settlements": "Settlement"}},
    "MajorEmpire": {"properties": ["moneynet"], "references": {"settlements": "Settlement"}},
    "Unit": {"properties": ["combatstrength"], "references": {"unitcollection": "UnitCollection"}},
}


def effect(path, prop, on_source=False, specific="", reads=()):
    return {"path": path, "properties": [prop], "on_source": on_source, "specific": specific, "reads": list(reads)}


class DescriptorCheckTest(unittest.TestCase):
    def test_district_carrying_an_empire_path_breaks_at_runtime(self):
        owners = {"Extension_Era3_Wampanoag": ["Effect_Wampanoag"]}
        effects = {"Effect_Wampanoag": ("MajorEmpire", [effect(["Settlements", "Districts"], "FoodProduced")])}
        found = problems(owners, effects, CONTRACTS)
        self.assertEqual(len(found), 1)
        self.assertIn("(District) carries Effect_Wampanoag, declared for MajorEmpire", found[0])

    def test_property_missing_on_the_target_fails_the_load(self):
        effects = {"Effect_Chickasaw": ("District", [effect(["Settlement"], "ExperienceGainOnUnitCreation", on_source=True)])}
        found = problems({}, effects, CONTRACTS)
        self.assertEqual(len(found), 1)
        self.assertIn("experiencegainonunitcreation", found[0])

    def test_path_missing_from_the_declared_starting_type_fails_the_load(self):
        effects = {"Effect_X": ("District", [effect(["Cities"], "FoodProduced")])}
        self.assertEqual(len(problems({}, effects, CONTRACTS)), 1)

    def test_valid_effects_pass(self):
        owners = {"Extension_Base_Food": ["Effect_Food", "Effect_Settlement_Xp"], "LandUnit_X": ["Effect_Unit"]}
        effects = {
            "Effect_Food": ("District", [effect([], "FoodProduced")]),
            "Effect_Settlement_Xp": ("District", [effect(["Settlement"], "ExperienceGainOnUnitCreation")]),
            "Effect_Unit": ("Unit", [effect([], "CombatStrength")]),
        }
        self.assertEqual(problems(owners, effects, CONTRACTS), [])

    def test_specific_target_type_decides_the_target_properties(self):
        contracts = {**CONTRACTS, "Empire": {"properties": [], "references": {}}}
        effects = {"Effect_Wonder": ("Settlement", [effect(["Empire"], "MoneyNet", specific="MajorEmpire")])}
        self.assertEqual(problems({}, effects, contracts), [])

    def test_formula_reads_are_checked_on_target_and_source(self):
        # RPN op 8 reads the target, op 9 the source (Amplitude.Framework.Simulation.Operation).
        reads_target = effect(["Settlement"], "ExperienceGainOnUnitCreation", reads=[("target", "MoneyNet")])
        reads_source = effect(["Settlement"], "ExperienceGainOnUnitCreation", reads=[("source", "FoodProduced")])
        found = problems({}, {"Effect_Bad": ("District", [reads_target]), "Effect_Good": ("District", [reads_source])}, CONTRACTS)
        self.assertEqual(len(found), 1)
        self.assertIn("moneynet", found[0])

    def test_carrier_of_another_type_is_flagged_even_when_names_resolve(self):
        # The compiled reference and property indexes belong to the starting type, not the carrier.
        owners = {"Extension_Era1_HarappanCivilization_02": ["Effect_Repeatable_Culture"], "Extension_Era2_Chickasaw": ["Effect_Unit_Emblematic"]}
        # Path and property resolve on a District too, so only the declared type gives the problem away.
        effects = {"Effect_Repeatable_Culture": ("Settlement", [effect([], "FoodProduced")]),
                   "Effect_Unit_Emblematic": ("Unit", [{"path": [], "properties": [], "on_source": False}])}
        found = problems(owners, effects, CONTRACTS)
        self.assertEqual(len(found), 1)
        self.assertIn("Effect_Repeatable_Culture", found[0])

    def test_unknown_starting_types_and_owners_are_not_judged(self):
        owners = {"Civilization_Era1_Colchis": ["Effect_Empire"]}
        effects = {"Effect_Empire": ("MajorEmpire", [effect(["Settlements"], "MoneyNet", on_source=True)]),
                   "Effect_Odd": ("Faction", [effect([], "X")])}
        self.assertEqual(problems(owners, effects, CONTRACTS), [])


if __name__ == "__main__":
    unittest.main()
