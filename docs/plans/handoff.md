# Handoff: Invidia Claritatis during M4

Written 2026-11-27 at the end of the session that planned M4 and did its
sub-task a, updated after sub-task d. Start the next session from
this file.

## State

- Repository `domoarigatomrburato/invidia-claritatis` (renamed from
  `DLSS5-Autopilot`), checked out as `invidia-claritatis`, branch `main`, in
  sync with origin. The rewrite is `main`; the Python app's last commit is the
  tag `python-reference` (b84e39c).
- The Python reference is tag `python-reference`, checked out read-only beside
  the repository as `invidia-claritatis-reference`.
- M0–M3 ticked in `docs/plans/rewrite.md`; M4 planned there as sub-tasks
  a–j, with **a, b1, b2, c and d done**. Gate green: 2394 tests, 98.15%
  line coverage, floor 98 in `coverage-floor.txt`.
- **The tool is now Invidia Claritatis** (Decisions in `rewrite.md`); every
  name the tool gives is `invidia-claritatis`, in the specs and in the code.
- **Greenfield, never published:** nothing reads the reference's records,
  folders or settings, and no start-up cleanup of an old all-users layer
  exists. The specs dropped every compatibility rule of that kind.
- The `nvngx_dlssnr.dll` digests behind `DlssnrPins` were measured in
  `~/coding/claude-lab/dlssnr-digests/results.md`.

## Next: M4 sub-tasks e, f and i, in parallel

Plan and order in `rewrite.md` (a → b1∥b2 → c → d → e∥f∥i → g → h → j).
e, f and i build on d and are independent of each other: one implementer
each, in hand-made worktrees, reviewed and merged one at a time.

What b1–d left for the next sub-tasks:

- **The record** is `Install/InstallRecord`, read and written by
  `Install/InstallRecords`; which files are ours is `Install/OwnedFiles`;
  the failure note is `Install/FailureNotes`. Entry states now include
  `Held` (our file beside its path as `<name>.invidia-claritatis-held`
  until commit) and `Restored` (an original put back during the install,
  `Superseded` numbering its superseded name); an open record carries the
  install it replaces as `Previous`.
- **The transaction** is `Install/InstallTransaction` (`RunAsync`,
  `RecoverAsync` for the folders the tool knows at start). A plan
  (`InstallPlan`: groups of `PlaceFile`, `MoveAside`, `CreateFolder`,
  `KeepEntry`) is resolved by `InstallResolver` into `ResolvedInstall`
  (actions with their journal entries and `DiskChange`s), which the
  transaction executes exactly. A previous entry the plan neither places,
  moves aside nor keeps is held and goes at commit.
- **e:** uninstall reuses `EntryUndo` (one path's entries undone, by hash)
  and `InstallRollback`'s shape; a record left `Removing` may still carry
  `Previous` (a rollback that stopped) or `Stuck`/`RestoreFailed` placed
  entries and un-undone `Held`/`Restored` ones. The `.invidia-claritatis-superseded`
  report line of uninstall's table is not produced yet (`UndoResult.Superseded`).
- **f:** the preview calls `InstallResolver.ResolveAsync` and shows the
  `ResolvedInstall`; refusals run before `InstallTransaction.RunAsync`
  (which itself only refuses unreadable records, records outside the game
  folder, paths outside it and plan conflicts). An unwritable folder is
  today reported as `Locked` (access denied counts as held, since Windows
  reports a delete of a loaded DLL that way): f's access refusal must catch
  it first.
- **i:** registry and config steps are new `PlanStep`/`InstallAction` kinds;
  `PriorInstall.Opening` carries the previous install's registrations,
  graphics preferences and config edits over unchanged today.
- **d/e check** the record's derived game folder against the plan's
  (`InstallChecks.RecordRefusal`); e must do the same.
- **Test beds** in `tests/Autopilot.Core.Tests/TestBeds/` (`GameTree`,
  `GameFolders`, `TreeSnapshot`, `CrashPoints`, `DiskFullPoints`,
  `PlatformFakes` with `FakeFreeSpace`) and
  `tests/Autopilot.Core.Tests/Install/` (`TransactionBed` with `Clone`,
  `HeldPaths`, `BothSteps`; `Switches` with `AssertBackToX`/`AssertY`).
- **ReShade's setup:** the real 6.8.0 addon setup opens with the extractor
  (relative offsets, the signature after the zip, six members including
  `ReShade32_XR.json` and `ReShade64_XR.json`); notes in
  `~/coding/claude-lab/reshade-setup-shape/results.md`.

Carry into later sub-tasks:

- **g/h:** a plan names every previous entry it keeps with `KeepEntry`;
  anything it forgets is removed at commit.

- **g/h:** a `ComponentRelease` for an id with several catalog rows (`dlss`,
  `dlssd`, `dlssg`, `optiscaler`) must carry its row: `ComponentLayout` and
  `ReleaseAssets` look rows up by id, and `ComponentCatalog.Of(id)` refuses
  such ids.
- **h, wiring `DlssnrBuilds.ChooseAsync`:** on Auto, skip a pinned build
  whose `DlssnrPin.Sm` excludes the card before downloading it (165–276 MB
  each); today the fatbin is read after the download.
- **Open, ask Ale before g:** the Windows Authenticode verifier behind
  `ISignatureVerifier` is planned for M5, but M4's route plans already place
  files checked by NVIDIA's signer (`dlss`, `dlssd`, `dlssg`, NVIDIA's own
  `dlssnr` builds). Either it moves into M4, or M4's installs stay
  test-only until M5 (real games are checked on Windows in M7 anyway).

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
