"""Installed by setup.sh into the venv with a .pth hook, so every bundle tool and one-off "python -c" scan
gets a virtual-memory ceiling. An uncapped 8.5 GB UnityPy scan got the whole WSL distro killed (2026-11-25).
ZK_PY_MEMORY_GB sets the cap in GB (default 4); 0 lifts it for a deliberate big job."""
import os
import resource

_cap_gigabytes = float(os.environ.get("ZK_PY_MEMORY_GB", "4"))
if _cap_gigabytes > 0:
    _hard = resource.getrlimit(resource.RLIMIT_AS)[1]
    _soft = int(_cap_gigabytes * 1024 ** 3)
    # A soft limit above the inherited hard one makes setrlimit raise at every interpreter start.
    if _hard != resource.RLIM_INFINITY:
        _soft = min(_soft, _hard)
    resource.setrlimit(resource.RLIMIT_AS, (_soft, _hard))
