"""Shared pytest configuration: the guards every test runs under."""

from __future__ import annotations

import platform
import tkinter as tk
from typing import TYPE_CHECKING, NoReturn, Protocol

import pytest

from tests.fake_pe import dll

if TYPE_CHECKING:
    from collections.abc import Iterable, Mapping
    from pathlib import Path

pytest_plugins = ["pytester"]  # tests/test_guards.py runs pytest inside a test

NO_WINDOW = (
    "a test tried to create a Tk window: unit tests open no window; "
    "put the logic under test behind a function that takes plain values"
)


def pytest_collection_modifyitems(items: Iterable[pytest.Item]) -> None:
    on_windows = platform.system() == "Windows"
    for item in items:
        if item.get_closest_marker("windows") and not on_windows:
            item.add_marker(pytest.mark.skip(reason="needs Windows"))
        if item.get_closest_marker("network"):
            # pytest-socket's own marker: the real internet for this test only.
            item.add_marker(pytest.mark.enable_socket)


@pytest.fixture(autouse=True)
def _no_window(monkeypatch: pytest.MonkeyPatch) -> None:
    def refuse(*_args: object, **_kwargs: object) -> NoReturn:
        pytest.fail(NO_WINDOW, pytrace=False)

    monkeypatch.setattr(tk.Tk, "__init__", refuse)


# Where the app keeps its settings, library, profiles, art, log, cache and
# sightings, as "module.constant". Each is computed from LOCALAPPDATA when its
# module is imported, so setting the environment afterwards is too late and
# the constant itself is redirected. Mirrors `core/prefs.py` FILE, `log.py`
# DIR and FILE, `net.py` CACHE, `sources.py` _API_CACHE, `watch.py` RECORD,
# `diagnose/model.py` STANDALONE_LOG and, derived from `prefs.FILE` at import,
# `library.py` FILE, `profiles.py` DIR and `covers.py` ROOT.
_APP_FILES = {
    "core.prefs.FILE": ("dlss5-autopilot", "settings.json"),
    "core.library.FILE": ("dlss5-autopilot", "library.json"),
    "core.profiles.DIR": ("dlss5-autopilot", "profiles"),
    "core.covers.ROOT": ("dlss5-autopilot", "art"),
    "core.log.DIR": ("dlss5-autopilot",),
    "core.log.FILE": ("dlss5-autopilot", "autopilot.log"),
    "core.net.CACHE": ("dlss5-autopilot", "cache"),
    "core.sources._API_CACHE": ("dlss5-autopilot", "api-cache"),
    "core.watch.RECORD": ("dlss5-autopilot", "sightings.json"),
    "core.diagnose.model.STANDALONE_LOG": ("RHI", "Logs", "standalone-dlssnr.log"),
}


@pytest.fixture
def home(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> Path:
    """An isolated user profile: nothing the app reads or writes is the real one."""
    profile = tmp_path / "home"
    local = profile / "AppData" / "Local"
    program_data = tmp_path / "ProgramData"
    local.mkdir(parents=True)
    # Path.home() follows HOME on POSIX and USERPROFILE on Windows.
    monkeypatch.setenv("HOME", str(profile))
    monkeypatch.setenv("USERPROFILE", str(profile))
    monkeypatch.setenv("LOCALAPPDATA", str(local))
    monkeypatch.setenv("APPDATA", str(profile / "AppData" / "Roaming"))
    monkeypatch.setenv("PROGRAMDATA", str(program_data))
    for target, parts in _APP_FILES.items():
        monkeypatch.setattr(target, local.joinpath(*parts))
    # core/covers.py EPIC_DATA reads PROGRAMDATA at import too.
    monkeypatch.setattr(
        "core.covers.EPIC_DATA",
        program_data / "Epic" / "EpicGamesLauncher" / "Data",
    )
    return profile


@pytest.fixture
def x64_exe(tmp_path: Path) -> Path:
    """A 64-bit PE built by tests/fake_pe.py, never a file from the machine."""
    exe = tmp_path / "x64.exe"
    exe.write_bytes(dll())
    return exe


class GameFolderFactory(Protocol):
    def __call__(
        self,
        name: str = ...,
        *,
        exe: str = ...,
        files: Mapping[str, bytes] | None = ...,
    ) -> Path: ...


@pytest.fixture
def game_folder(tmp_path: Path) -> GameFolderFactory:
    """Makes a game folder holding `exe` (a 64-bit PE) and the extra `files`."""

    def make(
        name: str = "Game",
        *,
        exe: str = "Game.exe",
        files: Mapping[str, bytes] | None = None,
    ) -> Path:
        folder = tmp_path / "games" / name
        for relative, content in {exe: dll(), **(files or {})}.items():
            path = folder / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
        return folder

    return make
