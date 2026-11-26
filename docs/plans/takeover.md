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
- **Tests are written fresh, at triage.** The inherited `test_all.py`
  (about 2,300 checks over 129 sections that share state) is deleted, not
  ported: it pins how the code works, and the code is being rewritten. When
  phase 4 triages a module, its pytest tests come first and describe what
  it does for the user; then it is kept, repaired or rewritten behind them.
  A module not triaged yet has no tests. The old checks stay in git history
  as a list of behaviours to look up (`git show 3d8c276:test_all.py`).
  Unit tests run offline and open no window; tests that need the network
  or Windows are marked and run where those exist.
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

- [x] `pyproject.toml` with uv; dev dependencies pinned; `uv run poe fix`,
      `check`, `check-files <paths>` (the files a commit touches) and
      `build` (replaces `build.bat`).
- [x] Full gate configuration, every check blocking (see Decisions). After
      the clean-up commits: format clean, ruff 5,988 findings (125 C901,
      73 PLR0912, 54 PLR0915), basedpyright 17,564 errors (the top-level
      tests and `pages/` now counted), vulture 116, both import contracts
      broken.
- [x] Pruned: upstream's maintenance scripts and agent hooks in `_tools/`,
      `build.bat`, `test_install.py` (hard-coded to the original author's
      disk), and every check that inspected the code instead of what the
      app does (source text, line caps, structure rules). Kept, as the
      regression corpus of the diagnosis users see: the 184 reports,
      `verdict_baseline.json`, `replay_report.py`, `verdict_check.py`, and
      `fake_pe.py`; phase 3 moves them under `tests/`.
- [x] The feed scripts moved from `.github/scripts` to `pages/`: the type
      checker never reads dot directories.
- [x] One `ruff format` commit, listed in `.git-blame-ignore-revs`.
- [x] Ruff safe autofixes applied (PERF101 left out in `core/ui/art.py`:
      the `list()` copy is a snapshot against another thread).
- [x] `core/diagnose`: explicit imports instead of `import *` and the
      `globals()` re-export.
- [x] import-linter contracts written (`core.ui` protected, no cycles
      between `core`'s modules); violations listed in phase 4.
- [x] `AGENTS.md` (and a `CLAUDE.md` that points to it): layout, the gate
      command, when each check is needed, autofix first.

### 3. Test scaffold

- [x] The inherited top-level tests deleted (`test_all.py`,
      `test_clean_machine.py`, `test_reshade_ini.py`, `test_scan.py`; see
      Decisions).
- [x] pytest configured in `pyproject.toml` and run by `check`: strict
      markers (`windows`, `network`), Windows-only tests skipped elsewhere,
      the `network` ones only on request.
- [x] Every test has a timeout (`pytest-timeout`): a low default for all (5 seconds),
      raised only where a test needs it, with the reason beside it. A hung
      test fails with its stack instead of stopping the suite.
- [x] Unit tests cannot reach the network or open a window: an unexpected
      socket or Tk root fails the test.
- [x] Fixtures for an isolated HOME and cache and a game folder; the
      64-bit PE fixture built by `fake_pe.py`, never a file on someone's
      disk.
- [x] First test: every module imports, with warnings as errors.
- [x] Coverage measured, floor set; it only rises.
- [x] The release workflow runs the tests on Windows before it builds, in
      place of the two deleted scripts.

### 4. Clean-up of the remaining findings

Every fix here can change behaviour, so each one comes with a test.

- [ ] Triage every module by what it does for the user: first its tests
      (the old checks in git history say what to look for), then keep,
      repair or rewrite behind them; kept and repaired modules get their
      annotations here.
- [ ] The DLSS page's behaviour, when its module is triaged; the pruned
      `_tools/dlss_page_check.py` (in git history) listed it: update all
      takes only what it may (not an anti-cheat game, not a running one),
      the originals stay beside the new files and restore puts them back,
      a refused file says where and why with nothing changed, versions read
      "current -> newest", a stale read waits for the new one.
- [ ] The diagnosis, when triaged: the report corpus (`_tools/reports`,
      `verdict_baseline.json`) becomes a parametrised test; 28 reports
      already get a verdict other than the recorded one, each judged right
      or wrong before the baseline is saved.
- [ ] basedpyright findings that need a behaviour change (attributes that
      do not exist, unhandled `None`, wrong argument types).
- [ ] `except Exception` audit: each one either narrows, logs with intent,
      or goes.
- [ ] Split `installer.install` (1,528 lines), `installer.preview` (597),
      `installer.uninstall` (433), `diagnose.chain._analyse` (1,335) and
      every other function over the complexity limits into named steps.
- [ ] One version parser, one path-equality helper, one HTTP retry loop.
- [ ] Function-level imports that dodge cycles: fix the cycle instead;
      import-linter contracts green. The cycle contract needs 17 imports
      gone, among them `autopilot`, `components` and `dlss` importing
      `installer`, and `community` importing `dlss` (`uv run lint-imports`
      lists them).
- [ ] `core/reportui.py` and `core/compareui.py` move under `core/ui`: they
      are the only `core` modules that import the window (`kit`, `theme`,
      `win`).
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
