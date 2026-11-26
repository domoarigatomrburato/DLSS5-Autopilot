"""The guards hold: no network, no window, and a hung test fails."""

from __future__ import annotations

import re
import socket
import tkinter as tk

import pytest

from tests.conftest import NO_WINDOW


def test_no_socket_reaches_the_internet() -> None:
    # pytest-socket warns before it raises, and the ini turns warnings into errors.
    with pytest.raises((RuntimeError, UserWarning), match="socket"):
        socket.create_connection(("example.com", 443), timeout=1)


def test_no_tk_window_opens() -> None:
    with pytest.raises(pytest.fail.Exception, match=re.escape(NO_WINDOW)):
        tk.Tk()


def test_a_hung_test_fails_with_a_timeout(pytester: pytest.Pytester) -> None:
    pytester.makepyfile(
        """
        import time
        import pytest

        @pytest.mark.timeout(1)
        def test_sleeps_too_long():
            time.sleep(30)
        """
    )
    result = pytester.runpytest_subprocess()
    result.assert_outcomes(failed=1)
    result.stdout.fnmatch_lines(["*Timeout (>1.0s)*"])
