# Handoff: Invidia Claritatis during M4

Written 2026-11-27 at the end of the session that planned M4 and did its
sub-task a, updated after sub-tasks e, f and i. Start the next session
from this file.

## State

- Repository `domoarigatomrburato/invidia-claritatis` (renamed from
  `DLSS5-Autopilot`), checked out as `invidia-claritatis`, branch `main`, in
  sync with origin. The rewrite is `main`; the Python app's last commit is the
  tag `python-reference` (b84e39c).
- The Python reference is tag `python-reference`, checked out read-only beside
  the repository as `invidia-claritatis-reference`.
- M0–M3 ticked in `docs/plans/rewrite.md`; M4 planned there as sub-tasks
  a–j, with **a, b1, b2, c, d, e, f, i, k and g1 done**. Gate green: 2759 tests,
  98.06% line coverage, floor 98 in `coverage-floor.txt`.
- **The tool is now Invidia Claritatis** (Decisions in `rewrite.md`); every
  name the tool gives is `invidia-claritatis`, in the specs and in the code.
- **Greenfield, never published:** nothing reads the reference's records,
  folders or settings, and no start-up cleanup of an old all-users layer
  exists. The specs dropped every compatibility rule of that kind.
- The `nvngx_dlssnr.dll` digests behind `DlssnrPins` were measured in
  `~/coding/claude-lab/dlssnr-digests/results.md`.

## Next: M4 sub-task g2, then h

Plan and order in `rewrite.md` (a → b1∥b2 → c → d → e∥f∥i → k → g → h → j).
g is split in two: **g1 is done** (the plan infrastructure, the ReShade base,
native); **g2** is next: upstream, bridge (with `dlss5-bridge.cfg` and the
bridge's ofa settings), renodx (`[RENODX-DLSS]`, the game that closes itself
at start), DXVK (native, bridge; the feeder reuses it in h), REFramework on RE
Engine games, and the "route not offered for the game" refusal. Write g2's
brief fresh, on g1's shape (below).

Where g1 put things (`src/Autopilot.Core/Install/Plans/`):

- `FolderSurvey` reads the folder once (names by relative path, config bytes
  through `Content(path)`, proxy content by `ContentMarks`, previous record's
  entries and components); `RoutePlans.For(request, survey, files)` is pure.
  `PlanRequest.Facts` must come from runtimes read **with** the record
  (`GameRuntimesReader` leaves our own files out).
- `PlanGather` resolves and fetches into the cache: a handler map for
  reshade, renodx, dlssnr, dlss, dlssd, and a generic path
  (`ReleaseGather.ComponentAsync`) for ids `ComponentSources` resolves, with an
  empty "generic ids supported" set g2 fills (upstream, bridge, dxvk,
  reframework). Other ids throw `NotSupportedException`; so does a native plan
  through DXVK until g2.
- `PlanFiles`: `GatheredFiles` (cache paths, facts hashed once; pinned
  `dlssnr` facts from the pin's digest) or `PreviewFiles` (names only,
  `PendingSource`; the transaction refuses a plan that `NeedsGathering`).
- `IniText` edits config files in place (keeps every other line, the BOM,
  UTF-16, the line endings); `ReShadeIni` writes `ReShade.ini`.
- `PlanNames.AddOnsOf(route)` is the purge table, read by placing and purging.
- The proxy name taken by DXVK, REFramework, Special K or anything not ReShade
  refuses in phase 1 (`FileRefusals.ProxyTaken`, `BlockerKind.ProxyTaken`); a
  hand-installed OptiScaler is moved aside with its companions.
- On Auto, a pinned `dlssnr` build whose pin excludes the card is skipped
  before it is downloaded.
- Settled: the overlay key stays per game in `RouteSettings` (the app fills in
  its one global value, M7); `ComponentRelease` takes its `CatalogEntry`.
- **To check on Windows:** the content marks (ReShade, OptiScaler in UTF-16,
  DXVK, REFramework, "SpecialK") against real DLLs: an injector naming
  ReShade must not pass for it.

**Waiting on Windows:** `dotnet test --project tests/Autopilot.Windows.Tests`
has been built but never run (it runs only on Windows). It checks the struct
layouts of `Signatures/NativeMethods`, the `.part` name and the signer read.
Not covered by a test: the cache-only chain build on a clean install and the
second `WinVerifyTrust` without revocation (net.md, "To verify").

Where things are:

