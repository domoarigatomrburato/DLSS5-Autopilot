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
- **What the app does is the spec; the code is not.** Assume the inherited
  code is poorly built: it was written without engineering review. What
  matters is what the tool does for the person using it. Its comments,
  README claims, design choices
  and workarounds are hypotheses to check against behaviour and primary
  sources, never reasons in themselves; `test_all.py` records what the code
  does today, not what it should do. Keep what survives the check, replace
  what does not, and say which in the commit. Pruning and rewriting are
  encouraged, not merely allowed: code, tools, tests and docs with no clear
  use are deleted, and a part simpler to rewrite than to repair is
  rewritten from scratch behind tests that pin what it does for the user.
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
  formatter and linter with autofix, a strict type checker, dead code,
  import layering and cycles, complexity, tests with a coverage floor.
- **Everything blocks, nothing is grandfathered.** No type-checker
  baseline, no report-only rules, no ratchet: every selected rule blocks
  everywhere, and the existing findings are fixed, not recorded. Rules left
  out of the ruff selection are left out on purpose, each with its reason
  in `pyproject.toml`; per-line suppressions only for a confirmed false
  positive, with the reason on the line.
- **Clean-up order: prune and format, then tests, then repair or rewrite.**
  Pruning, formatting and safe autofixes come first. No annotation work goes
  into a module before it is judged worth keeping: each module is triaged
  (keep, repair, rewrite) once the tests pin what it does. Fixes that do (real type errors, broad
  `except`, complexity splits, dead code) wait for the pytest suite and each
  comes with a test. Until the tree is clean, a commit must leave the files
  it touches clean under the full gate.
- **No CI checks on GitHub for now.** The gate runs locally; GitHub Actions
  keeps only the compatibility and release workflows. CI and branch
  protection come once the tree is clean.

## Toolchain

| Concern | Tool | Blocks |
| --- | --- | --- |
| Environment and dev dependencies | uv, `pyproject.toml`, `uv.lock` | lockfile drift |
| Formatting | `ruff format` | everywhere |
| Lint | `ruff check`, an explicit rule selection; each family left out says why | everywhere |
| Types | basedpyright, strict, no baseline | everywhere |
| Dead code | vulture, with a reviewed whitelist | everywhere |
| Layering and cycles | import-linter (`core` never imports `core.ui`; no cycles between modules) | everywhere |
| Complexity | ruff `C901`, `PLR0912`, `PLR0915` | everywhere |
| Tests and coverage | pytest, pytest-cov | failures; coverage floor that only rises |
| CI | Later: GitHub Actions on push and pull request; the platform-neutral checks (format, lint, types, dead code, imports) on Ubuntu, the tests on Windows, the only platform the tool runs on; actions pinned by SHA; Dependabot for actions and uv. Until then the gate runs locally | required checks, once the tree is clean |

## Running the current suite before phase 3

Until the pytest migration, `test_all.py` needs a 64-bit PE fixture and
downloads real components (about 700 MB) into a cache under `HOME`. Keep
both out of the repository and point the suite at them:

Use Python 3.12.11 (`uv python install 3.12.11`): it is the last uv build
with Tk 8.6, the Tk the Windows build ships; later builds carry Tk 9.0, which
segfaults on macOS (`Tk_MacOSXGetTkWindow`) when a test updates a window.

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

The suite opens real Tk windows, which take the focus on a desktop. On a
developer's machine run it headless in a Linux container (Docker, or
`colima` on macOS) with Tk drawing into Xvfb:

```
FROM python:3.12.11-bookworm
RUN apt-get update && apt-get install -y --no-install-recommends xvfb xauth && rm -rf /var/lib/apt/lists/*
WORKDIR /src
```

```
docker run --rm -v "$PWD":/src -v <lab>/home:/cache -v <lab>:/lab -e HOME=/cache \
  -e DLSS5_TEST_X64=/lab/fake-host64.exe dlss5-tests xvfb-run -a python test_all.py
```

The suite still stops at the first Windows-only section there. To see every
section, run a copy rewritten so each top-level statement catches its own
exception and prints it as a `FAIL` line; compare base and change the same
way.

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

- [x] Repoint `core/update.py` `REPO`, `core/community.py` `FEED_URL`,
      `core/ui/vrpage.py`, `version_info.txt`, `.github/scripts/*`, README
      verification and roles.
