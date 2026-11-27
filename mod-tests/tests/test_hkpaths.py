"""hkpaths: Humankind's Windows folders, from overrides or %USERPROFILE%, with no user-specific path in the code."""
import os
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools"))

import hkpaths  # noqa: E402


class HkPathsTest(unittest.TestCase):
    def setUp(self):
        hkpaths.windows_home.cache_clear()

    def test_overrides_win(self):
        with mock.patch.dict(os.environ, {"HK_DOCS": "/x/docs", "HK_LOCALLOW": "/x/low"}):
            self.assertEqual(hkpaths.docs(), "/x/docs")
            self.assertEqual(hkpaths.locallow(), "/x/low")

    def test_derives_from_the_windows_home(self):
        with mock.patch.dict(os.environ, {"WIN_HOME": "/mnt/c/Users/someone"}, clear=False):
            os.environ.pop("HK_DOCS", None)
            os.environ.pop("HK_LOCALLOW", None)
            self.assertEqual(hkpaths.docs(), "/mnt/c/Users/someone/Documents/Humankind")
            self.assertEqual(hkpaths.locallow(), "/mnt/c/Users/someone/AppData/LocalLow/Amplitude Studios/Humankind")
            self.assertEqual(hkpaths.logs(), "/mnt/c/Users/someone/Documents/Humankind/Temporary Files")

    def test_stops_with_a_clear_error_when_the_windows_home_is_unknown(self):
        with mock.patch.dict(os.environ, {}, clear=False), mock.patch.object(hkpaths.subprocess, "run") as run:
            for name in ("WIN_HOME", "HK_DOCS", "HK_LOCALLOW"):
                os.environ.pop(name, None)
            run.return_value = mock.Mock(stdout="")
            with self.assertRaises(RuntimeError):
                hkpaths.docs()

    def test_finds_the_registry_of_whichever_profile_exists(self):
        with tempfile.TemporaryDirectory() as docs:
            os.makedirs(os.path.join(docs, "Users", "0xABC"))
            registry = os.path.join(docs, "Users", "0xABC", "Registry.xml")
            open(registry, "w").close()
            with mock.patch.dict(os.environ, {"HK_DOCS": docs}):
                self.assertEqual(hkpaths.registry(), registry)


if __name__ == "__main__":
    unittest.main()