- **The record** is `Install/InstallRecord`, read and written by
  `Install/InstallRecords`; which files are ours is `Install/OwnedFiles`.
  Entry states include `Held` and `Restored`; an open record carries the
  install it replaces as `Previous`; a committed one may carry `Releasing`
  (registrations and graphics preferences of the replaced install not yet
  released, retried by the next transaction without counting as a recovery).
- **The transaction** is `Install/InstallTransaction` (`RunAsync`,
  `RecoverAsync`, `CleanUpLayerFilesAsync`). A plan (`InstallPlan`: groups
  of `PlaceFile`, `MoveAside`, `CreateFolder`, `KeepEntry`, `PlacePattern`,
  `RegisterLayer`, `SetGraphicsPreference`) is resolved by `InstallResolver`
  into `ResolvedInstall`, which the transaction executes exactly. A previous
  entry the plan neither places, moves aside nor keeps is held and goes at
  commit.
- **Refusals and the preview** (f): `InstallRefusals.CheckAsync` gives an
  `InstallCheck` (blockers, warnings, the resolved plan);
  `InstallPreview.Of(check).Lines()` reads it. Each `InstallAction` states its
  preview lists and its `outside:` line. Route settings are typed
  (`Routes/RouteSettings`, one rule table in `ShownSettings`); route facts
  come from `RouteFacts.Of`.
- **Uninstall** (e) is `Install/Uninstaller`; with no usable record,
  `KnownNamesCleanup` (a damaged record kept as `.corrupt`). "Verify" is
  `InstallVerification.VerifyAsync`.
- **Signatures** (k): `ISignatureVerifier.CheckAsync` reports a
  `SignatureCheck` (status, signer, Windows' detail); `Net/Verifier` decides
  (`NotSigned`, `WrongSigner`, `SignatureInvalid`). The Windows
  implementation is `Autopilot.Windows/Signatures/AuthenticodeVerifier`; M7
  passes it to `DownloadCache` in `CompositionRoot`.
- **Outside the game folder** (i): `SharedPlaces` (HKCU registry, the
  machine's for reading, the layer folder) is null everywhere until M7 wires
  it in `CompositionRoot`, so a plan with layer or preference steps is
  refused today, by the preview and the transaction alike. Undo goes through
  `IOutsideWork.For(disk, installFolder)`; dependents come from the records
  the per-game state store names. M7 also calls `CleanUpLayerFilesAsync` at
  start, and must give `InstallRefusals.Shared` the same places as the
  transaction.

For g and h:

- **Patterns:** wire `GatheredRefusals` and expand `PlacePattern` steps into
  real files after the gather; the free-space check runs again then.
- **Refusals still to write:** a proxy name already taken (e's
  `ContentMarks` recognises ReShade, DXVK and REFramework by content); a
  route not offered for the game; the feeder's HDR minimum.
- **OptiScaler:** its first free proxy name must avoid the game's own
  frame-generation names.
- **Settings:** the bridge's ofa settings; `OverlayKey` is per game in the
  code, global in app.md: settle one.
- **h** owns the config-edit step (`rtx.conf` and the like): its undo slot is
  `IOutsideChanges.UndoConfigEditsAsync`, a no-op today.
- **j:** `ResolvedInstall.Steps()` does not predict the release steps at
  commit; the crash-point replay must account for them.
- The preview's held-layer-file refusal reuses `RefusalLocked`, whose
  "often the game's launcher" hint is off for a layer DLL.
- **g/h:** a plan names every previous entry it keeps with `KeepEntry`;
  anything it forgets is removed at commit.
- **g/h:** a `ComponentRelease` for an id with several catalog rows (`dlss`,
  `dlssd`, `dlssg`, `optiscaler`) must carry its row: `ComponentLayout` and
  `ReleaseAssets` look rows up by id, and `ComponentCatalog.Of(id)` refuses
  such ids.

## How we work (binding, besides AGENTS.md and the plan's Decisions)

- Replies to Ale in Italian, short; code, docs, commits in English.
  Commits as ale.burato@icloud.com, trailer
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Maximum autonomy; no polling; minor calls decided and reported.
- **Implementers by complexity, never below Sonnet high:** Sonnet high for
  small mechanical changes, Opus medium for moderate, well-specified
  sub-tasks, Opus high for complex ones. No blanket Sonnet default to save
  quota: on real logic its output ends up rewritten.
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
  (`git worktree add -b <branch> ../invidia-claritatis-<x> <commit>`); LSP does
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
