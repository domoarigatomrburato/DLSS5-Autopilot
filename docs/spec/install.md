# Install and uninstall

What happens to a game folder, and to the few places outside it, when the tool
sets a game up, previews that, removes it again, or moves it to another route.
This spec covers the mechanics: the record, backups, refusals, rollback,
switching, shared registrations, the Windows graphics preference, config
edits, disk-full handling, settings and atomic writes. Which files each route
places, and why, is the routes spec; where the bytes come from is the net
spec. Read from the reference's `core/installer.py` (all 5,936 lines),
`core/prefs.py`, `core/gpupref.py`, `core/vulkan.py`, `core/openxr.py`,
`core/emulators.py`, `core/remix.py` (the one-line edit), `core/log.py`, the
README sections "Using it" and "Is it safe", and `SECURITY.md`. Evidence is
cited as `file:line` in the reference.

The reference grew its mechanics route by route, so every one of them was
re-implemented several times (backups in five places, the preview as a hand
copy of the install). This spec states each rule once and says where the
reference departs from it. Where a clearly better behaviour exists it is
specified and marked **Changed from the reference:** with one line of why.
Anything uncertain is under "Open questions", not stated as a decision.

## What the user gets

### The folders involved

- **The install folder** is the folder holding the game's executable. It may
  be a subfolder of the **game folder** (Unreal and CryEngine games run from
  `Binaries\Win64` while the launcher stub sits in the root). One install
  record per install folder.
- A few things a route needs live elsewhere inside the game folder: a
  runtime the game keeps under `Plugins\...` (a swapped `nvngx_dlss.dll`), an
  RTX Remix mod's `.trex` folder and its `rtx.conf`. Those are written
  through the same rules. The boundary for anything the tool may touch is the
  **game folder**, never the drive.
- Outside the game folder the tool touches only what this spec lists:
  the tool's own data folder, the shared layer registrations, the Windows
  graphics preference, and an emulator's own config file.
