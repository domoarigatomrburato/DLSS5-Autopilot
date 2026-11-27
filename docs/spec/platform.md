# Platform: what every area shares

The concerns every area meets, stated once: where settings and per-game
state live and how they are written, what identifies a game, logging, the
single instance, elevation, time, cancellation, jobs and text. Other specs
point here instead of restating these rules. Read from the reference's
`core/prefs.py`, `core/log.py`, `core/ui/app.py` and the install, watch and
app specs, where these rules were first written.

## What the user gets

### Settings and the per-game state store

Two stores, both under `%LOCALAPPDATA%\invidia-claritatis\`:

| Store | File | Holds |
| --- | --- | --- |
| settings | `settings.json` | the person's preferences: what the app and the CLI read as options. The app spec lists the keys |
| per-game state | `games\<key>.json`, one file per game (`<key>` below) | what the tool learned about one game: the last verdicts (`last_verdicts`), the autopilot plan (`autopilot_plans`), the "aim for" history, the install's failure note ([install](install.md)) |

- **Changed from the reference:** `last_verdicts`, `autopilot_plans` and
  the "aim for" history lived in `settings.json`, read and written whole on
  every diagnosis, keyed by the string of a path. They are per-game state
  now, so one game's write never rewrites another's, and settings stay
  small.
- Each per-game file names its folder, so the store can list every folder
  the tool knows ([install](install.md) finds a registration's dependents
  this way).
- **A schema version** in every file, an integer. A file whose version is
  newer than the tool knows is read where it can be and never written,
  with a notice, so an older build cannot drop a newer one's data. Unknown
  keys are kept on write.
- **One writer.** Every write goes through one store service in the process
  and, across processes (the app and the CLI), under a named mutex per
  file, re-reading inside the lock. A read-modify-write is never done
  outside it.
- **Atomic writes**, for these stores, the install record, the layer
  manifests and every config the tool writes whole:
  1. write the full content to a uniquely named temp file in the same folder
     (process id plus a random part);
  2. flush the data to the disk (`FlushFileBuffers`) **before** the rename;
  3. replace the target with a write-through rename, retried a few times
     (100 ms apart, up to 6 tries) when an antivirus or indexer holds the
     old file;
  4. on failure remove the temp file and leave the old target whole. Never
     fall back to writing the target in place.

  Without the flush, a crash after the rename can leave an empty file under
  the target's name.
- **A missing file is empty defaults.**
- **A value of the wrong type** (a hand-typed `"F10"` where a key code is
  expected) reads as its default, per key, and never stops the window
  opening or an install.
- **Unreadable is not empty.**
  - A file that cannot be read for a moment (locked by an antivirus or
    another process) is retried briefly; if it stays so, the last good copy
    in memory is used, and **the file is not written**: a write made from an
    empty read would drop everything.
  - A file that does not parse, or parses to something that is not an
    object, is **corrupt**: it is moved aside to `<name>.corrupt` (the newest
    kept, never deleted by the tool), the tool starts from defaults and says
    so once. Nothing safety-relevant lives in these stores: install records,
    registrations and the graphics-preference records are elsewhere.
- **A write that hits a full drive** keeps the value in memory and shows a
  notice once, instead of dropping it silently.
- **A full uninstall** drops the game's per-game state; one that left
  something behind keeps it, with the failure note.

### Game identity

A game is its **install folder** (the folder holding the executable the
tool installs beside), normalised:

- the final path, after junctions, symbolic links and 8.3 names are
  resolved;
- `\` separators, no trailing separator;
- compared ordinal, ignoring case.

The per-game state key is the first 32 hex digits of the SHA-256 of that
path, upper-cased invariantly. Not a store id, because one game can be in
two stores (Steam and a folder added by hand), and an id from one store says
nothing about the other. A game moved to another folder is a new game: its
install record moves with it, its learned state does not.

### Logging

- `Microsoft.Extensions.Logging`, with Serilog's file sink
  (`Serilog.Sinks.File` through `Serilog.Extensions.Logging`) in shared mode,
  so the app and the CLI can write one log at once.
- The file is `%LOCALAPPDATA%\invidia-claritatis\autopilot.log`. It rolls at
  1 MiB and the newest 3 files are kept.
  - Changed from the reference: it trimmed the one file in place to its
    newest half (`log.py:63-70`), unsafe with two copies of the tool
    running.
- An entry carries a level and a section as data, not as a text prefix, so
  the app's drawer, the bug report and the zip can each filter them.
- Only paths and versions, never anything the person typed.
- A failure to log is swallowed: logging is never the thing that breaks the
  tool.
- Every unhandled exception is logged with its stack and a label saying what
  was being done, from background threads too. The last one is kept for the
  bug report.

### Single instance

- The app takes a named mutex for the Windows user session at start. A
  second launch finds it held, sends "activate" over a named pipe, and
  exits; the first copy brings its window to the front (restoring it if
  minimised, or showing it from the tray).
- If the first copy does not answer on the pipe within 3 s (still starting,
  or stuck), the second opens normally. The job lock below still keeps two
  copies from writing at once.
- **The CLI is a separate executable** and does not take the instance
  mutex: it runs beside the app. It takes the same **job lock** (a named
  mutex) around every write, so an install from the CLI waits while the app
  runs a job, saying "waiting for the app to finish <job>", and the reverse.

### Elevation

- The app and the CLI run unelevated: the manifest asks for the invoker's
  rights.
- A tool the person started as administrator does what it can do the same
  way, and refuses what would need de-elevation, saying why:
  - it never starts a game ([watch](watch.md)): a game started from an
    elevated process is elevated and reads no per-user layer;
  - it never registers anything for all users.
- Nothing the tool does needs elevation.

### Time

`TimeProvider` is injected wherever a duration or an age decides anything:

- the 6 h freshness of API answers, the feed and the self-update check;
- GitHub's 1 h rate-limit window;
- the watcher's 4 s poll, 8 s settle and 20 s minimum play;
- the cover-art retry ages (7 days, 1 day) and the 24 h staging cleanup;
- every cache age.

Tests advance a fake clock; no test sleeps to pass a window.

### Cancellation

- Every public asynchronous API takes a `CancellationToken`, last.
- Cancellation surfaces as `OperationCanceledException`. It is never
  caught as a failure, never retried and never logged as an error.
- A cancelled write job rolls back ([install](install.md)), and its result
  says "cancelled".
- Each job owns one cancellation source. Closing the app, the job's cancel
  button and Ctrl+C in the CLI all cancel through it.
- `CancellationToken.None` appears only at a process's root.

### Jobs

- **One job at a time in v1.** A job is anything that writes: an install,
  an uninstall, a DLSS update or restore, update all, an autopilot pass, a
  Remix mod fetch or removal, the self-update download.
- Further requests are **queued** in order, visibly: the status line says
  "waiting: <job> on <game>", and a queued job can be removed from the
  queue. The same game is never queued twice for the same job.
- Read-only work (scans, reads, covers, previews) runs beside jobs.
- **Closing during a job** cancels it and rolls it back. The window says
  "stopping, putting the folder back" and closes when the rollback ends, at
  most 10 s after the request. If the rollback has not ended by then, the
  process exits anyway: the install record's journal makes the next start
  finish it.

### Text

- English only in v1.
- Every user-facing text lives in resources keyed by id, so a translation
  needs no code change.
- UI text is in sentence case ([app](app.md)).

## What must never happen

- A store written from an empty read, written in place, or written by two
  writers at once.
- A newer schema's file rewritten by an older build.
- Per-game state keyed by anything but the normalised install folder.
- Two jobs writing at once, in one process or across the app and the CLI.
- A window close that kills a job without cancelling and recording first.
- A clock read directly where a window or an age decides something.
- A message the person reads built in code instead of taken from resources.

## Bugs in the reference not to port

- The settings lock was in-process only (`prefs.py:36-40`): the app and the
  CLI lost each other's writes.
- The settings write fell back to an in-place write after six failed renames
  (`prefs.py:98-102`), and the atomic writes had no `fsync`.
- A failed settings write was silent (`prefs.py:104-105`).
- Learned per-game data in `settings.json`, keyed by `str(path)`.
- The log trimmed itself in place (`log.py:63-70`).
- Durations read from the wall clock in place, so every window was tested by
  waiting.

## To verify against primary sources

| Claim | Source |
| --- | --- |
| What `MoveFileEx` with `MOVEFILE_WRITE_THROUGH`, `ReplaceFile` and `File.Replace` guarantee on NTFS after a power loss, and that `FlushFileBuffers` on the temp file before the rename is enough | Microsoft's file system docs; .NET `File` API remarks |
| A named mutex and a named pipe per Windows session (`Local\`) behave as above for two copies started by the same user, and across fast user switching | Microsoft docs for `CreateMutex` and named pipes |
| `Serilog.Sinks.File` shared mode with size rolling is safe with two processes | Serilog file sink docs |
| `TokenElevation` reads elevation correctly on a UAC-off machine | Microsoft docs for `GetTokenInformation` |

## Fixtures

- **Stores**: an empty file, a truncated JSON, a JSON array, a wrong-typed
  value, a newer schema version, a file locked by another handle, a full
  drive at the write.
- **Two writers**: two test processes (or two store instances sharing the
  mutex name) writing different keys at once; both keys survive.
- **Game identity**: one folder spelled through a junction, an 8.3 name,
  a trailing slash and another case; all give one key.
- **A fake `TimeProvider`** for every window above.
- **Jobs**: a queue of three requests, one removed; a close during each
  phase of an install, with a rollback that ends in time and one that does
  not.

## Open questions

- None open.
