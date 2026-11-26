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
  scripts that replay it (`replay_report.py`, `verdict_check.py`, `fake_pe.py`).
- `tests/`: fixtures for the pytest suite that replaces the top-level
  `test_*.py` files (phase 3).

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

`test_all.py`, `test_clean_machine.py` and `test_scan.py` create Tk windows
(`test_scan` an invisible one, which can still take the focus): never run them
on a desktop. Run them headless in a Linux container with Xvfb, as the
tracker's "Running the current suite before phase 3" describes.
`python test_reshade_ini.py` and `python _tools/verdict_check.py` (the report
corpus through the diagnosis) open no window and run anywhere.

## Git

After cloning, run `git config blame.ignoreRevsFile .git-blame-ignore-revs`.