- The tool's data folder is `%LOCALAPPDATA%\dlss5-autopilot`
  (`prefs.py:22-26`, `log.py:26`, `vulkan.py:61-64`): `settings.json`,
  `autopilot.log`, the download cache (net spec), the per-user layer copy in
  `reshade-vulkan\`, and the per-folder failure notes (below).

### The install record

A JSON file named `dlss5-autopilot.json` in the install folder
(`installer.py:90`). It is the only thing uninstall trusts about what was
done. It is written before the first change, kept current through the
install, and deleted when the last thing it names is gone.

**Contents.** Format and names are the C# implementation's to fix; the
contents are:

| Part | Holds | Reference |
| --- | --- | --- |
| format version | an integer; a newer one than the tool knows is never edited (below) | `"version": 1`, `installer.py:3165` |
| tool version | which build wrote it | `:3170` |
| state | `installing`, `installed`, `removing`; see the lifecycle | reference had only a boolean `complete`, `:3171` |
| game facts | executable name, bitness, API, kind (store/emulator/...) | `:3172-3175`, `:3197` |
| route and choices | the route and every choice needed to repeat the install with newer versions (provider, proxy name, OptiScaler build, DXVK, VR, MFG, keep-game-DLSS, feeder pin, helper mode, own frame-generation set, upscaler, dials). The route specs own the exact list. | `:3176-3195`, replayed by `options_from_manifest` `:3263-3342` |
| components | component name to installed version, for "what has moved on" | `:3196` |
| entries | one per path the install touches, see below | reference had a flat list of strings, `:3180` |
| config edits | key-level edits to files the user owns (rtx.conf key, emulator backend key) | `:3199`, `emulators.py:776` |
| registrations | shared registrations this install relies on or created | new, see "Shared registrations" |
| graphics preference | the records of values written under the Windows graphics key | `:3211-3212` |
| created folders | folders the install created | new |
| notes, warnings, skipped | what the install said, kept for the report and the diagnosis | `:3181-3183` |

**One entry per path**, structured (**Changed from the reference:** the
reference mixed written files and backups in one list of strings and told
them apart by file-name suffix, which misclassifies; `installer.py:2976`,
`:5616`):

| Field | Meaning |
| --- | --- |
| path | relative to the game folder, `/` separators, spelled as on disk. Compared ignoring case and with both slashes read as separators on every host (the net spec's rule). |
| class | `component` (a binary the tool placed; expected to stay as written) or `config` (a file its components rewrite while running: `ReShade.ini`, `ReShadePreset.ini`, `dlss5-feed.cfg`, `dlss5-bridge.cfg`, `OptiScaler.ini`, `host64/ReShade.ini`, an ASI loader's `.ini`). |
| state | `pending` (named, maybe not yet written), `written`, `moved-aside`, `restore-failed`, `stuck` |
| written | SHA-256 and size of what the tool wrote (known before the write: the file was downloaded and verified first) |
| original | present only when the path held a file the tool did not write: SHA-256, size, attributes and modified time of that file, and the name its backup has |

Hashes are lowercase hex SHA-256. The reference recorded no hash anywhere.

**Lifecycle.**

| State | Meaning | Written when |
| --- | --- | --- |
| (no record) | nothing of ours is here | initial; also after a full uninstall or a rollback |
| `installing` | a transaction is open; entries are `pending` or `written` | before the first change to the folder |
| `installed` | the install committed | the last step of a successful install |
| `removing` | an uninstall is part way, or ended with leftovers | before the first deletion; kept only if something is left |

A record found in `installing` or `removing` at the start of any later
operation on that folder means the previous run ended abnormally (power
loss, killed process, forced close). The tool says so and completes the
rollback or the removal from the record before doing anything else. It never
installs over a half-finished transaction.

**Reading it.**

- A record whose format version is newer than the tool knows is refused for
  install and uninstall, with "this folder was set up by a newer version;
  update the tool". Editing a format the tool does not understand is how
  files get orphaned.
- A record that cannot be parsed, or is the wrong shape, is **corrupt** and
  is treated as no record: see "Uninstall without a record".
- Entries naming a path that resolves outside the game folder are ignored
  with a line in the log, never followed (`installer.py:5530-5567`, SECURITY.md
  first bullet).
- **Records from the Python release** (`"version": 1`, flat `files` list,
  backups told by suffix) are read and uninstalled by the new tool, with no
  hashes: the hash rules below fall back to "ours by name", exactly as the
  reference decided. See open question 1 for the older name sets
  (`dlss5kur-kurulum.json`, `dlss5-installer.json`, Turkish keys,
  `installer.py:346-350`).
- **Two executables in one folder** (a game and its expansion launcher)
  share one record. Installing for the second one is a reinstall of the same
  folder, and the record names both executables; the report says "uninstalling
  either removes the files for both" (`installer.py:3642-3652`).

### Order of an install

Every install runs these phases in order. A refusal in any phase before 3
leaves the game folder, the registry and every config file untouched.

1. **Refuse.** Every check that can say no, read-only. Includes every
   route-specific refusal.
2. **Gather.** Resolve versions, download, verify, and inspect everything the
   install will place, into the download cache only. The sum of bytes to
   write is now exact.
   **Changed from the reference:** the reference interleaved downloads with
   writes (ReShade written, then the add-on download fails, then rollback).
   A network failure now happens before the first write, so most failures
   touch nothing. Report 175 shows the shape: `dxgi.dll` present and
   everything else missing after a certificate failure
   (`_tools/reports/175.txt`, install 1.8.1).
3. **Journal.** Save the record in state `installing`, listing every path the
   next group of writes will touch as `pending`, with the hash of each
   original. Atomic and durable (see "Atomic writes"). **A write begins only
   after the record that names it is on disk.**
4. **Apply.** Per group (one component, or one route-specific step): back up
   originals, write, update the entries to `written`, save the record. Shared
   registrations, the graphics preference and config edits are journaled the
   same way before they are changed (the record names the registry value and
   what it held, the config key and its original text).
5. **Verify and commit.** Check every written file is there with its size and
   hash (see "Quarantined files"), set state `installed`, save. Only then are
   the files held back during a switch deleted (see "Switching routes").

Progress is reported as (step index, step count, step name) and a percentage;
the step names are the route specs'. The log lines are for people; the report
carries notes and warnings.

### Refusal checks

All run in phase 1, before anything is moved, backed up, deleted or written,
and again where noted. The reference ran some of them after it had already
moved or deleted files (`_clear_stale_reshade` at `installer.py:3751` and the
hand-installed OptiScaler move at `:3717-3727` run before the route refusals
at `:3759-3843`; the Remix refusals at `:3954-3968` run after DXVK and
REFramework are written).

| Check | Refuses when | Reference |
| --- | --- | --- |
| executable known | no executable, or detection errored | `installer.py:761-766` |
| architecture and API readable | a protected executable whose bitness or API was not set by hand: "choose this game's architecture and graphics api in the game's settings - Windows protects its executable" | `:767-772` |
| bitness known | "Could not read the architecture." | `:773-774` |
| DirectX 8 only 32-bit | a 64-bit executable set to DX8 | `:775-782` |
| Remix only on the Remix route | a `.trex` runtime is in the folder and the route is not Remix; the message says why (ReShade crashes a Remix game before it draws; on DX9 it would overwrite the runtime) | `:3588-3596` |
| a card DLSS can run on | no NVIDIA card any route can use, unless "ignore GPU mismatch" is set | `:3599-3605` |
| install folder exists | "`<folder>` does not exist." | `:3461-3462` |
| install folder writable | a probe file created and deleted; permission denied is worded for Xbox/Game Pass folders (the store owns them; elevation does not help) and for ordinary folders ("close the game, or run as administrator"); disk full is worded as disk full | `:3464-3489` |
| game not running | a process whose executable image lies inside the install or game folder is running. Asked in phase 1 and again just before phase 3, since gathering can take minutes. | `:3491-3496` |
| nothing locked | each existing file the plan will overwrite, move or delete can be opened for writing; a locked one (a launcher holding the proxy DLL) is named now | new |
| a non-ReShade file under the proxy name | refused ("`dxgi.dll` already exists but is not ReShade; DXVK, Special K or another injector? Remove it first"), except our own file from the recorded previous route and a hand-installed OptiScaler (backed up and moved, by the rules below) | `:3705-3737` |
| the record is usable | newer format refused; corrupt record refused for install with "this folder's install record is damaged: uninstall first, then install" | new, see open question 2 |
| free space | the game's drive has room for every byte to write, plus temporary copies, plus any backups made by copy, plus a margin for the record | new |
| route-specific refusals | each route spec lists its own; they all run here. From the reference: a pinned feeder older than the D3D10 relay, the 64-bit helper mode and the built-in motion vectors (`:3759-3813`); GITC-Uplift on Vulkan/OpenGL needs ReShade 6.8 (`:3815-3830`); the RTX 40 MFG package on another card, the person's own frame-generation slot taken (`:3832-3843`); Remix with no runtime, or a runtime without the neural pass and no swap chosen (`:3953-3968`); a local add-on file missing or not 64-bit (`:4686-4696`) | |

Not refusals, stated in the preview and the report (route and diagnosis
specs own the wording): launcher-like executable, other NGX hooks in the
folder, anti-cheat, RE Engine, unmet D3D12 Agility SDK, the route's
reliability level (`installer.py:3636-3697`, `:2024-2141`).

An error message names the cause and ends with what to do, in plain words;
it never carries a traceback. A refusal says "nothing was overwritten or
deleted" when that is true and the check ran after a partial step could have
(it cannot now).

### The "what will happen?" preview

Pressing the question mark, or running with `--check`, shows what an install
would do to this folder as it is now. It writes nothing: no file in the game
folder, no probe file, no registry value, no cache file, no settings, no
network request.

- **One plan, two readers.** The preview and the install are the same plan
  list, one printed and one executed.
  **Changed from the reference:** the preview was a ~700-line hand copy of
  the install (`installer.py:2012-2693`), and its comments record the drift:
  "the preview promised a Program Files copy the install skipped"
  (`vulkan.py:378-403`, gate 2.0.9), "described a plan the install then turned
  down" (`:2055-2057`). A test installs every fixture folder on every route
  and asserts the set of files written, backed up and removed equals the
  preview's, except for entries the preview marks as patterns.
- **Content.** Seven lists: steps; files written (created or overwritten);
  files that will be backed up; things cleaned up first (previous route's
  files, second ReShade copies, other routes' add-ons, the sidelined
  compiler); effects outside the folder; blockers (anything that would make
  the install refuse); warnings.
- **Exact where knowable, pattern otherwise.** A file list that only a
  download reveals (shader packs, the OptiScaler package) is read from the
  cache when an earlier install left that archive there, and given as a
  pattern (`lumenite_*.fxh`, "the rest of the package") otherwise. A 7z
  archive cannot be listed and is always a pattern (`installer.py:1932-1945`,
  `:2339-2350`). The preview says which lines are patterns.
- **Preview lines**, in this order, as the window and the log show them
  (`installer.py:2696-2719`):

  | Line | When |
  | --- | --- |
  | `cannot install: <blocker>` (and nothing else follows) | any blocker |
  | `warning: <text>` | each warning |
  | `will clean up first: a, b, ...` | cleanups |
  | `will write N file(s) (first three, ...)` | always |
  | `will back up: a, b, ...` or `nothing of yours is overwritten - no backups needed` | always one of the two |
  | `outside: <text>` per effect, or `nothing is written outside this folder` | always |

- **Effects outside the folder** are always listed, each with its scope and
  how it is undone: the Vulkan layer ("loads into EVERY Vulkan application
  until 'uninstall' removes it"; reused when one exists; also registered for
  all users when the tool runs as administrator, with the Program Files path
  and that taking it off needs administrator rights); the OpenXR layer; the
  Windows graphics setting (only on a machine with an NVIDIA card and
  another GPU, and only where the person has not already chosen); an
  emulator's config file and the key; a swapped runtime outside the install
  folder ("backed up beside itself"); the Remix `rtx.conf` line ("this one
  line, nothing else in the file is touched").
- The preview reports the game-running blocker and the writability blocker
  from read-only queries (an access check, a process list), never by probing.

### Backups of the game's own files

- **What is the game's own.** Any file at a path the tool is about to
  overwrite, move or delete that the tool did not write. Ownership is
  decided by the record **and the hash**: a path is ours when the record
  lists it as written and its current content hashes to what was written
  (for a `config` class file, when the record lists it).
  **Changed from the reference:** ownership was decided by name alone
  (`installer.py:2976`, `:3740`). A game update that replaced our
  `dxgi.dll` with its own was then overwritten without a backup on the next
  install and deleted by the next uninstall.
- **Where.** Beside the file, original name plus `.dlss5-autopilot-backup`
  (`installer.py:307`). The backup is made before the first change to the
  file and is never overwritten by a later install.
- **Exactly the bytes.** A backup is the original's bytes, attributes
  (read-only, hidden) and modified time, and its hash must equal the hash
  taken from the original before the copy. At every instant of an install,
  the original's bytes exist under its own name or under its backup name.
  Whether the tool copies or renames is its own choice
  (**Changed from the reference:** it always copied, doubling the space for
  a 200 MB runtime; a rename is atomic and free on one volume).
- **A backup is valid only if the record says so.** A file with the backup
  suffix that the record does not list, or whose hash is not the recorded
  original, is not trusted: a copy cut short by a crash looked like a
  finished backup to the reference ("a copy cut short is removed first, or
  the next install took it for the backup", `refw.py:77`, `mfg.py:432`,
  `installer.py:860-866`).
- **Backup exists, file is ours:** keep the backup, write over our file.
  **Backup exists, file is not ours** (the game updated it since): the old
  backup is moved to `.dlss5-autopilot-superseded`, a name no restore ever
  touches, and the current file becomes the new original with a new backup.
  The report says so. (The reference had this case only for the Remix runtime,
  `installer.py:3984-4020`.)
- **A backup that cannot be made refuses the write.** Nothing is overwritten
  or deleted. The sentence (from the reference, kept): "`<name>` could not be
  backed up (`<reason>`), so it was left as it is. Free some space on the
  drive, or close whatever has the file open, and install again"
  (`installer.py:870-883`).
- **Restore** is byte for byte: the backup's bytes replace the path atomically
  (temp copy beside the target, flushed, renamed over it), attributes and
  modified time included, and only then is the backup deleted. If the
  replacement fails, the backup stays, stays on the record in state
  `restore-failed` with the reason, and the uninstall is reported incomplete
  (see "Uninstall").
- **Byte-for-byte survival.** After any sequence of installs, updates, route
  switches, failed installs and uninstalls, every file that existed before
  the first install is identical in content, attributes and modified time,
  except files the game or its store changed in the meantime. This is the
  headline test of the area.
- **One mechanism.** Backup logic exists in five places in the reference
  (`installer.py:791-867`, `dxvk.py:153-193`, `refw.py:72-78`,
  `mfg.py:428-434` and `:499`, `optiscaler.py:724-727`), each with its own
  repairs. Every component uses the one backup step.

Suffixes, all in the game's own folder, and what each means:

| Suffix | Meaning | Restored by uninstall? |
| --- | --- | --- |
| `.dlss5-autopilot-backup` | the game's own file the tool replaced or removed | yes |
| `.dlss5-off` | the game's `d3dcompiler_47.dll`, renamed so Windows' newer one loads (the neural pass compiles for `cs_5_1`; route spec) (`installer.py:318-319`) | yes, as a backup (same rules) |
| `.dlss5-autopilot-superseded` | an old original replaced by a newer one the game shipped | never; left for the person, named in the report |
| `.dlss5-dlss-original` | the DLSS page's own backup of a runtime it updated (DLSS-files spec) | never touched by this spec; see open question 6 |

**Changed from the reference:** the `.dlss5-autopilot-orphan` suffix
(`installer.py:323`, `:3036-3041`, `:3081-3086`) is dropped. It held our own
add-ons from another route "moved aside" only to be deleted by uninstall, and
its entry was recorded by file name alone (`rep.written.append(aside.name)`),
wrong for any path not in the folder root. Our own files of a previous route
are held until commit and then deleted (see "Switching routes").

### Writing a file

For each file the install places:

1. The source is the verified cache file, or generated content.
2. Written to a temporary name in the same folder, flushed to disk, its hash
   checked against the recorded one.
3. Renamed over the target. A target held open by another program is retried
   a few times with short waits (antivirus and indexers hold files for a
   moment); if it stays locked the install fails with the "Windows refused to
   write a file" message below, never with a half-written target.
4. The entry becomes `written`, and the record is saved at the group's end.

Rules:

- A target is never opened for writing in place. The reference extracted zip
  members straight into the destination (`net.py extract_one`, opening
  `dest` for write) and wrote the shader headers with a plain write
  (`installer.py:2736`, `:4625`), so a failure left a truncated runtime where
  the game expected a whole one. Only plain DLL copies were atomic
  (`:1529-1539`).
- Downloads that must be a 64-bit Windows binary are checked before they can
  replace anything: at least 200,000 bytes, `MZ` header, `PE\0\0` signature at
  the offset in the header, machine type x64. A failed check deletes the
  cached file so it is not served again and refuses with "`<name>` was not
  installed: `<why>`. That file was not written and the download was thrown
  away. Try again" (`installer.py:1100-1132`, `:1512-1525`).
- If the first source of a component cannot be downloaded the next one in its
  family is tried and the log says so (`installer.py:1448-1499`). The family
  order and the sources are the net and routes specs'.
- Read-only attributes on a target the tool owns are cleared before a delete
  or replace (`installer.py:5928-5936`).
- Preview and install write no other temp names than the pattern the tool
  owns; leftovers of that pattern from a crashed run are removed at the next
  operation on the folder.

### Rollback of a partial install

When an install stops (a locked file, a full drive, a cancelled run, an
unexpected error) after phase 3 began, everything it did is undone from the
record and the folder is as it was before the install: byte for byte, by the
survival rule.

**Changed from the reference:** the reference rolled back only a *fresh*
install (one with no earlier record), and only by running the full uninstall.
An update or a route switch that failed kept "what was written so far" and
said "install it again to return" (`installer.py:1191-1289`), leaving a mix of
old and new files. Here every failed install, fresh or not, returns the
folder to its state at the start, because the files it replaced are kept
until commit (see "Switching routes") and the journal says what changed.

- Rollback reverses the journal: files written are removed or replaced by the
  kept originals and previous-route files; shared registrations this install
  created are removed unless another install depends on them; the graphics
  preference and config edits are put back by their recorded originals.
- Rollback is idempotent and resumable: it runs from the record, so a second
  run after a crash during the first finishes the job.
- A file that cannot be removed because it is locked stays on the record in
  state `stuck`, the record stays in state `removing`, and the message says
  "Part of what it wrote could not be removed - a file is in use. Close the
  game and press 'uninstall'" (`installer.py:1253-1258`).
- **The result card** says what became of the folder, then the cause, in this
  order, because the cause is the line callers keep. The opening sentences:

  | Situation | Opening sentence |
  | --- | --- |
  | nothing had been written | "Nothing was written into the game folder." |
  | rolled back completely | "Nothing of it was left in the game folder: what it had written was taken back out, so the game runs as it did before." |
  | rollback incomplete | the "Part of what it wrote could not be removed" sentence above |

  Where a one-line reason is needed (the autopilot, "update all"), the cause
  paragraph is taken, not the opening sentence (`installer.py:1377-1389`).
- **A failure leaves no file in the game folder.** The reason is kept in the
  tool's own data folder, keyed by the folder's path, for an hour, so the
  diagnosis can say why this folder's install stopped ("the drive ran out of
  space", the install's own refusal text up to 400 characters, the
  traceback's last 4,000 characters). **Changed from the reference:** it
  left a record in the game folder holding only a "rolled back" note
  (`installer.py:1275-1280`, `net.py:54`) and kept the traceback in memory
  (`:1135-1188`), so a rolled-back folder still had a file of ours in it and
  the reason was lost on restart.
- The message for a refused write ("Windows refused to write a file"): the
  rollback sentence, then the Windows error, then "Almost always this means
  the game (or its launcher) is running and holding the file open. Close it
  and run the install again" (`installer.py:5259-5269`). A rate-limited or
  unavailable publisher ends with the net spec's sentence, last.

**No-record end states disappear.** The reference wrote its record last, so a
full drive at that moment, or a closed window, left a fully set-up folder
with no record, and the person was told to delete files by hand
(`installer.py:1292-1355`, `:5382-5384`). Because the record is written
first, those texts are not needed.

### Reinstall and update

Installing again with the same route (an "update", "update all", a new
choice) replaces our files and keeps what was recorded:

- Components that the install does not touch keep their entries, hashes and
  backups. A file written the first time and merely left alone the second
  (a kept `nvngx_dlss.dll`) must stay on the record, or no uninstall could
  remove it (`installer.py:3131-3142`).
- The original behind a file we replaced is never re-backed-up as the game's:
  the existing backup stays on the entry (`installer.py:797-830`, #148).
- An update is the same choices with every component at its newest version.
  A failure rolls back to the previous working install, not to nothing.
- Frame-generation and MFG files present on the record but switched off now
  are removed, and the original behind the loader name returns
  (`installer.py:2367-2376`, `:2615-2644`, `:5049-5051`). The route specs own
  the file lists.

### Switching routes

Installing route Y over a recorded route X (or switching the feeder between
in-process and helper mode, `installer.py:271-281`) leaves nothing of X.

- **Everything X recorded goes**, including its shared registrations (when no
  other install needs them), its config edits, its graphics-preference
  records and its runtime artifacts. The new record names only Y.
- **Never leave a second route's add-on loaded.** ReShade loads every
  `.addon64`, `.addon32` and `.addon` in the folder, and two NGX hooks fight.
  An add-on of another route found in the folder, recorded or not, is taken
  out of ReShade's reach: ours is deleted at commit, an unrecorded one is
  backed up (restored by uninstall, so the person's own add-on returns). Same
  for a ReShade proxy under another name than the one chosen (a second copy
  aborts itself and the game may not start), and every ReShade proxy on the
  Remix route (`installer.py:2989-3102`, `:3012-3055`). A `d3d9.dll` in a
  Remix game is the Remix runtime, never mistaken for ReShade
  (`:3032-3033`).
- **Held until commit.** **Changed from the reference:** the reference ran
  X's whole uninstall first and irrevocably (`installer.py:3845-3861`), so a
  failure in Y's install left neither, and said "the previous X install stays
  removed" (`:1281-1289`). Here the files of X that Y does not overwrite are
  renamed into a recorded holding place inside the install folder, the files
  Y overwrites are held as the replaced originals are, and all of it is
  deleted only at commit; a rollback renames them back. The cost is
  transient disk use of the old and new files together, counted in the free
  space check.
- **The game's own originals survive the switch.** Their backups carry over
  from X's entries to Y's; a switch X to Y then uninstall returns the game's
  folder to its state before X.
- The note "replaced a previous X install" goes in the report.

### Moved-aside files

Files that stay out of the way of a route but are not ours:

- **The game's `d3dcompiler_47.dll`** (and any other name the route list
  names) is renamed to `<name>.dlss5-off` on the routes that need Windows'
  copy (`installer.py:318-319`, `:3499-3537`). Recorded as `moved-aside`;
  restored by the backup rules.
  **Changed from the reference:** when `.dlss5-off` already existed the
  reference deleted the current `d3dcompiler_47.dll` (`:3517-3518`), and at
  uninstall it swept every `*.dlss5-off` in the folder, unlinked whatever sat
  under the original name and renamed over it (`:3540-3565`), reverting a
  game update. These are now ordinary backup entries with hashes.
- **A hand-installed OptiScaler** under the name ReShade needs is backed up
  with its forwarder DLL and `.ini` and moved aside, with a note
  (`installer.py:3705-3731`); the route spec owns the file names.
- **An older bridge add-on** (`dlss5-dx11-bridge.addon64`) loads beside a new
  one; every ReShade route backs it up and moves it aside; it returns on
  uninstall (`installer.py:4544-4585`).
- **GITC-Uplift's neighbour** (`nvngx_dlssnr.dll` beside the executable on
  RTX 50 loads before the add-on can) is backed up and moved aside, kept under
  the superseded name when a backup already exists (`:1599-1619`).

### Quarantined files

Antivirus can remove a freshly written file: the components are unsigned,
uncommon and heuristically flagged (`installer.py:5347-5377`; README "Is it
safe").

- At commit the install checks every `component` entry exists with the
  recorded size and hash. Missing or changed files go in a warning: "N file(s)
  were written and are no longer there: a, b, c, d... Almost always this is
  antivirus." The warning names the first four and logs the first eight.
  **Changed from the reference:** it checked existence only and skipped
  backups; its text asserted the detections "are false positives"
  (`:5362-5369`). The text now says what is known (the files are gone) and
  what to do (restore them from the antivirus' quarantine and exclude the
  game folder, then install again), and says nothing about whether the
  detection is right. See "To verify".
- **The record is not rewritten** to hide it. The record says what was
  written; the disk says what survives. A "verify" operation compares them
  (missing, changed, ok); the game page and the diagnosis call it, so a file
  removed after the install is seen too, not only within the install's own
  seconds.
- **Uninstall** of a quarantined file finds nothing to delete and clears the
  entry. The backup of what it replaced is still restored, since our file is
  gone and the game's original is the right state.

### Uninstall from the record

Removes exactly what the record names, restores what it replaced, and nothing
else (README "Using it"). Reached from the game page, the library's
right-click menu and `--remove`. Refused while the game runs, as the install
is: the reference attempted it and reported locked files as "stuck"
(`installer.py:5692-5706`), which leaves a partly removed chain.

**Per entry**, decided from the disk and the hashes (**Changed from the
reference:** it copied the backup over the path and deleted the rest with no
content check, so a game update was reverted or deleted,
`installer.py:5616-5629`, `:5692-5706`):

| Entry | On disk now | Action |
| --- | --- | --- |
| written, no original | hash equals recorded | delete (retry a locked file four times with growing waits, clear read-only, then `stuck`) |
| written, no original | hash differs (the game or store replaced it, or the person edited it) | leave it, report "changed since install: not removed" |
| written, no original | missing | clear the entry |
| written, with original | hash equals recorded | replace with the backup (atomic), then delete the backup |
| written, with original | hash equals the original (the store verified files and put it back) | delete the now-redundant backup; the file stays |
| written, with original | hash differs from both (the game shipped a newer file) | leave the file; move the backup to `.dlss5-autopilot-superseded`; report |
| written, with original | missing | restore the backup |
| backup | its hash differs from the recorded original, or it is missing | do not restore a damaged file; keep the backup; state `restore-failed`; leave our file in place; report. A missing backup with our file present: delete ours, report that the game's original is gone and its store can repair it |
| `config` class | any content | delete when no original; restore the backup when there is one, regardless of hash (the components rewrite these while running) |

Then, in this order:

1. Config edits of user-owned files, by recorded key (below).
2. The `host64` helper folder: the recorded entries inside it, then the
   folder if empty. **Changed from the reference:** it removed `host64` with
   a recursive delete whether or not it held anything of ours
   (`installer.py:5708-5712`).
3. Runtime artifacts written while the game ran, which the record cannot
   know, are removed by exact known name: `ReShade.log`, `dlss5-feed.log`,
   `dlss5-feed-host.log`, `dlss5-bridge.log`, `OptiScaler.log`, `nvngx.log`,
   `nvngx_dlssnr.log`, `nvngx_dlss.log` (`installer.py:330-339`,
   `:5717-5725`), and DXVK's logs named after the executable, only when DXVK
   was ours (`:5726-5737`). OptiScaler's `Logs` folder: only files whose name
   starts with `OptiScaler`, and the folder only if that empties it.
   **Changed from the reference:** it also deleted every `.log` in a folder
   named `Logs` (`:5744-5747`), which may be the game's own.
4. Folders the record shows the install created, deepest first, if empty.
   **Changed from the reference:** it tried every parent of every removed
   path plus a fixed set of shader folders (`:5841-5859`), which can remove
   an empty folder the person made.
5. The graphics preference and the shared registrations (below).
6. Per-folder data in the tool's own folder (the settings index, frame-rate
   measurements, remembered plans, the failure note) is dropped
   (`installer.py:5762-5773`).
7. The record, last: removed only when no entry is left. With leftovers it
   is rewritten, atomically, with state `removing`, **all entries not yet
   processed and every failed restore with its backup**, and the message
   "Removed N items; M could not be removed. Close the game and its
   launcher, then uninstall again." The reference rewrote the record with
   only the stuck names, dropping the failed restores' backups, with a
   non-atomic write (`:5861-5886`).

Uninstall is idempotent and resumable. The final message is "Removed N
items." with a line per item ("removed: X", "restored: X (the game's own
file)").

### Uninstall without a record

When the record is missing, **or unreadable, or the wrong shape**, uninstall
falls back to a known-names cleanup and says "No install record found;
cleaning up by known filenames."

**Changed from the reference:** a corrupt record was read as "no files, no
data" and the known-names list was used only when the record file was absent
(`installer.py:5429-5431` against `:5432-5528`), so a damaged record made
uninstall remove nothing, say "Removed 0 items", and delete the evidence.

Rules of the fallback, stricter than the reference:

- A name unique to this tool's components (`dlss5-feed.addon64`,
  `dlss5-bridge.addon64`, `renodx-dlss5.addon64`, `renodx-dlss.addon64`,
  `nvngx.dll.addon64`, `standalone-dlssnr.addon64`, `dlss5-feed.cfg`,
  `dlss5-bridge.cfg`, `DLSS5_Feed.fx`, `DLSS5_AIO_Feed.fx`, `vort_Motion.fx`
  and the other names in `installer.py:5433-5460`) is deleted.
- A ReShade proxy under any of its six names is deleted only when its
  content says it is ReShade (the string "ReShade" and over 1 MB, or its
  version resource), so a game's own `d3d11.dll` stays
  (`installer.py:611-618`, `:5514-5516`). REFramework's `dinput8.dll` and
  DXVK's DLLs the same, by content (`:5520-5521`, `:5466-5484`).
- **A name that games commonly ship themselves is never deleted by name
  alone.** The reference's list contained `nvngx_dlss.dll`, `nvngx_dlssg.dll`,
  `ReShade.ini`, `ReShadePreset.ini` and `dgVoodoo.conf`
  (`installer.py:5441-5453`): a known-names uninstall deleted the game's own
  DLSS runtime and a person's own ReShade settings. These are deleted only
  when a marker the install writes proves them ours (route specs define the
  markers); otherwise they are listed as "not removed: could not tell it from
  yours".
- Backups with the tool's suffix found in the install folder (and, for a
  swapped runtime, in the game folder) are restored; with no record there is
  no hash to compare, so a restore may revert a game update, and the log
  says "restored from a backup with no record; if the game updated since,
  verify its files". The scan is bounded the way detection's is.
- Shared registrations are left alone (there is no record of what this
  install needed); the dependents list (below) decides.
- A `d3d9.dll` in a Remix game is the Remix runtime and is left alone
  (`installer.py:5466-5476`).

### Shared registrations

Vulkan and OpenXR layers are global for the Windows user, shared by every
install that needs them, and the only effect of an install that reaches
beyond the game folder besides a graphics preference and a config edit. They
are announced in the preview, journaled before they change, and removed with
the last install that needs them.

**What is registered.**

| Layer | Registry key (value name is the full path of the manifest, data DWORD `0` = enabled) | Layer name | Files |
| --- | --- | --- | --- |
| Vulkan, 64-bit | `HKCU\Software\Khronos\Vulkan\ImplicitLayers` (`vulkan.py:32`) | `VK_LAYER_reshade` | `%LOCALAPPDATA%\dlss5-autopilot\reshade-vulkan\ReShade64.dll` and `ReShade64.json` |
| Vulkan, 32-bit game | same key | **`VK_LAYER_reshade32`** (different on purpose: the loader de-duplicates by name and would drop the 32-bit one; `vulkan.py:36-58`) | `ReShade32.dll`, `ReShade32.json` |
| OpenXR (VR, 64-bit games only) | `HKCU\Software\Khronos\OpenXR\1\ApiLayers\Implicit` (`openxr.py:28`) | `XR_APILAYER_reshade` | same folder, `ReShade64_XR.json` beside the shared `ReShade64.dll` |
| Vulkan, all users | `HKLM\Software\Khronos\Vulkan\ImplicitLayers`, the 64-bit view for 64-bit, the 32-bit view for 32-bit (`vulkan.py:67-91`) | as above | a second copy under Program Files (path from the registry's `ProgramFilesDir`, never the environment variable, `vulkan.py:248-267`): `...\dlss5-autopilot\reshade-vulkan\` |

All files come from the ReShade setup executable (it has the DLLs and
manifests appended as a zip). The manifest's `library_path` is rewritten to
`.\<dll>` so a moved folder cannot leave a dangling path
(`vulkan.py:178-196`, `openxr.py:114-119`).

**Install-time decisions** (the preview asks the same questions in the same
order, so it cannot promise what the install skips, `vulkan.py:378-403`):

| Situation | Action |
| --- | --- |
| an active (value `0`) ReShade layer that this game's architecture can load is registered by something else (ReShade's own installer) | reuse it, write nothing, record "reused" and its path |
| it is a layer for the other architecture | not usable: register ours |
| a registration exists but its value is not `0` | it is disabled and not reused |
| our own 32-bit manifest still carries the 64-bit layer's name (an older release's) | rewrite it |
| the tool runs elevated, the game is 64-bit, and the only layer is on the per-user list | register ours for all users too: an elevated process reads only the machine's list (the loader skips HKCU under a high-integrity token) |
| elevated, and a foreign layer for that architecture is already on the machine's list | do not add ours beside it (two ReShades in one 32-bit process) |
| the tool is not elevated | per-user registration only; the preview and the report say programs started as administrator will not get it |

The machine's 32-bit view is deliberately not consulted when looking for a
layer to reuse: a foreign 32-bit ReShade there calls itself
`VK_LAYER_reshade` and would clash with ours (`vulkan.py:78-91`).

**The all-users layer is never built from the per-user cache.** The copy under
Program Files is extracted by the elevated process from a ReShade setup
executable that the elevated process fetched or verified itself, into a
location only administrators can write, and the setup is checked as the net
spec's component verification requires. It is never taken from, or copied
out of, `%LOCALAPPDATA%` (the download cache or the per-user layer folder):
any program running as that user can replace those, and the next elevated
install would put its file into every elevated Vulkan program on the PC.
**Changed from the reference:** `vulkan.py:345-375` says it extracts again
"from ReShade's setup, not copied from the per-user folder", but the setup
path it receives is the download cache file (`installer.py:4453` and
`:4493`, `:4464`), writable by the unelevated user. See open question 3.

**Bookkeeping: who depends on a layer.**

- The set of install folders relying on each layer is kept **outside
  `settings.json`**, in a small file beside the layer files (the layer folder
  is the tool's own and the layer's lifetime is the list's).
  **Changed from the reference:** it kept `vulkan_games` and `openxr_games` in
  `settings.json` (`prefs.py:255-310`), where a damaged or locked file read
  as an empty list (see the next rule) and one write of one key rewrote the
  rest.
- Entries are stored and compared as **resolved paths**: final path after
  junctions, symbolic links and 8.3 names, case-insensitively, by whole
  parts. The reference compared `str(root)` of whatever spelling the game
  list gave (`prefs.py:275-288`, `installer.py:5789`, `:5804`), so a game
  added once as `D:\Games\X` and once through a junction counted twice or
  not at all, and the layer was never removed. The same comparison applies to
  every "is this registration ours" test (`vulkan.py:171-175` resolved
  paths, but `unregister` deleted by the string it built, `vulkan.py:509-525`,
  `openxr.py:133-147`): removal enumerates the registry values and deletes
  those whose resolved path is our manifest.
- **The list is pruned against the disk.** An entry whose folder no longer
  exists, or whose record no longer lists the layer, is dropped, at every
  uninstall and at start. The reference never pruned: a game deleted from the
  store stayed in the list forever and the global layer was never removed.
- **Unknown is not empty.** If the list cannot be read (locked, damaged) the
  layer is kept, the list file is not rewritten, and the log says why. Only a
  list read successfully and found empty after pruning removes a registration.
  The reference has this rule in one place (`prefs.py:260-272`,
  `vulkan.py:539-559`) and violates it in the install and uninstall paths,
  which read through the default reader that returns `[]` for an unreadable
  file (`installer.py:5804-5806`, `prefs.py:255-258`).
- **Never remove a registration we did not create.** Removal deletes only
  values naming our own manifests. A reused foreign layer is left even when
  it is the last dependent.

**Uninstall of the last dependent.**

- The per-user values are deleted (both manifests), then the layer folder's
  files when neither the Vulkan nor the OpenXR registration remains (the DLL
  is shared). A DLL held by a running Vulkan or OpenXR program is left and
  tried again at the next start. **Changed from the reference:** it never
  deleted the layer files from the per-user folder.
- The all-users registration and its Program Files copy are removed only by
  an elevated run. Not elevated, the log says "still registered for all
  users: ReShade's Vulkan layer (...). It loads into every Vulkan program on
  this PC, and removing it needs administrator rights: start this tool once
  as administrator and it takes it off" (`installer.py:5394-5406`). Started
  elevated later, with no live dependents, it removes it
  (`vulkan.py:539-559`). Other Windows users' lists are an open question
  (4).
- A layer file refresh is skipped when the file already has the content to be
  written, so a running Vulkan program holding the DLL does not fail the
  install. When it does differ and is held, the install says "ReShade64.dll
  is in use by a running Vulkan or OpenXR program - close it and install
  again" (`openxr.py:106-111`); the reference worded the Vulkan case as a
  game-running refusal.
- OpenXR is only registered for 64-bit games; for a 32-bit game the log says
  "VR: the OpenXR layer is 64-bit only - skipped" and the install continues
  (`installer.py:4456-4460`).
- The report carries the notes and warnings (`installer.py:4466-4516`): the
  layer "loads into EVERY Vulkan/OpenXR application, not just this game", and
  VR through OpenXR "has not been tried with a headset by the author".

### The Windows graphics preference

On a machine with an NVIDIA card and another GPU, Windows may run the game on
the other card, where NGX does not exist. The tool sets the program's
"High performance" preference (`gpupref.py:1-25`).

- **Where.** `HKCU\Software\Microsoft\DirectX\UserGpuPreferences`, value name
  the full path of the executable, `REG_SZ` data holding semicolon-separated
  `Name=Value;` tokens; ours is `GpuPreference=2;` (2 is High performance).
  The value is shared with other tokens (`SwapEffectUpgradeEnable=1;`, Auto
  HDR): add or remove only ours, keep the rest as found.
- **Which programs.** The game's executable; the 32-bit feeder's 64-bit helper
  `host64\dlss5-feed-host64.exe`; GITC-Uplift's helper
  `gitc-uplift-helper64.exe` for a non-64-bit game
  (`installer.py:3345-3355`).
- **Only when it matters.** A machine with an NVIDIA adapter and an Intel or
  AMD one (the GPU spec defines detection; DXGI's list first, adapter names
  as the fallback), and the option on (default on; `--no-gpu-pref` and a
  settings box turn it off). A preference the person already set for that
  program, any value, is left alone and never recorded as ours, with a note.
  Our own `2` found again on a reinstall keeps its first record
  (`gpupref.py:236-274`).
- **The record.** Per program: path, the string before (absent or the text),
  and the string written. Journaled before the registry write.
- **Restore.** Only while the value still says `2`. If the whole string is
  unchanged since the install, the old string returns exactly, or the value
  is deleted when there was none. If other tokens changed since (Windows
  toggled Auto HDR), only our token is removed. If our token is changed or
  gone, it is the person's now and is left alone (`gpupref.py:277-307`).
- **Never stops an install.** A failure (a locked key) becomes a warning with
  the by-hand path "Settings > System > Display > Graphics", and the earlier
  records stay on the record (`installer.py:3376-3429`). Turning the option
  off and reinstalling takes the preference back out.
- Program paths are compared as resolved paths, as everywhere (the reference
  compared lowercased strings with slashes normalized, `gpupref.py:232-233`).
- Tests use an in-memory registry, never the machine's (the reference had one,
  `gpupref.py:73-90`).

### Emulator config edits

For an emulator whose renderer is not one ReShade can reach, the tool edits
the emulator's own config to a DXGI backend, so the install is one press
(`emulators.py:376-391`). It edits **text**, one line, never re-serialising
through a parser: INI, TOML and `.cfg` writers reorder keys and drop
comments.

- **Byte-exact outside the one line**: the file's encoding including a BOM,
  its CRLF or LF, bytes that are not valid UTF-8, comments, spacing and
  every other key (`emulators.py:658-665`).
- **Locate.** The first existing candidate in each emulator's own order
  (portable markers next to the executable first, then Documents or AppData).
  No config yet: "config file not found (run the emulator once first). Set the
  backend by hand: `<hint>`" and the install goes on
  (`emulators.py:791-796`).
- **Decide.** Read the key. Already a DXGI value: nothing to do, "backend
  already `<name>` in `<file>`". Otherwise write the target. A hand-written
  value the tool does not know is shown as itself. Emulators with no DXGI
  path at all (Vulkan or OpenGL only) are not edited; the report says so with
  the hint, and those route through the Vulkan layer (the routes spec).
- **Write.** Replace the key's line in place, keeping any trailing comment;
  if the key is absent, add it under its section header; if the section is
  absent, append the section. Quote the value where the format quotes.
- **Record only the keys changed** (**Changed from the reference:** it copied
  the whole file to `<config>.dlss5-autopilot-backup` and, at uninstall, copied
  that whole file back over the config, erasing every setting the person or
  the emulator changed since, `emulators.py:808-811`, `:831-833`;
  DuckStation and RetroArch rewrite their config at exit): the record holds
  the file, the section and key, the original line's exact text or "absent",
  whether the section or the key was added, the value written, and whether the
  file ended with a newline.
- **Restore** by key: if the key still holds the value we wrote, put the
  original line back, or remove the line we added (and the section header
  only if we added it and it is empty), and restore the final newline's
  state. If the person changed the key since, leave it. Nothing else in the
  file is touched. No backup file is left in the emulator's folder.
  The config is written atomically.
- The edit is journaled before it is made, and is undone on rollback.
- **Running emulator.** The emulator is the game executable, so the game
  running check covers it; the edit would be overwritten when it exits.

The backends the reference knew (`emulators.py:536-629`; every value was
read from the emulator's source by the author and needs re-checking, see "To
verify"):

| Emulator | Section, key (written) | Written | Already DXGI | Config files, in order (`:426-522`) |
| --- | --- | --- | --- | --- |
| DuckStation | `[GPU]` `Renderer` | `D3D12` | `D3D11`, `D3D12` | `settings.ini` beside the exe if `portable.txt` or `settings.ini` is there; `Documents\DuckStation\settings.ini`; `%LOCALAPPDATA%\DuckStation\settings.ini` |
| PCSX2 | `[EmuCore/GS]` `Renderer` | `15` (D3D12) | `3` (D3D11), `15` | `inis\PCSX2.ini` beside the exe if `portable.ini` or `portable.txt`; `Documents\PCSX2\inis\PCSX2.ini` |
| Dolphin | `[Core]` `GFXBackend` | `D3D12` | `D3D` (D3D11), `D3D12` | `User\Config\Dolphin.ini` beside the exe if `portable.txt`; `Documents\Dolphin Emulator\Config\Dolphin.ini`; `%APPDATA%\Dolphin Emulator\Config\Dolphin.ini` |
| PPSSPP | `[Graphics]` `GraphicsBackend` | `2 (DIRECT3D11)` | `2`, `2 (DIRECT3D11)`, `DIRECT3D11` | `PSP\SYSTEM\ppsspp.ini` under the memstick: the path in `installed.txt` (or Documents\PPSSPP when empty), else `memstick\` beside the exe, then Documents\PPSSPP |
| Xenia | `[GPU]` `gpu` (quoted) | `"d3d12"` | `d3d12` | `xenia-canary.config.toml` or `xenia.config.toml` (canary first for a canary exe) in the exe folder if `portable.txt`, then `Documents\Xenia` |
| RetroArch | no section, `video_driver` (quoted) | `"d3d11"` (d3d12 lacks features some cores use) | `d3d11`, `d3d12` | `retroarch.cfg` beside the exe, then `%APPDATA%\RetroArch\retroarch.cfg` |

Read-only (shown, never edited): RPCS3 (`config.yml` beside the exe, key
`Renderer`) and Cemu (`settings.xml`, `<api>`), which have no DXGI path.
The other eleven profiles have a hint and no edit (`emulators.py:58-215`);
which emulators exist, and how they are found, is the games spec's.

### Closing the window during an operation

**Changed from the reference:** closing the window ended the process at once.
The install ran on a daemon thread (`ctl_game.py:1447`) and the close handler
never looked at it (`ctl_watch.py:519-574`, `app.py:96`), so a close mid-install
killed it between a backup and the write, with no record at all.

- A close request while an install or uninstall is running **asks the
  operation to stop and waits** for it to reach a safe point: between files,
  and between chunks of a download or a copy. The install then rolls back
  (an uninstall finishes the entry it is on and leaves the rest on the
  record); the window shows "stopping, putting the folder back" meanwhile and
  closes when done. A second close request while it waits does not kill it
  but restates the line.
- The command line handles the console close and Ctrl+C the same way.
- A forced kill, a crash or a power loss is covered by the journal: the
  record names everything touched, and the next operation on the folder
  (or the next start of the tool, for folders it knows) completes the
  rollback or removal (see the lifecycle).

### Disk full

Recognised through any chain of wrapped errors, by errno 28 and Windows codes
112 and 39 (the net spec). Install adds:

- **Before the first write.** The free-space refusal above. The message names
  the game's drive, how much is free, how much the install needs, and ends
  "free some space on it, then install again".
- **During a write.** The temp file is removed, the install rolls back
  (rolling back needs no extra space because originals are kept by rename or
  were copied before the first overwrite), the message is "Out of disk space
  on `<drive>` (`<free>` free)." followed by where the game and the cache
  are with their free space (the net spec's text), and the reason is kept in
  the failure note so the diagnosis does not tell the person to simply install
  again. Settings writes that hit a full drive keep the value in memory and
  show a notice once, instead of dropping it silently (`prefs.py:104-105`).
- **The record cannot be saved.** Because the record precedes the writes,
  this is a refusal at the start or before a group ("nothing was touched"),
  never a finished install with no list.

### Settings: `settings.json` and atomic writes

`%LOCALAPPDATA%\dlss5-autopilot\settings.json` holds the tool's preferences
(`prefs.py:22-26`). Other specs own the keys; this one owns how the file
behaves. The reference's module also held a remembered local add-on file and
the install, Vulkan and OpenXR lists; the lists are no longer settings
(above).

- **A missing file is empty defaults.** Unknown keys are kept on write, so
  an older build never drops a newer one's keys.
- **A value of the wrong type** (a hand-typed `"F10"` where a key code is
  expected) is read as the default, per key, and never ends an install
  (`installer.py:886-896`).
- **One writer at a time**, across threads and across running copies of the
  tool: a read-modify-write is done under a lock (a named mutex for the
  copies), re-reading inside the lock. The reference's lock was in-process
  only (`prefs.py:36-40`).
- **Unreadable is not empty.**
  - A file that cannot be read for a moment (locked by an antivirus or
    another process) is retried briefly; if it stays so, the value is read
    from the last good copy, and **the file is not written**: a write made
    from an empty read would drop everything (`prefs.py:55-72`,
    `:112-129`).
  - A file that parses to something that is not an object, or does not
    parse, is **corrupt**: it is moved aside to `settings.json.corrupt` (the
    newest one kept, never deleted by the tool), the tool starts from
    defaults, and says so once. Nothing safety-relevant lives here, so
    defaults are safe: the install records, the layer dependents list and the
    graphics-preference records are not in it.
  - **Unreadable settings never drop the Vulkan games list**: that list is not
    in `settings.json`, and no read or write of settings can alter it
    (`prefs.py:255-272`, gate 2.0.9, defeated by `set_` writing a one-key file
    when no good copy had been seen, `:112-129`).
- **Atomic writes**, for the settings, the install record, the layer
  manifests and every config the tool writes whole:
  1. write the full content to a uniquely named temp file in the same folder
     (process id plus a random part; the reference used one fixed `.tmp` name
     for the record, `installer.py:3230`);
  2. flush the data to the disk (`fsync`, `FlushFileBuffers`) **before** the
     rename;
  3. replace the target with a write-through rename, retried a few times
     (100 ms apart, up to 3 to 6 tries) when an antivirus or indexer holds the
     old file;
  4. on failure remove the temp file and leave the old target whole. **Never
     fall back to writing the target in place** (the reference did after
     six failed renames, `prefs.py:98-102`).
  Without the flush, a crash after the rename can leave an empty file under
  the target's name on a journaling file system.
- **The log** `autopilot.log` (`log.py`): one file, trimmed past 1 MiB to its
  newest half with a "[earlier entries trimmed]" marker; only paths and
  versions, never anything typed in; a failure to log is swallowed, because
  logging is never the thing that breaks the tool; every unhandled exception
  is recorded with a traceback and a label saying what was being done (also
  from background threads), and the last one is available for a bug report.
  **Changed from the reference:** the trim rewrote the file in place
  (`log.py:63-70`); it now rotates to a second file by rename, which is safe
  with two copies of the tool running.

### System tools

Windows tools are started by absolute path from Windows' own system folder,
never by bare name. A bare name is searched in the application's folder first
(often a user-writable download folder) and then the current directory, so a
planted `tasklist.exe` or `cmd.exe` next to the tool runs. The reference
started `tasklist` (`installer.py:3438`), `cmd /c <bat>` (`selfupdate.py:399`),
`powershell` (`wincrash.py:99`) and `explorer` (`ui/app.py:396`,
`ui/shell.py:916`) by bare name.

- The folder comes from the operating system's API (the system directory and
  Windows directory), never from an environment variable a user can set.
- `tasklist`, `cmd.exe` and `powershell.exe` are under System32
  (`WindowsPowerShell\v1.0\` for the last); **`explorer.exe` is in the
  Windows folder, not System32**.
- Prefer no process at all: the running-process check uses the process API
  (image path), and "open folder" and "show in folder" use the shell's
  open verb.
- The self-update script is staged in a folder only the user can write and
  carries its own risks (the updater spec).

## What must never happen

- **A write that the durable record does not name.** No file, registry value
  or config key changes before the record naming it, with what it held, is on
  disk.
- **A file of the game's overwritten or deleted with no verified backup.**
  This includes the case where the backup copy fails, is cut short or is
  stale.
- **A file written outside the game folder** (other than the listed
  registrations, graphics preference, emulator config and the tool's data
  folder), or a restore to anywhere other than where the backup came from. A
  record entry that resolves outside the game folder, including through a
  junction inside it, is ignored.
- **A half-written file under a final name.** Writes are to temp names,
  flushed, renamed.
- **Anything moved or deleted before the last refusal check** has passed.
- **A rollback that leaves a mix of two installs,** or removes a working
  install over a network error.
- **An uninstall that reverts the game's update:** a file whose content is not
  what we wrote is not deleted, and a backup is not restored over a file the
  game has updated.
- **A restore that plants a damaged file.** A backup that fails its hash is
  not restored.
- **A failed restore that loses its backup:** the backup and the entry stay.
- **Removing a shared registration we did not create,** or one another
  install, or another Windows user, still needs; or acting on an unreadable
  dependents list.
- **An all-users layer built from anything a standard user can write.**
- **A config edit that rewrites more than the keys changed,** or restores more
  than that.
- **Deleting by name alone a file the game commonly ships** (`nvngx_dlss.dll`,
  `ReShade.ini` and the like).
- **Removing a folder the install did not create,** or the contents of a
  folder named like ours that held other things.
- **A bare-name start of a Windows tool.**
- **Overwriting an unreadable settings file with a write that lacks its
  content,** or deleting a corrupt one.
- **Closing the window in the middle of a transaction without waiting or
  recording first.**
- **Any request on the network, or any write, from the preview.**

### The takeover's thirteen findings, and where each is specified

| Finding | Where |
| --- | --- |
| closing the window during an install waits or records first | "Closing the window during an operation", "Order of an install" |
| files enter the record before they are written | "Order of an install" step 3, the record's lifecycle |
| a failed restore keeps its backup on the record | "Backups" (restore), "Uninstall" per-entry table and step 7 |
| a corrupt record falls back to a known-names uninstall | "Uninstall without a record" |
| atomic writes fsync before rename | "Settings: `settings.json` and atomic writes" |
| nothing is moved or deleted before the last refusal check | "Refusal checks", "Order of an install" |
| game updates between install and uninstall are detected by hash | "Backups" (ownership), "Uninstall" per-entry table |
| emulator config restores only the keys changed | "Emulator config edits" |
| the ReShadePreset technique is removed only on routes that wrote it | bug 12 below |
| layer bookkeeping compares resolved paths | "Shared registrations" (bookkeeping) |
| unreadable settings never drop the Vulkan games list | "Settings" (unreadable is not empty), "Shared registrations" |
| the all-users Vulkan layer is never built from the per-user cache | "Shared registrations" |
| system tools run by absolute System32 path | "System tools" |

## Bugs and questionable choices in the reference not to port

1. **The record is written last** (`installer.py:5380-5387`, and in the error
   handlers `:5244`, `:5272`, `:5301`). A kill, a closed window or a full drive
   at the end leaves a set-up folder with no list; `_roll_back` /
   `NO_RECORD_HEAD` exist to say so (`:1292-1355`). A real shape: reports 327,
   438, 761, 897 and 956 carry "install record: MISSING".
2. **Closing the window kills the install** (`ctl_game.py:1447` daemon
   thread; `ctl_watch.py:519-574` ignores `busy`).
3. **Interleaved download and write** (`installer.py:4451-4600`): a failure
   after the first write leaves half a chain; rollback was added for it
   afterwards (#438, gate 2.0.6). Report 175: `dxgi.dll` present, the rest
   missing.
4. **Refusals after moves** (`:3717-3727`, `:3751` before `:3759-3843`;
   `:3954-3968` after DXVK and REFramework).
5. **Rollback only for fresh installs** (`:1246-1258`) and by running the
   whole uninstall, so an update's failure leaves a mix and a switch's failure
   leaves neither route (`:1281-1289`).
6. **Corrupt record means nothing is removed** (`:5429-5431` vs `:5432`), and
   the evidence is deleted (`:5887`).
7. **The known-names list deletes the game's own files**: `nvngx_dlss.dll`,
   `nvngx_dlssg.dll`, `nvngx_dlssnr.dll`, `ReShade.ini`,
   `ReShadePreset.ini`, `dgVoodoo.conf` (`:5433-5460`), contradicting the
   function's own docstring "never touch the game's own files" (`:5410`).
8. **Restore with no content check** (`:5623-5627`): the backup is copied
   over whatever is there and deleted, reverting a game update; a failed
   restore is logged and the record is removed anyway if nothing else is
   stuck (`:5628-5629`, `:5887`), orphaning the backup. The "restored" set is
   built from the file list before any restore succeeds (`:5686-5690`), so a
   failed restore also leaves our file undeleted.
9. **A restore safety net that walks the tree** (`:5573-5577`): every file
   with the backup suffix is restored, recorded or not, including a stale one
   from an earlier install, with `rglob` over the folder.
10. **Ownership by name** (`:2976`, `:3740`, `:818`): a game-replaced file at
    a recorded name is "ours": overwritten with no backup, deleted at
    uninstall.
11. **A truncated backup is trusted by existence** (`:842-850`, `refw.py:72-78`,
    `mfg.py:428-434`, `dxvk.py:185-193`, `optiscaler.py:724-727`): five
    backup implementations, each patched for cut-short copies.
12. **ReShadePreset techniques stripped on every route.** The record always
    stores `provider` (default 3, `installer.py:3177`), and uninstall always
    calls `remove_our_techniques` with it (`:5757-5761`, `reshade_ini.py:477-516`)
    after the backup restore, so on the native, bridge, OptiScaler and Remix
    routes the LumeniteFX and feed techniques are removed from the person's
    own preset, including a preset just restored from its backup. Only the
    feeder route writes a preset (`:5055-5071`), and a file we wrote is simply
    deleted or restored.
13. **Orphan suffix and name-only entries** (`:3040-3041`, `:3086`): our own
    files of a previous route "moved aside" to be deleted later, recorded by
    base name.
14. **Sideline deletes the game's compiler** when a `.dlss5-off` already
    exists, and the restore unlinks whatever sits under the original name
    (`:3517-3518`, `:3558-3560`).
15. **Recursive delete of `host64`** (`:5708-5712`) and **every `.log` in a
    `Logs` folder** (`:5744-5747`), recorded or not.
16. **The stuck record is rewritten with only the stuck names** and a
    non-atomic write (`:5865-5880`), dropping the backups of failed restores.
17. **Zip extraction writes in place** (`net.py extract_one`), as do the
    shader headers (`installer.py:2736`, `:4625`) and the Remix `rtx.conf` edit
    (`remix.py:234`, `:271`). The one atomic write has no `fsync`
    (`:1533-1534`, `:3232-3237`); the settings write falls back to an in-place
    write after six failed renames (`prefs.py:98-102`).
18. **The Remix `rtx.conf` edit deletes a pre-existing key's line.** Setting
    replaces an existing line (and drops duplicates of the key), and removing
    deletes the line, so a user's own `key = False` is gone, not restored
    (`remix.py:196-236`, `:249-274`).
19. **The emulator restore copies the whole backup back** (`emulators.py:831-833`);
    the backup is made only if none exists, so a later re-edit restores a
    stale file (`:809-810`).
20. **The quarantine check is existence only,** skips backups, and asserts the
    detection is a false positive (`installer.py:5354-5370`).
21. **The preview is a hand-copied second implementation** (`:2012-2693`).
22. **Layer bookkeeping.** String comparison of paths (`prefs.py:275-288`);
    never pruned; stored in `settings.json`, readable as empty when the file
    is not (`prefs.py:255-258`, `:112-129`); `unregister` builds the registry
    value name by string (`vulkan.py:509-525`); the layer files are never
    deleted from the per-user folder; the per-user list gates a machine-wide
    registration other users may rely on (`vulkan.py:539-559`).
23. **The all-users layer takes the cached setup executable** (`vulkan.py:345-375`
    fed by `installer.py:4493`).
24. **A game-running check by executable name only** (`installer.py:3491`):
    misses the launcher or another process holding the files, and matches an
    unrelated process with the same name; run by a bare-name `tasklist`
    (`:3437-3443`).
25. **`vulkan.install_layer` raising a permission error** on the held layer
    DLL surfaced as "the game is running" (`installer.py:5259-5269`).
26. **Bare-name system tools** (`installer.py:3438`, `selfupdate.py:399`,
    `wincrash.py:99`, `ui/app.py:396`, `ui/shell.py:916`).
27. **Dead and legacy code**: the legacy record and backup names and Turkish
    keys (`installer.py:346-350`, `:2936-2946`), dgVoodoo2's files in the
    known-names list (`:5447-5451`, dropped in release 1.6.0),
    `_nested_game_dlss` and friends gating on a Tk-thread cache
    (`:925-1002`), and `getattr(g, "bitness", None)` defensive reads
    throughout. Do not port without a live consumer.
28. **The settings lock is in-process only** (`prefs.py:40`); two copies of
    the tool lose each other's writes.
29. **A failed settings write is silent** (`prefs.py:104-105`).
30. **The all-users cleanup at start** runs with no knowledge of other users
    (`vulkan.py:539-559`).

## To verify against primary sources

- **Windows file system.** What `MoveFileEx` with `MOVEFILE_WRITE_THROUGH`,
  `ReplaceFile` and `File.Replace` guarantee on NTFS after a power loss;
  whether `FlushFileBuffers` on the temp file before the rename is enough;
  that a directory cannot be flushed. Source: Microsoft's file system docs and
  the .NET `File` API remarks.
- **Rename of a file in use.** That a loaded DLL can be renamed but not
  overwritten or deleted, and how antivirus and indexer handles show up
  (`ERROR_SHARING_VIOLATION`, `ERROR_ACCESS_DENIED`). This decides whether the
  backup-by-rename and hold-until-commit designs work on the files that
  matter (a proxy DLL held by a launcher).
- **Vulkan loader.** Implicit layers' registry location and semantics
  (HKCU and HKLM, value name the manifest path, DWORD `0` enabled and non-zero
  disabled), HKCU ignored for elevated processes, layer de-duplication by name
  (the loader's own trace quoted in `vulkan.py:40-58`), the WOW6432Node view
  for 32-bit. Source: Khronos Vulkan-Loader `LoaderLayerInterface.md`
  and `LoaderInterfaceArchitecture.md`.
- **OpenXR loader.** The implicit API layer key
  `HKCU\Software\Khronos\OpenXR\1\ApiLayers\Implicit` and its HKLM sibling;
  whether HKCU is skipped for elevated processes; the manifest schema
  (`api_layer.library_path`). Source: OpenXR loader design doc
  (`OpenXR-SDK` `doc/specs`, "loader" and "api layers").
- **ReShade's setup executable.** That it carries `ReShade64.dll`,
  `ReShade32.dll`, `ReShade64.json`, `ReShade32.json` and `ReShade64_XR.json`
  as an appended zip; its Authenticode signature, if any; any published hash.
  This decides how the all-users copy can be verified (open question 3).
  Source: reshade.me, the ReShade repository's setup project.
- **`UserGpuPreferences`.** The key, the value name being the full path, the
  `GpuPreference=` token and its values (0 let Windows decide, 1 power saving,
  2 high performance), the other tokens Windows keeps there, whether the
  match is case-insensitive or by 8.3 name, and whether changes apply to a
  running program. Source: Microsoft's documentation on "graphics performance
  preference" and an observation of Settings > System > Display > Graphics on
  current Windows 11.
- **Elevation.** `IsUserAnAdmin` versus `TokenElevation` on a UAC-off
  machine, which the reference relies on (`wincrash.py:338-345`).
- **Program Files ACL.** That a standard user cannot write under the
  `ProgramFilesDir` value on a default Windows, so the machine copy cannot be
  replaced. The `ProgramFilesDir` registry value as the trusted source
  (`vulkan.py:248-267`).
- **Process image paths.** That a non-elevated process cannot always read an
  elevated process' image path, and what the fallback is.
- **Antivirus behaviour.** The README's statement of Defender heuristics
  (`Wacatac.B!ml`, `Ulthar.A!ml`, `Trojan:Win32/Fonzi.A!ml`) and what Defender
  does to unsigned files written by an unsigned process; where Protection
  history and quarantine live. The report's wording avoids claiming the
  detection is wrong until this is settled.
- **Store launchers.** What Steam "verify integrity", Epic and GOG repair do
  to a modified or added file, and to a game that updates `nvngx_dlss.dll`:
  decides how common the hash-mismatch branches are. Whether a game store
  deletes unknown files in the folder on an update.
- **Xbox / Game Pass folders.** That the install folder is system-owned and
  elevation does not help, and the store setting that allows editing
  (`games.XBOX_HINT`, games spec).
- **Emulator configs.** Every key, value and location in the table against
  each emulator's current source (`settings.cpp`, `Pcsx2Config.cpp`,
  `MainSettings.cpp`, `Config.cpp`, `xenia_main.cc`, `retroarch.cfg`), the
  value spellings (PCSX2's renderer numbers; PPSSPP's `"2 (DIRECT3D11)"`;
  Xenia's quoting), which emulators rewrite their config at exit, and whether
  one reads a config path from a setting the tool has not located.
- **`rtx.conf`.** Whether the key can legitimately appear more than once, and
  what the runtime does on a duplicate (the reference removed duplicates).
- **Case sensitivity.** That a directory marked case-sensitive (WSL
  interop) is not a case the tool must separate: comparison is ordinal
  ignore-case everywhere.
- **Windows tool paths.** That `explorer.exe` is in the Windows folder and
  `powershell.exe` under `System32\WindowsPowerShell\v1.0`.

## Fixtures

The C# tests need an in-memory file system or a temp folder, an in-memory
registry, a fake process list, a fake clock, and a fake elevation switch. All
the platform seams are interfaces in the engine (the plan's "Windows behind
interfaces"); the reference had the registry one already (`gpupref.py:73-90`).

- **Game folders**, built by tests from a description (file name, size,
  content): a folder with no pre-existing files; one with the game's own
  `dxgi.dll`, `nvngx_dlss.dll`, `ReShade.ini` (a user's own), `d3dcompiler_47.dll`,
  a hand-installed OptiScaler under `dxgi.dll` with `OptiScaler.ini` and its
  forwarder; one with a second ReShade under `d3d11.dll`; one with an add-on
  of another route; a Remix folder (`.trex\d3d9.dll`, `rtx.conf` with and
  without a final newline, with the key already present as `False`); an Unreal
  layout (root launcher stub, `Binaries\Win64\game.exe`, a nested
  `Plugins\...\nvngx_dlss.dll`); a 32-bit game (split `host64`); an Xbox-like
  unwritable folder. A snapshot helper hashes the whole tree (content,
  attributes, modified time) for the round-trip test.
- **PE stubs**: x64, x86 and truncated DLLs, an HTML page named `.dll`
  (`tests/fake_pe.py` in the reference tree).
- **Component archives**: a tiny zip per source with the member names the
  routes spec lists (a ReShade setup exe with the appended zip, the DLSS-NR
  zip, a feeder zip, an OptiScaler zip, DXVK, VORT, LumeniteFX), and the SHA
  they publish.
- **Install records**: one per route in the reference's version 1 shape
  (written by running the reference against fixture folders; the shape is
  `installer.py:3164-3213`), including a record with locked leftovers (`files`
  holding only the stuck names, `complete: false`, the "uninstall left N locked
  file(s)" note, `:5869-5874`), a record holding only the "rolled back" note
  (`net.py:54`), a legacy record with the Turkish keys, a truncated JSON, a
  record with a `../` entry, an absolute entry inside the game folder, and
  one outside it; and new-format records for each state in the lifecycle.
- **Registry**: an in-memory registry with ReShade's own Vulkan registration
  (64-bit, enabled; 32-bit; disabled), ours, a foreign machine layer, and
  `ProgramFilesDir`; the same for OpenXR; `UserGpuPreferences` values with
  `SwapEffectUpgradeEnable=1;`, `GpuPreference=1;`, none.
- **Emulator configs**: one per emulator in the table, byte-for-byte as the
  emulator writes it, with a BOM, with CRLF, with the key absent, with the
  section absent, with a trailing comment on the line, with a non-UTF-8 byte,
  and an unwritable file; `installed.txt` and `portable.txt` markers.
- **Corrupt settings**: empty file, truncated JSON, a JSON array, a file with
  a wrong-typed `overlay_key`, a file locked by another handle.
- **Disk full**: a file system fake that throws errno 28 and Windows 112 and
  39 at a chosen write, directly and wrapped in other errors; a fake free-space
  answer per drive.
- **Crash points**: a fake that stops the transaction after the Nth write, so
  a test replays recovery from the journal at every point.
- **Real-world shapes**: report `_tools/reports/175.txt` (failed install with
  `dxgi.dll` present and the rest missing, install 1.8.1) and the five with
  "install record: MISSING" (`327`, `438`, `761`, `897`, `956`).
- The reference's old suite lives in git history, deleted in `d5801a3`
  ("tests: delete the inherited top-level tests"); `python-reference~1` already lacks
  `test_all.py`, so use `git show d5801a3~1:test_all.py` (29,886 lines) for
  hints at behaviour.

## Open questions

1. **Reading the Python release's records.** The new tool reads and
   uninstalls version 1 records (hash-less). Also read the older names
   (`dlss5kur-kurulum.json`, `dlss5-installer.json`, `dosyalar`, the
   `.dlss5kur-yedek` and `.dlss5-installer-backup` suffixes,
   `installer.py:346-350`)? Those are from releases up to 1.2. Recommendation:
   read version 1, drop everything older.
2. **Install over a damaged record.** This spec refuses and tells the person to
   uninstall first, since treating it as "no record" would back up our own
   old files as the game's. Alternative: run the known-names uninstall
   automatically as part of the install. Recommendation: refuse (safer), with
   an "uninstall, then install" button.
3. **How is the all-users layer's source verified?** ReShade may publish no
   hash and its setup may be unsigned. Options: ship its hash in the tool
   (pinned per version, so the tool must update when ReShade does), accept
   only a version the tool knows, or never register for all users and say
   so. Recommendation: pin, and offer the all-users copy only for a pinned
   version.
4. **Other Windows users and the machine registration.** The dependents list is
   per user, the all-users registration is per machine. Options: the elevated
   run removes it only after reading every profile's list it can; or never
   removes it automatically and shows a command; or keeps a machine-level
   list under Program Files, writable only when elevated. Recommendation:
   the machine-level list, since an unelevated install never adds to it.
5. **Hold until commit costs disk and complexity.** Switching routes and
   updates keep the old files until commit (transient old plus new). The
   alternative is the reference's irreversible removal with rollback of fresh
   installs only. Recommendation: keep the design; cap it with the free-space
   check, which gather makes exact.
6. **DLSS page and the record.** A runtime this install swapped and the DLSS
   page later updates changes the file's hash, so uninstall leaves it. The DLSS
   page's update must either rewrite the entry's hash or mark it "owned by the
   DLSS page", with its `.dlss5-dlss-original` twin untouched. This crosses the
   DLSS-files spec; decide there.
7. **Close while an install waits.** Waiting for a safe point can take a few
   seconds on a large file; a person may close again. Is a hard "close
   anyway" offered, relying on the journal, or is the wait unconditional?
   Recommendation: offer "close anyway" after 10 seconds, with the record
   covering it.
8. **Cross-process safety.** Single instance of the tool (a second start
   focuses the first) versus a mutex for the settings only. Recommendation:
   single instance, plus the mutex around the record and settings for the
   command line.
9. **"Quarantined files" scope.** Read here as files an antivirus removes
   after the install wrote them. If the term meant something else (a
   deliberate holding area the tool makes for files it moved aside), the
   moved-aside section is where it would live; confirm.
10. **Verification after the first launch.** Cloud heuristics can act
    minutes or hours after a write. The "verify" operation reports it when
    asked; should the library check each installed game's files at start and
    show a banner? Recommendation: yes, a quick size check at start and the
    full hash when a game page opens.
11. **Hashing cost.** Hashing every written file at commit and at uninstall
    reads a few hundred MB per game. Acceptable, or hash only components over a
    size at install and trust the size at uninstall for those? Recommendation:
    always hash; it is local disk.
12. **Config-class list.** Which files count as rewritten at run time is a
    per-component judgment (the list above is from the reference's notes);
    each route spec must confirm its files. A file listed `config` that a
    component does not rewrite loses the hash protection for nothing.
13. **Refuse uninstall while the game runs.** The reference tried and
    reported locks. This spec refuses. If a person wants to uninstall with a
    launcher holding one DLL, the stuck path covers it; confirm refusing is
    the right default.
