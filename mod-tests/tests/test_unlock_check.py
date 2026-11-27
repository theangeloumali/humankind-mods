"""unlock_check: a common unit on a family ladder must be unlocked by a technology, because only a technology
sets its EraOfUnlock and the minor factions' army upgrade (BaseHumanMinorFactionUtils.GetUpgradedUnitDefinition)
reads it unchecked when it steps onto that rung at an era change (soak e, turn 88).
Run: mod-tests/.venv/bin/python -m pytest mod-tests/tests"""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools"))

from unlock_check import problems  # noqa: E402

NO_NEXT = {}


class UnlockCheckTest(unittest.TestCase):
    def test_common_unit_no_technology_unlocks_is_flagged(self):
        techs = {"Technology_Era2_01": [["LandUnit_Era2_Common_Swordsmen"]]}
        families = {"LandUnit_Era2_Common_Swordsmen": ["UnitFamily_Infantry", 3, False],
                    "LandUnit_Era3_Common_ManAtArms": ["UnitFamily_Infantry", 5, False]}
        found = problems(techs, families, NO_NEXT)
        self.assertEqual(len(found), 1)
        self.assertIn("LandUnit_Era3_Common_ManAtArms", found[0])
        self.assertIn("UnitFamily_Infantry#5", found[0])

    def test_emblematic_units_and_the_ladder_bottom_are_not_judged(self):
        families = {"LandUnit_Era2_Rome_Legion": ["UnitFamily_Infantry", 3, True],
                    "LandUnit_Era0_Common_Tribesmen": ["UnitFamily_Infantry", 0, False]}
        self.assertEqual(problems({}, families, NO_NEXT), [])

    def test_bottom_rung_counts_once_another_family_leads_into_it(self):
        families = {"LandUnit_Era1_Common_Scouts": ["UnitFamily_Scout", 6, False],
                    "LandUnit_Era2_Common_Horsemen": ["UnitFamily_LightCavalry", 12, False]}
        techs = {"Technology_Era2_03": [["LandUnit_Era2_Common_Horsemen"]]}
        self.assertEqual(problems(techs, families, {"UnitFamily_Militia": "UnitFamily_LightCavalry"}), [])
        found = problems({}, families, {"UnitFamily_Militia": "UnitFamily_Scout"})
        self.assertEqual(len(found), 1)
        self.assertIn("LandUnit_Era1_Common_Scouts", found[0])

    def test_districts_and_unitless_entries_are_not_judged(self):
        families = {"Extension_Base_Food": ["DistrictFamily_Food", 2, False], "LandUnit_Odd": ["", None, False]}
        self.assertEqual(problems({}, families, NO_NEXT), [])


if __name__ == "__main__":
    unittest.main()
