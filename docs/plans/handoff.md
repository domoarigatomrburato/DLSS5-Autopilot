# Handoff: DLSS5 Autopilot after M3

Written 2026-11-27 at the end of the session that did M3. Start the next
session from this file.

## State

- Repository `domoarigatomrburato/DLSS5-Autopilot`, branch
  `rewrite/scaffold`, in sync with origin. `main` is the Python app (b84e39c).
- The Python reference is tag `python-reference`, checked out read-only beside
  the repository as `DLSS5-Autopilot-reference`.
- M0–M3 are ticked in `docs/plans/rewrite.md`. Gate green: 2002 tests,
  98.74% line coverage, floor 98 in `coverage-floor.txt`.
- No GitHub CI on the branch (first CI release is M7). No worktrees or extra
  branches left.

## What M3 left

- `Routes/`: the route decision and why (`routes.md`), the warnings, the
  components each route needs (`RouteComponents`, from `RouteFacts`).
- `Gpu/`: the card and the driver as facts.
- `Components/`, one way down:
  root (`ComponentRelease`, `ComponentException`, `ComponentVersion`,
  `ReleaseSource`, `ResolutionMemory`, `ComponentHosts`) → `GitHub/`
  (API, cache, pages fallback) → `Catalog/` (the one table of sources and
  checks) → `Fetch/` (download into the cache, layout check), `Mirror/`
  (the mirror and NVIDIA's runtimes), `Sources/` (ReShade, feeder,
  OptiScaler and the rest) → `Routes/`.
- Net: a HEAD that reports a redirect, a 5xx body read on request,
  `NetFallback.OrNullAsync`, `TextAnswer.IsSuccess`.

## Next: M4, Install and uninstall

Spec: `docs/spec/install.md` and the M4 line of the plan, which now also
carries M3's leftovers (route settings and option types, refusals before an
install, the staleness check of `components.md`). Open questions to settle
first:

- **What a "run" is.** `ResolutionMemory`, the latest-tag, list and
  release-page memories live for the process: a long-running app never sees
  new releases, and shared resolutions run uncancelled. M4 (install) and M7
  (autopilot) need a run object that owns them.
- **`OwnedFiles` vs `GameFolder`:** the rule of which files the tool owns is
  written twice; M4's record should become its one home.
- **`CatalogEntry.Repository`** throws for a web-site source (ReShade's
  `reshade.me`); callers must not reach it for that row.
- **NVIDIA redirect refusals:** `NvidiaRuntimes` swallows every
  `NetException`, refusals included, and the mirror serves alone
  (`MirrorCatalog` tests encode it on purpose). Keep or narrow, decided with
  the install's error texts.
- **Catalog rows per source:** OptiScaler's six repositories and the
  mirror's `dlss`/`dlssg` borrow one row each and are rewritten in code.
  One row per source would make the catalog the whole truth; it changes
  `components.md`'s table, so it needs Ale.
- **Mirror sort:** `ComponentVersion.Order` ties labels that do not compare,
  so it is not a strict order when a family mixes numeric and other labels.
  Check the mirror's real tags or sort comparable labels first.

**First sub-task, decided by Ale:** the mirror's `dlssd-` releases become
the `dlssd` family (a `dlssd-` prefix before `dlss-` in `MirrorFamilies`),
behind NVIDIA's own entry and checked by NVIDIA's signer like `dlss` and
`dlssg`. `components.md` already says so.

## How we work (binding, besides AGENTS.md and the plan's Decisions)

- Replies to Ale in Italian, short; code, docs, commits in English.
  Commits as ale.burato@icloud.com, trailer
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Maximum autonomy; no polling; minor calls decided and reported.
- **Implementers on Opus, never Sonnet:** medium for moderate, well-specified
  sub-tasks, high for complex ones. Sonnet's output ends up rewritten.
- **Shared infrastructure first.** Plan the milestone so the pieces every
  sub-task needs (memories, helpers, exception kinds, test beds) land in a
  first, sequential sub-task. Parallel sub-tasks then reuse them; in M3,
  parallel implementers solved the same problem three to six ways.
- **Review per sub-task, on small diffs.** Size each sub-task so its diff
  stays at a few thousand lines at most, and run `/simplify` (fixes gated)
  then `/code-review` at `high` on it before merging. At the end of the
  milestone run one narrow cross-cutting `/simplify` (reuse and altitude)
  over the whole milestone. M3 was reviewed once over about 15k lines and
  needed two `/simplify` rounds plus a third after the namespace split.
- One brief per sub-task in the scratchpad: context and spec sections to
  read, settled design, tests first, the stop sentence, checks
  (`dotnet test --project ...` while iterating, `dotnet run gate.cs -- fix`
  once at the end), report with the tree fingerprint. Review each diff,
  recompute the fingerprint, commit per sub-task.
- Independent sub-tasks may run in parallel in hand-made worktrees
  (`git worktree add -b <branch> ../DLSS5-Autopilot-<x> <commit>`); LSP does
  not answer there, use grep and ast-grep.
- Internals are tested through `InternalsVisibleTo`; nothing becomes public
  for tests.
- Stores and similar knowledge: follow Playnite (MIT) where it applies;
  GPL code may be read, never copied. Only MIT-compatible packages.

## Manual checks

- **Low end:** Ale has an old gaming laptop with a power-limited RTX 2080
  (sm 75). From M4 on, check installs there too: it is the card the
  `dlssnr` label preference and the steering rules treat differently.
- **Windows:** the "To verify" sections of `docs/spec/net.md`, `pe.md` and
  `library.md` become checkable with the Windows implementations in M7.

## Questions for Ale from M3

- Newer `nvngx_dlssnr` builds exist than the label preference names
  (`gpu.md` Q1): prefer by label or by version?
- LumeniteFX `mainline` no longer has `lumenite_QuantMotion.fx`, so motion
  provider 4 answers "layout changed".
- The Remix runtime's `/latest` is `dlssnr-v1`.
- An oversized 5xx body that the caller accepts fails as `TooLarge` (M3a).
