# Takeover and hardening plan

This repository is a hard fork of
[Kizzuwatnaa/DLSS5-Autopilot](https://github.com/Kizzuwatnaa/DLSS5-Autopilot)
(MIT). It stops tracking upstream once the tree is reformatted; until then,
fixes that matter to upstream users go back as small pull requests.

This file is the tracker: tick items as they land, and keep the decisions
section current. Any coding agent picking the work up starts here.

## Decisions

- **Hard fork.** The self-updater, the shared compatibility list, the bug
  report links and the release pipeline point at
  `domoarigatomrburato/DLSS5-Autopilot`. Upstream merges stop after the
  formatting commit.
- **Upstream courtesy.** The two critical installer fixes (phase 0) go
  upstream as two separate pull requests, in upstream's style, before the
  fork diverges.
- **Identity.** The fork's name everywhere (copyright, the exe's
  `CompanyName`, signing roles) is the GitHub handle `domoarigatomrburato`;
  upstream's copyright stays beside it.
- **Compatibility feed on GitHub Pages.** The workflow already builds and
  deploys the searchable page; `compatibility.json` is published beside it
  and the client reads it from
  `https://domoarigatomrburato.github.io/DLSS5-Autopilot/compatibility.json`.
  No bot commits on any branch. The build reads this repository's issues and,
  read-only, upstream's, so results shared by upstream's users keep counting;
  a failed read fails the run and Pages keeps the previous file.
- **Runtime stays standard library only.** The shipped exe carries Python,
  tkinter and certifi, nothing else. Development tools are welcome, pinned
  in `uv.lock`.
- **Tests move to pytest.** `test_all.py` is split by module into
  `tests/`; checks that read source text (`src_of`) are rewritten as
  behaviour tests or dropped. Unit tests run offline; tests that need the
  network or Windows are marked and run where those exist.
- **Gates mirror the IDP Quality Gates model**, translated to Python:
  formatter and linter with autofix, a strict type checker whose existing
  findings sit in a baseline so only new code is held to it, dead code,
  import layering and cycles, complexity reported but not blocking, and a
  ratchet that only grows.

## Toolchain

| Concern | Tool | Blocks |
| --- | --- | --- |
| Environment and dev dependencies | uv, `pyproject.toml`, `uv.lock` | lockfile drift |
| Formatting | `ruff format` | everywhere |
| Lint | `ruff check`; the selected rule list is the ratchet, a rule joins it once its findings are fixed | selected rules everywhere |
| Types | basedpyright, strict, with a committed baseline | new findings only |
| Dead code | vulture, with a reviewed whitelist | everywhere |
| Layering and cycles | import-linter (`core` never imports `core.ui`; no cycles between modules) | everywhere |
| Complexity | ruff `C901`, `PLR0912`, `PLR0915` | report only |
| Tests and coverage | pytest, pytest-cov | failures; coverage floor that only rises |
| CI | GitHub Actions on push and pull request: lint on Ubuntu, tests on Windows and Ubuntu; actions pinned by SHA; Dependabot for actions and uv | required checks |

## Running the current suite before phase 3

Until the pytest migration, `test_all.py` needs a 64-bit PE fixture and
downloads real components (about 700 MB) into a cache under `HOME`. Keep
both out of the repository and point the suite at them:

Python 3.12 comes from `uv python install 3.12` where the system has none.

```
python3.12 -I -c "import sys; sys.path.insert(0, '_tools'); import fake_pe; open('<lab>/fake-host64.exe', 'wb').write(fake_pe.dll('1.0.0'))"
env HOME=<lab>/home DLSS5_TEST_X64=<lab>/fake-host64.exe python3.12 test_all.py > <lab>/run.log 2>&1
```

On macOS it takes about 2.5 minutes and stops in section 31 (it needs
`C:\Windows\System32\where.exe`). After the phase 0 fixes the 40 sections
before that give 728 PASS and 24 FAIL. All 24 are Windows-only APIs
(`winreg`, `ctypes.WINFUNCTYPE`, Windows paths): compare the sorted
`  FAIL  ` lines with a baseline run, never the exit code. On Windows the
whole suite should run; that has not been checked yet.
`test_reshade_ini.py` and `python -m unittest test_scan` pass everywhere.

## Phases

### 0. Critical installer fixes (upstream pull requests)

- [x] A route or mode switch re-reads ownership after uninstalling the
      previous route, so the user's restored files are backed up again and
      never recorded as ours (`installer.install`, `rep.preinstalled`).
- [x] A failed backup stops the overwrite or delete instead of being
      swallowed (`installer._backup`, `dxvk`, `refw`, `mfg`); a partial
      backup is removed before the error is raised.
