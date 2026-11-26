# Agent and contributor guide

A hard fork of Kizzuwatnaa/DLSS5-Autopilot: a Windows tkinter app, Python 3.12,
standard library only at run time. What it does for the person using it is the
spec; the inherited code and its comments are hypotheses. The plan, the
decisions and the open phases are in [docs/plans/takeover.md](docs/plans/takeover.md):
start there.

## Layout

- `dlss5_autopilot.py`: the entry point (window and command line).
- `core/`: the engine (detection, routes, installer, diagnosis, updates).
  `core/ui/`: the window. `core/diagnose/`: logs to verdict and bug-report body.
- `pages/`: the compatibility feed and its searchable page, built and
  published by `.github/workflows/compatibility.yml`. `.github/workflows/`
  holds only that and the release; no other CI yet.
- `_tools/`: the report corpus (`reports/`, `verdict_baseline.json`) and the
  scripts that replay it (`replay_report.py`, `verdict_check.py`).
- `tests/`: the pytest suite and its fixtures (`fake_pe.py` builds a PE image).

## Commands

Set up once with `uv sync` (Python 3.12.11, see `.python-version`). Every
command goes through `uv run poe <task>`:

- `fix`: ruff autofix and format. Run it first, before anything else.
- `check-files <paths>`: format, lint and type check of the files you touched.
  Needed before every commit while the tree is not clean: a commit must leave
  the files it touches clean.
- `check`: every read-only check over the whole tree (lockfile, format, lint,
  types, dead code, import layering). Run it for a whole-tree view; it fails
  until phase 4 lands.
- `build`: the exe, with the release workflow's flags. Windows only.

Do not re-run a check on a tree that has not changed since it passed. Rules
are never silenced to pass a check: a per-line suppression needs a confirmed
false positive and the reason on the line.

## Tests

pytest, in `tests/`. `uv run poe test` runs it, and takes pytest's arguments
for a focused run (`uv run poe test -k diagnose`, a path, `-m network`);
`check` runs the whole suite with the coverage floor, which only rises.

- A test describes what the tool does for the person using it, built from a
  real log or a real folder where there is one. A module gets its tests
  when it is triaged: the tracker's phases 3 and 4. Until then it has none.
- No network, no window: both are enforced. A socket (pytest-socket) or a
  `tkinter.Tk()` (an autouse fixture in `tests/conftest.py`) fails the test.
  Put the logic under test behind a function that takes plain values.
- Markers, strict: `windows` for a test that needs Windows (skipped
  elsewhere, run by the release workflow); `network` for one that reaches the
  real internet (deselected by default, `-m network` runs only those).
- Every test has 10 seconds; a hung one fails with its stack. One that needs
  longer takes `@pytest.mark.timeout(n)` with the reason on the line above.
- Fixtures in `tests/conftest.py`: `home` (a user profile under `tmp_path`:
  HOME, USERPROFILE, LOCALAPPDATA, APPDATA, PROGRAMDATA and the settings,
  cache and log paths the app computes at import), `x64_exe` (a 64-bit PE
  from `tests/fake_pe.py`, never a file from the machine) and `game_folder`
  (a factory: a folder with a named exe and optional extra files). Any test
  that makes the app read or write its own files takes `home`.
- `python _tools/verdict_check.py` (the report corpus through the diagnosis)
  opens no window and runs anywhere.

## Git

After cloning, run `git config blame.ignoreRevsFile .git-blame-ignore-revs`.
