"""zk_modbuilder edits: int and reference fields, and a cloned figure that survives a rebuild.
Run: mod-tests/.venv/bin/python -m unittest discover mod-tests/tests"""
import glob
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools"))

import UnityPy  # noqa: E402
from module_edits import parse_clone, parse_edit  # noqa: E402
from zk_modbuilder import patch  # noqa: E402
import hkpaths  # noqa: E402

SIGMA = next(iter(glob.glob(os.path.join(hkpaths.community(), "mod.io", "*", "Sigma Pack.*"))), None)


class ParseTest(unittest.TestCase):
    def test_int_edit(self):
        self.assertEqual(parse_edit("NavalUnit_Era2_Trireme.Level=19"), ("NavalUnit_Era2_Trireme", "Level", 19))

    def test_reference_edit(self):
        self.assertEqual(parse_edit("Era2_Cruithni_01.PresentationUnitDefinition=@PresentationLandUnit_Era2_Cruithni"),
                         ("Era2_Cruithni_01", "PresentationUnitDefinition", {"ref": "PresentationLandUnit_Era2_Cruithni"}))

    def test_nested_json_edit(self):
        self.assertEqual(parse_edit("Effect_X.Effects.0.Path.PropertyToFollow=json:[]"),
                         ("Effect_X", "Effects.0.Path.PropertyToFollow", []))

    def test_clone(self):
        self.assertEqual(parse_clone("Era1_Boita_01=Era1_Alia_01"), ("Era1_Boita_01", "Era1_Alia_01"))


class CloneBuildTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        assert SIGMA, "Sigma Pack is not installed under Documents/Humankind/Community/mod.io"

    def test_clone_points_at_its_new_presentation_and_source_is_dropped(self):
        with tempfile.TemporaryDirectory() as out:
            built = patch(SIGMA, out, "ZK Clone Test",
                          [parse_edit("Era1_Alia_01.PresentationUnitDefinition=@PresentationNavalUnit_Era1_Alia")],
                          clones=[parse_clone("Era1_Boita_01=Era1_Alia_01")])
            bundle = next(glob.glob(os.path.join(built, "*.assetbundle")).__iter__())
            names = {}
            for obj in UnityPy.load(bundle).objects:
                if obj.type.name == "MonoBehaviour":
                    tree = obj.read_typetree()
                    names[tree.get("m_Name")] = tree
        self.assertNotIn("Era1_Boita_01", names)
        self.assertEqual(names["Era1_Alia_01"]["PresentationUnitDefinition"]["serializableElementName"],
                         "PresentationNavalUnit_Era1_Alia")

    def test_nested_edit_reaches_the_built_effect(self):
        district = "Amplitude.Mercury.Simulation.District, Amplitude.Mercury.Firstpass"
        with tempfile.TemporaryDirectory() as out:
            built = patch(SIGMA, out, "ZK Nested Test", [
                parse_edit("Effect_Extension_Era3_Wampanoag.Effects.0.Path.PropertyToFollow=json:[]"),
                parse_edit(f'Effect_Extension_Era3_Wampanoag.startingType=json:"{district}"')])
            bundle = next(glob.glob(os.path.join(built, "*.assetbundle")).__iter__())
            tree = next(obj.read_typetree() for obj in UnityPy.load(bundle).objects if obj.type.name == "MonoBehaviour"
                        and obj.read_typetree().get("m_Name") == "Effect_Extension_Era3_Wampanoag")
        self.assertEqual(tree["Effects"][0]["Path"]["PropertyToFollow"], [])
        self.assertEqual(tree["startingType"], district)


if __name__ == "__main__":
    unittest.main()
