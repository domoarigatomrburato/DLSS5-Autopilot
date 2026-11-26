"""Every module imports: no window, no socket, no warning."""

from __future__ import annotations

import importlib
import pkgutil
from pathlib import Path

import pytest

import core

# Modules that only import on Windows; they run on the Windows runner only.
WINDOWS_ONLY = frozenset(
    {
        "core.ui.tray",  # builds ctypes.WINFUNCTYPE at import, which only Windows has
    }
)

# pages/ is a folder of scripts, not a package: the import goes through the
# namespace package, which the repository root on sys.path provides.
PAGES = ("pages.compat_page", "pages.compatibility")


def _modules(package: str, folder: Path) -> list[str]:
    """Every module under a package, found on disk: nothing is imported."""
    names: list[str] = []
    for info in pkgutil.iter_modules([str(folder)], f"{package}."):
        names.append(info.name)
        if info.ispkg:
            names += _modules(info.name, folder / info.name.rsplit(".", 1)[1])
    return names


MODULES = ["dlss5_autopilot", *_modules("core", Path(core.__path__[0])), *PAGES]


@pytest.mark.parametrize(
    "name",
    [
        pytest.param(name, marks=pytest.mark.windows) if name in WINDOWS_ONLY else name
        for name in MODULES
    ],
)
def test_module_imports(name: str) -> None:
    importlib.import_module(name)
