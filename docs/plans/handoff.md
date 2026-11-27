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
install, the staleness check of `components.md`). The M4 plan and its
sub-tasks are in `rewrite.md`. M3's open questions, settled with Ale on
2026-11-27:

- **A run is one job**, owning the component memories (`components.md`,
  "A run"); sub-task b2.
- **`OwnedFiles` vs `GameFolder`:** the record (sub-task c) becomes the one
  home of which files are ours; both read it.
- **`CatalogEntry.Repository`:** the source becomes typed (a GitHub
  repository or a web site), so ReShade's row has no repository; sub-task a.
- **NVIDIA redirect refusals:** kept; the mirror's copies carry NVIDIA's
  signature, the log warns and the report notes it; sub-task a.
- **One catalog row per source** (`components.md`, "The catalog"); sub-task a.
- **Mirror sort:** by version, then by tag, ordinal, where versions do not
  compare; sub-task a.

**First sub-task, decided by Ale:** the mirror's `dlssd-` releases become
the `dlssd` family (a `dlssd-` prefix before `dlss-` in `MirrorFamilies`),
behind NVIDIA's own entry and checked by NVIDIA's signer like `dlss` and
`dlssg`. `components.md` already says so.

**Also in the first sub-task, decided by Ale:** the `nvngx_dlssnr.dll` trust
rule and preference of `components.md` ("The mirror is accepted") and `gpu.md`
("The `nvngx_dlssnr.dll` build for the card"). Today `CatalogEntry`'s dlssnr
row asks NVIDIA's signer of every build, which refuses every build for RTX
20, 30 and 40 (all community builds, unsigned or with a broken signature).
The new rule: a label with a pinned DLL SHA-256 is checked against it, any
other needs NVIDIA's signer. The orchestrator measures the digests first
(download each community build into the lab, hash the DLL; the arch and
signature reader is `~/coding/claude-lab/dlssnr-arch/arch.py`), the pins
become data beside `RenodxPins`, and `DlssnrBuilds._preferred` takes the new
table.

**Also decided by Ale:** motion provider 4 (LumeniteFX QuantMotion) leaves the
choices (`routes.md`, motion-vector provider): LumeniteFX renamed it to
PocketMV and the feeder does not bind it yet (jlrouzies-fr/DLSS5-Feeder#164).
`MotionProvider.LumeniteQuantMotion` and its files go from the sources; a
setting naming 4 stops before writing with the spec's message. When the
feeder ships PocketMV, 4 returns with that release as its minimum.

**Also decided by Ale:** the Remix runtime is pinned to tag `dlssnr-v1`
(`components.md`, catalog and pins) instead of `/latest`;
`ReleaseComponents.RemixRuntimeAsync` asks it by tag like the MFG pins. Our
own fork of the runtime (one additive commit of about 2,500 lines on Remix
Plus) is reconsidered in M8, when the Remix route is built, if `dlssnr-v1`
proves too old for the mods in use.

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
