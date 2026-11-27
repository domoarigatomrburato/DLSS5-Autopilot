# Handoff: DLSS5 Autopilot after M2

Written 2026-11-27 at the end of the session that did the specs, M0, M1 and M2.
Start the next session from this file.

## State

- Repository `domoarigatomrburato/DLSS5-Autopilot`, checkout
  `/Users/ale/coding/DLSS5-Autopilot`, branch `rewrite/scaffold` at 6ed3b12,
  clean and in sync with origin. `main` is the Python app (b84e39c).
- The Python reference is tag `python-reference`, checked out read-only at
  `~/coding/DLSS5-Autopilot-reference`.
- M0, M1, M2 are ticked in `docs/plans/rewrite.md`. Gate green: 1339 tests,
  98.54% line coverage, floor 98 in `coverage-floor.txt`.
- No GitHub CI on the branch (no `.github/workflows`; first CI release is M7).
- No worktrees or extra branches left.

## Next: M3, Routes

Spec: `docs/spec/routes.md`, plus `components.md` for where each component
comes from. Plan line: which route a game gets and why, from what was read;
the components each route needs; Net gains a HEAD that reports a redirect
without following it and a 5xx body read on request (reshade.me).

M3 builds on M2's output: `GameEntry` (API decision, bitness, emulator
profile, anti-cheat and RE Engine findings in `Library/Protection`), and
M1's `NetClient` / `DownloadCache`. Like M2 it is all testable on macOS.

## How we work (binding, besides AGENTS.md and the plan's Decisions)

- Replies to Ale in Italian, short; code, docs, commits in English.
  Commits as ale.burato@icloud.com, trailer
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Maximum autonomy; no polling; minor calls decided and reported.
- Split a milestone into sub-tasks (M2 was M2a–M2i), one brief each in the
  scratchpad: context and spec sections to read, settled design, tests
  first, the stop sentence, checks (`dotnet test --project ...` while
  iterating, `dotnet run gate.cs -- fix` once at the end), report with the
  tree fingerprint. Review each diff, recompute the fingerprint, commit per
  sub-task.
- Independent sub-tasks may run in parallel in hand-made worktrees
  (`git worktree add -b <branch> ../DLSS5-Autopilot-<x> <commit>`); LSP does
  not answer there, use grep.
- End of milestone: `/simplify` (fixes gated), then `/code-review` at `high`,
  fixes, full gate once, commit, push, tick the plan.
- Internals are tested through `InternalsVisibleTo`; nothing becomes public
  for tests (project memory `no-public-api-for-tests.md`).
- Stores and similar knowledge: follow Playnite (MIT) where it applies;
  GPL code (GameFinder) may be read, never copied. Only MIT-compatible
  packages; the project stays MIT. Read-only clones of both, and the store
  formats research, are in `~/coding/dlss5-lab/`.

## Token budget (Ale's Pro weekly quota)

The session so far cost about 87M input-equivalent tokens, about 31% of the
weekly quota. M2 alone was about a fifth of a week. To spend less:

- one fresh session per milestone (this handoff instead of a long context);
- implementers on Opus, by the usual tiers (medium for moderate,
  well-specified sub-tasks; high for complex ones), never Sonnet: its output
  ends up rewritten, which costs more than it saved;
- smaller briefs, so each implementer runs fewer turns (the costliest ran
  11–48M each).

## Open, needing Windows (Ale's PC is coming)

The "To verify" sections of `docs/spec/net.md`, `pe.md` and `library.md`:
real store registry keys and files, the EA app key on a real `IS` file, the
WMI hardware values, emulator configs. They become checkable with the
Windows implementations in M7.

## Plan changes made during M2

- Cover art and the library's search, hiding and sorting moved to M7.
- M4 implements `IInstallRecords`; the graphics decision's reading of the
  record goes through it then.
- The atomic-write helper M4 asked for already exists
  (`Storage/AtomicFile`, M2i).
