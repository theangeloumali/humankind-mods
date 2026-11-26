"""The bundle-tool venv caps each Python process's memory, so a runaway UnityPy scan fails with
MemoryError instead of exhausting WSL (2026-11-25: an 8.5 GB scan got the whole distro killed).
Run: mod-tests/.venv/bin/python -m unittest discover mod-tests/tests"""
import os
import subprocess
import unittest

VENV_PYTHON = os.path.join(os.path.dirname(__file__), "..", ".venv", "bin", "python")
GIB = 1024 ** 3


def allocate(gigabytes, cap_gigabytes):
    env = {**os.environ, "ZK_PY_MEMORY_GB": str(cap_gigabytes)}
    script = f"buffer = bytearray({int(gigabytes * GIB)}); print('allocated')"
    return subprocess.run([VENV_PYTHON, "-c", script], env=env, capture_output=True, text=True, timeout=120)


class MemoryCapTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        assert os.path.exists(VENV_PYTHON), "no venv: run mod-tests/tools/setup.sh first"

    def test_allocation_past_the_cap_raises_memory_error(self):
        result = allocate(gigabytes=2, cap_gigabytes=1)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("MemoryError", result.stderr)

    def test_allocation_under_the_cap_succeeds(self):
        result = allocate(gigabytes=0.1, cap_gigabytes=1)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("allocated", result.stdout)


if __name__ == "__main__":
    unittest.main()