- [x] Both merged into this fork's `main`.
- [x] Both offered upstream:
      [#1068](https://github.com/Kizzuwatnaa/DLSS5-Autopilot/pull/1068)
      (`fix/route-switch-keeps-user-files`) and
      [#1069](https://github.com/Kizzuwatnaa/DLSS5-Autopilot/pull/1069)
      (`fix/failed-backup-stops-overwrite`), based on upstream `1736a20`.
      Delete the two branches once upstream merges or closes them.

### 1. Identity

- [ ] Repoint `core/update.py` `REPO`, `core/community.py` `FEED_URL`,
      `core/ui/vrpage.py`, `version_info.txt`, `.github/scripts/*`, README
      verification and roles.
- [ ] README: a fork notice with credit to upstream; NOTICE and LICENSE
      keep the original copyright and add ours.
- [ ] SECURITY.md: private advisories on this repository only.
- [ ] Compatibility results stop committing to `main` (87% of upstream's
      history is that bot): `compatibility.json` is published with the
      Pages site, read by the client from there (see Decisions).
- [ ] Release workflow: pinned PyInstaller and certifi, actions by SHA,
      signing off until this fork has its own SignPath project.

### 2. Tooling baseline

- [ ] `pyproject.toml` with uv; dev dependencies pinned.
- [ ] One `ruff format` commit, listed in `.git-blame-ignore-revs`.
- [ ] Ruff with the safe autofixes applied; starting rule list green.
- [ ] basedpyright strict baseline committed.
- [ ] vulture whitelist reviewed (dead code removed, not whitelisted).
- [ ] import-linter contracts; the current violations listed as phase 6
      work.
- [ ] CI workflow and branch protection on `main`.
- [ ] `AGENTS.md` (and a `CLAUDE.md` that points to it): layout, the gate
      command, when each check is needed, autofix first.

### 3. Tests

- [ ] pytest scaffold, fixtures for a game folder and an isolated HOME and
      cache; the 64-bit PE fixture built by `_tools/fake_pe.py`, never a
      file on someone's disk.
- [ ] `test_all.py` sections moved to `tests/` module by module; source
      text checks rewritten or dropped.
- [ ] Network replaced by local fixture archives in unit tests; a marked
      network suite for the real publishers.
- [ ] Windows CI runs the whole suite; macOS and Linux run what applies.
- [ ] Coverage measured, floor set.

### 4. Security

- [ ] The all-users Vulkan layer is never built from the per-user cache
      (download to an admin-only folder, or verify the ReShade setup's
      signer).
- [ ] Self-update: refuse a release without `SHA256SUMS.txt`, verify the
      Authenticode signer once releases are signed, hash the whole archive.
- [ ] Hash or signer checks for third-party components where publishers
      provide them (NVIDIA signature, published sums); pin mirrors.
- [ ] Download size limits; refuse https → http redirects.
- [ ] Absolute System32 paths for `cmd`, `powershell`, `tasklist`,
      `explorer`.
- [ ] Compatibility list: one vote per author per game and route;
      `compat_page.py` escapes `<` as `<`.

### 5. Installer correctness

- [ ] Closing the window during an install waits for it or records first.
- [ ] Files enter the record before they are written, so a partial write
      is rolled back.
- [ ] A failed restore during uninstall keeps its backup on the record.
- [ ] A corrupt record falls back to the known-names uninstall; atomic
      writes `fsync` before `os.replace`.
- [ ] No file is moved or deleted before the last refusal check.
- [ ] Game updates between install and uninstall detected by hash.
- [ ] Emulator config: restore only the keys we changed.
- [ ] ReShadePreset technique removal only on routes that wrote it.
- [ ] Layer bookkeeping compares resolved paths; unreadable settings never
      drop `vulkan_games`.
- [ ] `sidelined` entries pass `_permitted`.

### 6. Structure

- [ ] Split `installer.install` (1,528 lines), `installer.preview` (597),
      `installer.uninstall` (433) and `diagnose.chain._analyse` (1,335)
      into named steps.
- [ ] `core/diagnose`: explicit imports instead of `import *` and the
      `globals()` re-export.
- [ ] One version parser, one path-equality helper, one HTTP retry loop.
- [ ] `except Exception` audit: each one either narrows, logs with intent,
      or goes.
- [ ] Function-level imports that dodge cycles: fix the cycle instead.
- [ ] `core/reportui.py` and `core/compareui.py` move under `core/ui`.

### 7. Docs

- [ ] README rewritten around what a user does first; layout section
      generated from reality; claims checked against code.
- [ ] CONTRIBUTING for this fork: the gate, the test layout, the PR rules.
