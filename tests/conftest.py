"""Shared pytest configuration: the guards every test runs under."""

from __future__ import annotations

import platform
import tkinter as tk
from typing import TYPE_CHECKING, NoReturn

import pytest

if TYPE_CHECKING:
    from collections.abc import Iterable

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