- [x] README: a fork notice with credit to upstream; NOTICE and LICENSE
      keep the original copyright and add ours.
- [x] SECURITY.md: private advisories on this repository only.
- [x] Compatibility results stop committing to `main` (87% of upstream's
      history is that bot): `compatibility.json` is published with the
      Pages site, read by the client from there (see Decisions).
- [x] Release workflow: pinned PyInstaller and certifi, actions by SHA,
      signing off until this fork has its own SignPath project.

### 2. Tooling, pruning and behaviour-neutral clean-up

Measured on 2026-11-26 over `core` and `dlss5_autopilot.py` (44k lines):
basedpyright strict 14,244 errors (about 12,700 missing or unknown types),
ruff with every rule about 7,500 findings, 232 functions over the
complexity limits.

- [ ] `pyproject.toml` with uv; dev dependencies pinned; one local gate
      command (`fix` autofixes, `check` validates read-only).
- [ ] Full gate configuration, every check blocking (see Decisions).
- [ ] Prune what the product, the gate and the tests do not use, before
      spending gate work on it: upstream's maintenance scripts in `_tools/`
      (issue triage, stats, Microsoft submissions, its agent hooks), the 184
      user reports copied into `_tools/reports/`, `build.bat`, and the
      top-level `test_*.py` files the pytest suite will replace. Tests that
      import a `_tools` script keep it only if the check is worth keeping.
- [ ] One `ruff format` commit, listed in `.git-blame-ignore-revs`.
- [ ] Ruff safe autofixes applied.
- [ ] `core/diagnose`: explicit imports instead of `import *` and the
      `globals()` re-export.
- [ ] import-linter contracts written; violations listed for phase 4.
- [ ] `AGENTS.md` (and a `CLAUDE.md` that points to it): layout, the gate
      command, when each check is needed, autofix first.

### 3. Tests

- [ ] pytest scaffold, fixtures for a game folder and an isolated HOME and
      cache; the 64-bit PE fixture built by `_tools/fake_pe.py`, never a
      file on someone's disk.
- [ ] Every test has a timeout (`pytest-timeout`): a low default for all,
      raised only where a test needs it, with the reason beside it. A hung
      test fails with its stack instead of stopping the suite.
- [ ] `test_all.py` sections moved to `tests/` module by module; source
      text checks rewritten or dropped. The tests describe what the tool
      does for the user (the route it picks, the files it writes, backs up
      and restores, what it reports), not how the code does it: a check that
      pins an implementation detail is dropped, since the code under it may
      be rewritten.
- [ ] Network replaced by local fixture archives in unit tests; a marked
      network suite for the real publishers.
- [ ] Windows-only tests marked and skipped elsewhere, so macOS and Linux
      run everything else; no test opens a window on the screen of the
      person running it.
- [ ] The whole suite run once on Windows, when a Windows machine is
      available.
- [ ] Coverage measured, floor set.

### 4. Clean-up of the remaining findings

Every fix here can change behaviour, so each one comes with a test.

- [ ] Triage every module by what it does for the user: keep, repair or
      rewrite. Rewrites go module by module behind the phase 3 tests;
      kept and repaired modules get their annotations here.
- [ ] basedpyright findings that need a behaviour change (attributes that
      do not exist, unhandled `None`, wrong argument types).
- [ ] `except Exception` audit: each one either narrows, logs with intent,
      or goes.
- [ ] Split `installer.install` (1,528 lines), `installer.preview` (597),
      `installer.uninstall` (433), `diagnose.chain._analyse` (1,335) and
      every other function over the complexity limits into named steps.
- [ ] One version parser, one path-equality helper, one HTTP retry loop.
- [ ] Function-level imports that dodge cycles: fix the cycle instead;
      import-linter contracts green.
- [ ] `core/reportui.py` and `core/compareui.py` move under `core/ui`.
- [ ] Dead code removed (vulture clean, no whitelist entry without a
      reason).
- [ ] The full gate is green; CI workflow and branch protection on `main`.

### 5. Security

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

### 6. Installer correctness

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

### 7. Docs

- [ ] README rewritten around what a user does first; layout section
      generated from reality; claims checked against code.
- [ ] CONTRIBUTING for this fork: the gate, the test layout, the PR rules.
