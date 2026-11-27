"""Humankind's Windows folders as WSL paths. HK_DOCS / HK_LOCALLOW / WIN_HOME override; otherwise derived from
%USERPROFILE%, so no user-specific path lives in the code."""
import functools
import glob
import os
import subprocess


@functools.lru_cache(maxsize=None)
def windows_home():
    if os.environ.get("WIN_HOME"):
        return os.environ["WIN_HOME"]
    profile = subprocess.run(["cmd.exe", "/c", "echo %USERPROFILE%"], capture_output=True, text=True, cwd="/mnt/c").stdout.strip()
    return subprocess.run(["wslpath", profile], capture_output=True, text=True).stdout.strip()


def docs():
    return os.environ.get("HK_DOCS") or os.path.join(windows_home(), "Documents", "Humankind")


def locallow():
    return os.environ.get("HK_LOCALLOW") or os.path.join(windows_home(), "AppData", "LocalLow", "Amplitude Studios", "Humankind")


def logs():
    return os.path.join(docs(), "Temporary Files")


def community():
    return os.path.join(docs(), "Community")


def registry():
    """The mod list of whichever profile folder exists (Users/<profile id>/Registry.xml)."""
    return next(iter(sorted(glob.glob(os.path.join(docs(), "Users", "*", "Registry.xml")))), None)
