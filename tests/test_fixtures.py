"""The fixtures hold what they promise."""

from __future__ import annotations

import os
import struct
from pathlib import Path
from typing import TYPE_CHECKING

from core import covers, library, log, net, pe, prefs, profiles, watch
from core.diagnose import model
from tests.fake_pe import I386, dll

if TYPE_CHECKING:
    from tests.conftest import GameFolderFactory

_BITS_64 = 64
_BITS_32 = 32
_PE32_PLUS = 0x20B  # the optional header's magic
_OPTIONAL_HEADER = 0x40 + 24  # the PE header sits right after the 0x40-byte stub


def _magic(exe: Path) -> int:
    (magic,) = struct.unpack_from("<H", exe.read_bytes(), _OPTIONAL_HEADER)
    return magic


def test_home_redirects_the_profile_and_everything_the_app_keeps(home: Path) -> None:
    assert Path.home() == home
    assert Path(os.environ["USERPROFILE"]) == home
    kept_by_the_app = (
        prefs.FILE,
        library.FILE,
        profiles.DIR,
        covers.ROOT,
        covers.EPIC_DATA,
        log.FILE,
        net.CACHE,
        watch.RECORD,
        model.STANDALONE_LOG,
    )
    for kept in kept_by_the_app:
        assert kept.is_relative_to(home.parent)


def test_x64_exe_is_a_64_bit_pe(x64_exe: Path) -> None:
    assert _magic(x64_exe) == _PE32_PLUS
    assert pe.exe_bitness(x64_exe) == _BITS_64


def test_fake_pe_can_also_build_a_32_bit_image(tmp_path: Path) -> None:
    exe = tmp_path / "x86.exe"
    exe.write_bytes(dll(machine=I386))
    assert pe.exe_bitness(exe) == _BITS_32


def test_game_folder_holds_the_exe_and_the_extra_files(
    game_folder: GameFolderFactory,
) -> None:
    folder = game_folder("Hades", exe="bin/Hades.exe", files={"readme.txt": b"hi"})
    assert folder.name == "Hades"
    assert pe.exe_bitness(folder / "bin" / "Hades.exe") == _BITS_64
    assert (folder / "readme.txt").read_bytes() == b"hi"
    assert sorted(p.name for p in folder.rglob("*") if p.is_file()) == [
        "Hades.exe",
        "readme.txt",
    ]
