# Handoff: Invidia Claritatis during M4

Written 2026-11-27 at the end of the session that planned M4 and did its
sub-task a. Start the next session from this file.

## State

- Repository `domoarigatomrburato/DLSS5-Autopilot`, branch
  `rewrite/scaffold`, in sync with origin. `main` is the Python app
  (b84e39c).
- The Python reference is tag `python-reference`, checked out read-only beside
  the repository as `DLSS5-Autopilot-reference`.
- M0–M3 ticked in `docs/plans/rewrite.md`; M4 planned there as sub-tasks
  a–j, with **a done** (69bc2b9). Gate green: 2046 tests, 98.76% line
  coverage, floor 98 in `coverage-floor.txt`.
- **The tool is now Invidia Claritatis** (Decisions in `rewrite.md`); the
  specs use `invidia-claritatis` for every name the tool gives, and read the
  Python release's `dlss5-autopilot` names. The user agent, cache folder and
  window title are renamed in code (35e4ce9); `KnownFiles.InstallRecord` and
  `BackupSuffix` still hold the Python names, read by the M2 folder evidence,
  and move with sub-task c.
- The `nvngx_dlssnr.dll` digests behind `DlssnrPins` were measured in
  `~/coding/claude-lab/dlssnr-digests/results.md`.

## Next: M4 sub-tasks b1 and b2, in parallel

Plan and order in `rewrite.md` (a → b1∥b2 → c → d → e∥f∥i → g → h → j).
b1 (storage and archives) and b2 (platform seams, per-game state store, the
run, test beds) touch separate files: run them in hand-made worktrees, one
Opus implementer each, then review each diff on its own. The open questions
of `install.md` are settled (its "Open questions" section).

Carry into later sub-tasks:

- **c:** the record's and backups' names become `invidia-claritatis.json`
  and `.invidia-claritatis-backup`; the reader converts the Python release's
  `dlss5-autopilot.json` and keeps the backup names it recorded
  (`install.md`, "Records from the Python release"); `pe.md`'s folder
  evidence reads both through the reader.
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
