"""presentation_check on synthetic definitions laid out like Sigma Pack's Uybabaete: a presentation
with no figures that names the common Archers takes over their look, and drawing one jams the map.
Run: mod-tests/.venv/bin/python -m unittest discover mod-tests/tests"""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools"))

from presentation_check import problems  # noqa: E402

ARCHERS = "LandUnit_Era1_Common_Archers"


def presentation(unit, work_in_progress=False):
    return {"unit": unit, "wip": work_in_progress}


def figure(presentation_name, work_in_progress=False, weight=10):
    return {"presentation": presentation_name, "wip": work_in_progress, "weight": weight}


class PresentationCheckTest(unittest.TestCase):
    def test_presentation_without_figures_that_names_a_unit_is_flagged(self):
        presentations = {"Archers_Default": presentation(ARCHERS), "Uybabaete": presentation(ARCHERS)}
        figures = {"Era1_Common_Archers_01": figure("Archers_Default"), "Era1_Uybabaete_01": figure("Archers_Default")}
        found = problems(presentations, figures)
        self.assertEqual(len(found), 1)
        self.assertIn("Uybabaete", found[0])
        self.assertIn(ARCHERS, found[0])

    def test_work_in_progress_presentation_is_ignored(self):
        presentations = {"Archers_Default": presentation(ARCHERS), "Uybabaete": presentation(ARCHERS, work_in_progress=True)}
        figures = {"Era1_Common_Archers_01": figure("Archers_Default")}
        self.assertEqual(problems(presentations, figures), [])

    def test_figures_that_are_work_in_progress_or_weightless_do_not_count(self):
        presentations = {"Archers_Default": presentation(ARCHERS)}
        figures = {"a": figure("Archers_Default", work_in_progress=True), "b": figure("Archers_Default", weight=0)}
        self.assertEqual(len(problems(presentations, figures)), 1)

    def test_presentation_with_figures_passes(self):
        presentations = {"Archers_Default": presentation(ARCHERS)}
        figures = {"Era1_Common_Archers_01": figure("Archers_Default")}
        self.assertEqual(problems(presentations, figures), [])


if __name__ == "__main__":
    unittest.main()
