# DLSS files kept current

NVIDIA's DLSS runtimes (super resolution, frame generation, ray
reconstruction) are shipped by games and then left at whatever build the
studio released, often for years. This area finds those files in every
64-bit game of the library, including games DLSS 5 was never installed
into, reads their versions, compares them with NVIDIA's newest, replaces
them on request, and puts the game's own back on request. Read from the
reference's `core/dlssupdate.py`, `core/dlss.py` (the file walk),
`core/sources.py` (NVIDIA's build list), `core/ui/ctl_dlss.py` and
`core/ui/dlsspage.py` (behaviour only), the README sections "Keeping a
game's DLSS up to date", "The dlss page" and "Is it safe", the old suite's
dlss-update section and its dlss-page check (both in the history before the
`python-reference` tag), and the 10 bug reports that show a game updated on
this page.

Related specs: [net](net.md) (downloads, archive safety) and the
game-reading and install specs (the file walk's budgets, the anti-cheat
markers, the DLSS 5 install's own backup and record). Where this spec
depends on them it says what it needs.

## What the user gets

### The three runtimes

| Family | File | Label shown |
| --- | --- | --- |
| super resolution | `nvngx_dlss.dll` | super resolution |
| frame generation | `nvngx_dlssg.dll` | frame generation |
| ray reconstruction | `nvngx_dlssd.dll` | ray reconstruction |

(`dlssupdate.py:89-97`.) Always listed in that order. Neural rendering
(`nvngx_dlssnr.dll`) is not among them: NVIDIA's SDK does not carry it
(`sources.py:1107-1110`). Streamline files (`sl.*.dll`) are never touched.

### Which games and which files are listed

- **Games.** Every library game that has an executable, is not hidden, is
  and is 64-bit. A game whose bitness is not read
  yet is neither checked nor skipped: it is read once the library knows it.
  32-bit games are counted ("N 32-bit games skipped") and never listed or
  updated, because NVIDIA publishes these runtimes for 64-bit Windows only
  (`ctl_dlss.py:55-68`, `dlssupdate.py:494`, `dlssupdate.py:705-709`).
- **Files.** Each file with one of the three names, found by the bounded
  walk the game reader uses (see below), plus the same names directly in the
  game folder and the install folder, plus every path the update record
  names (the record knows where a file this tool changed is, even when the
  walk ran out of budget before reaching it). A file counts only when its PE
  header says x64: a 32-bit runtime inside a 64-bit game is not listed
  (`dlssupdate.py:454-482`, `:505`).
- **Never listed:**
  - anything under the feeder's `host64` helper folder;
  - a runtime a DLSS 5 install wrote where the game had none (it is named in
    the install record and has no DLSS 5 backup beside it): it is ours, not
    the game's (`:503-509`, `:390-419`);
  - the second and later spellings of one file (junction, 8.3 name, absolute
    and relative): one file, one entry, keyed by its resolved path.
- **Copies.** The same runtime in two folders of one game (an engine's
  plugin folder and a storage folder) is two entries, listed as one line
  that says "2 copies" per family. An update replaces every copy; each copy
  has its own backup and record entry.
- **A game with no such file** has no row. The page says how many games
  were checked and how many 32-bit games were skipped
  (`dlsspage.py:147-178`).

### The walk (shared with game detection)

These parameters come from the reference and are what finding DLSS evidence
needs; the game-reading spec owns them. The update area needs these
properties from it:

- File names, case-insensitive: the three above (detection also looks for
  `sl.interposer.dll` and `sl.dlss.dll`; this area filters to the three).
- Depth 9 below the game folder, at most 6000 folders, and a cancellation
  token; never a wall clock, so what is found does not depend on the disk's
  speed (`dlss.py:162-164` had a 6 s limit; aligned with
  [library](library.md)'s walk).
- Folders that never hold DLLs are skipped by name (`content`, `paks`,
  `saved`, `logs`, `movies`, `sounds`, `music`, `videos`, `localization`,
  `shadercache`, `derivedcache`, `cache`, `textures`, `maps`, `levels`,
  `audio`, `data`, `assets`, `mods`, `screenshots`, `steamapps`, `redist`,
  `_commonredist`, `host64`, `reshade-shaders`) and dot-folders, except a
  skipped folder that has a direct child named `streamline`, `dlss`,
  `nvidia`, `ngx`, `nvngx`, `binaries`, `plugins`, `win64`, `x64` or `bin`,
  which is entered and then followed only through such children
  (NBA 2K27 keeps Streamline in `data\streamline`) (`dlss.py:117-161`).
- The install folder is skipped by detection (what is there is ours); this
  area adds it back by direct lookup.
- One walk per game per run is remembered; an install, an update or "check
  again" forgets it.

Changed from the reference: the walk stops after 6 matches
(`dlss.py:334`). A game with more than six matching files (several engine
plugin folders, each with three runtimes) silently shows only some of them,
and an update then leaves the others on the old build. The stop is a
detection shortcut ("one hit is enough to know the game has DLSS") and must
not apply to this area: ask for every match, bounded by the folder and time
budgets only.

Changed from the reference: when the folder budget ends the
walk, the game's row says "search stopped early: more files may exist"; the
reference only logs it (`dlss.py:301-307`). A read that may be incomplete
must say so, because "every game is on NVIDIA's newest" would otherwise be
a claim about files nobody looked at.

### Reading a version

The version of a runtime is the file version stamped in its version
resource (the fixed file info, four numbers), shown without trailing zero
parts: `310.8.0.0` reads `310.8.0` (`pe.py:63-74`). Versions compare as
numbers part by part, never as text: `310.10.0` is newer than `310.9.0`, and
`310.10.0` and `310.1.0` must not read as equal (`installer.py:1435-1445`).
A file with no readable version has the empty version: it is listed, never
called outdated, and never offered an update.

The reference reads the resource through a Windows API and returns nothing
on any other host (`pe.py:75-76`). Changed from the reference: the C#
reader parses the PE resources itself, so the unit tests read real version
stamps on macOS and Linux (see Fixtures).

### Which build is NVIDIA's newest

- NVIDIA publishes the three runtimes in its public SDK repository
  `NVIDIA/DLSS`, in the folder `lib/Windows_x86_64/rel`, at each release
  tag. The tag is found from the redirect of
  `https://github.com/NVIDIA/DLSS/releases/latest` (a HEAD request, which
  costs no API request and still answers when the 60 anonymous API calls an
  hour are spent). Each file is
  `https://raw.githubusercontent.com/NVIDIA/DLSS/<tag>/lib/Windows_x86_64/rel/<name>`
  (`sources.py:188-206`, `:1111-1157`).
- One tag supplies all three families. The build is labelled
  `<tag without v> (NVIDIA SDK)`, for example `310.9.1 (NVIDIA SDK)`: the
  label says which SDK, because NVIDIA versions each runtime separately and
  the label must not claim to be each file's own version
  (`sources.py:1137-1140`).
- An unreachable redirect gives no list. That answer is never remembered, so
  one blip does not hide NVIDIA's builds for the rest of the session
  (`sources.py:1131-1135`).
- The newest is remembered for the run once it has an answer. **check
  again** asks again, so a window left open for days sees a build published
  meanwhile. What was known stays until a new answer replaces it, so an
  update right after a failed check still has a build to take
  (`dlssupdate.py:564-601`).
- The last known newest is saved with the scan. When NVIDIA cannot be
  reached, rows are still shown with the last known newest and the band says
  "offline: newest from an earlier check". With no earlier answer the page
  says "the newest builds could not be read - versions are shown as found",
  claims no game is behind, and an update is refused with: "NVIDIA's build
  list could not be read. Check the connection and try again."
  (`ctl_dlss.py:457-462`, `dlsspage.py:176-181`, `dlssupdate.py:965-972`).

### Is a file behind

A file is outdated, and only then offered an update, when all of these hold
(`dlssupdate.py:604-612`):

1. there is a newest build for its family;
2. its state is not "set by the DLSS 5 install" and not "missing";
3. it has a readable version;
4. it was not written from this very build (its recorded label equals the
   newest's label), and
5. its version is lower than the newest build's number.

A version equal or higher is current: a game that ships a newer preview
build is never downgraded. Rule 4 exists because NVIDIA versions each
runtime on its own: a file written from this build can stamp lower than the
SDK tag and must not read as behind forever.

Changed from the reference: rule 5 compares a file with the SDK tag
number, not with the version of the file NVIDIA publishes for that family.
Rule 4 patches the symptom for files this tool wrote; a file copied by
another tool (or by the game's own patch), whose stamp reads below the tag
though it is NVIDIA's current file, is offered an update again and again.
Compare with the published file's own stamped version per family. The
stamp is read once per NVIDIA tag: the three files of a new tag are
downloaded once (they are needed for the update anyway), their versions are
read, and the result is cached by tag in the per-user cache ([net](net.md)),
so a check makes no further download until NVIDIA publishes a new tag.

### Update, one game

Pressing update on a game's row, or the game page's line, replaces the
game's outdated runtimes with NVIDIA's newest, family by family in the
table's order. Steps, in order; the first failing step stops the game's
update and says why (`dlssupdate.py:943-1015`, `:810-940`):

1. **Guards.**
   - Not a 64-bit game: "Only 64-bit games are offered: NVIDIA publishes
     these runtimes for 64-bit Windows."
   - A process is running out of the game's folder (or its install folder),
     children of such a process included: "`<name>` is running from this
     game's folder. Close it first - Windows does not let anything replace a
     file a running program has open." Refusal kind: running.
   - Anti-cheat present and not allowed: see Online games.
2. **Reconcile** the record with the disk (next section). Its notes are
   part of the report.
3. **Read the game's files live** (not the cached row) and keep the
   requested families (a game's update takes every outdated family unless
   the caller limits it). Nothing found: "No 64-bit DLSS runtime of this
   game was found to update." (kind: nothing).
4. **Fetch NVIDIA's newest.** None: the offline message above (kind:
   offline).
5. **Per file**, in this order:
   - "set by the DLSS 5 install" is skipped: "set by the DLSS 5 install -
     change it in the game's settings";
   - "missing" is skipped: "the file is gone - restore original puts the
     game's own back";
   - no newest for the family: "NVIDIA publishes no build of it";
   - not outdated: "`<version>` is current";
   - otherwise replace it (below).
6. **Replace one file.**
   1. **Write probe** in the file's own folder: a file can be created there.
      A refusal: "Windows refused to write into `<folder>` (`<reason>`)."
      followed by, for an Xbox or Game Pass folder (`C:\XboxGames`,
      `WindowsApps`), the text that no tool can change that folder unless the
      game's publisher allows mods and the Steam version is the one that
      can be set up; for any other folder: "Close the game and its
      launcher; if it is still refused, the folder needs administrator
      rights." (kind: readonly; `games.py:83-91`, `:126`).
   2. **Download** NVIDIA's file through the shared download path (see
      [net](net.md)). Failure: "`<file>` could not be downloaded: `<first
      line of the cause>`. Nothing was changed." (kind: offline.)
   3. **Check the download.** It must be a 64-bit Windows DLL of at least
      200,000 bytes (an HTML error page, a cut transfer and a 32-bit file
      fail), and its size must equal the published size when one is known
      (`installer.py:1100-1135`). A bad download is deleted and nothing is
      changed.
   4. **Re-read the target.** Another writer may have been at it during the
      download: the file must still exist, still have the version the scan
      saw, and still have no DLSS 5 backup beside it. Otherwise: "`<file>`
      changed while the update was downloading (`<old>` -> `<now or gone>`).
      Nothing was changed; check again."
   5. **Decide the backup** (table below), make it, then write the new
      build beside the file and rename it over the file, so the file is
      never half written.
   6. **Record** the write (below). A failed record write is logged and does
      not undo the update: the backup beside the file still makes it
      restorable and a scan adopts it.
   7. The report line is `<family label>  <old> -> <new>`, plus "(the file
      that was there is kept as `<name>`)" when a file was set aside.
7. A failure after some runtimes went through says which went through and
   drops the "Nothing was changed" sentence, because it is untrue
   (`dlssupdate.py:160-161`, `:1007`). The page shows "super resolution
   updated; `<reason>`".

#### Backup decision at update time

"Ours" means: the record has an entry for the file, and the file's SHA-256
equals the hash the record holds for what was written.

| Disk now | Backup beside (`.dlss5-dlss-original`) | Action |
| --- | --- | --- |
| ours | yes | keep the backup; overwrite the file |
| ours | no | no backup is made: filing our own build as the game's would let a later restore put it over a genuine file. The record keeps the game's version; restore later says the backup is gone. Log a warning. |
| not ours | no | back up the file as the game's own (copy, check the copy, keep the original in place until the new build is written); record the game's version as original |
| not ours | yes, and the file is a byte-identical copy of the backup (a crash between backup and swap) | nothing to set aside; overwrite; the backup stands |
| not ours | yes, and the file differs from the backup (the record was lost, or something changed the file) | set the file aside as `<runtime>.dlss5-dlss-displaced-<unix time>[-<n>]`, never over another file; overwrite; original is the backup's version |

A record entry that names the game's own build is not trusted to withhold a
backup: a backup is withheld only when the bytes match the recorded hash
(`dlssupdate.py:39-47`, `:854-899`). If writing the new file fails after
the backup or the set-aside, both are undone (the set-aside file renamed
back, a backup made by this attempt deleted) and the error is "Windows
refused to replace `<path>` (`<reason>`). Nothing was changed. Close the
game and its launcher; a read-only file needs its attribute cleared."
(kind: readonly). A refusal of the backup copy itself: "the game's own
`<file>` could not be backed up (`<reason>`); nothing was changed."

Changed from the reference: "ours" is decided by the recorded hash
everywhere. The reference uses version plus size to recognise its own file
when keeping a backup, when settling and when restoring
(`dlssupdate.py:378-384`, `:854`, `:756`, `:1063`, `:1104`) and the hash only
when withholding a backup (`:364-375`). Two files of equal version and size
but different bytes (a modded or re-signed copy) were therefore treated as
ours, and could be overwritten, or deleted as a "duplicate backup", without
being kept. There are no old records to stay compatible with, so a record
entry without a hash is invalid.

Changed from the reference: the copies are verified by hash, not by size
alone (`dlssupdate.py:796-806`), and a leftover `*.dlss5-dlss-part` file
from a crash is removed at the next update or restore of that folder (the
reference leaves it).

### The record

`dlss5-dlss-update.json` in the game's root folder (`dlssupdate.py:99`,
`:338-357`). Written atomically (write beside, flush to disk, rename);
removed when it has no entries.

| Field | Meaning |
| --- | --- |
| `tool`, `kind`, `version` | `dlss5-autopilot`, `dlss-update`, `1` |
| `files[].path` | the file's path relative to the game folder, `/` separators, one entry per file |
| `files[].family` | `dlss`, `dlssg` or `dlssd` |
| `files[].original` | the game's own version |
| `files[].written` | the version of what was written (read back from the written file, else the label's number) |
| `files[].size` | its size in bytes |
| `files[].sha256` | its SHA-256, taken right after writing |
| `files[].label` | the build it came from, e.g. `310.9.1 (NVIDIA SDK)` |
| `files[].at` | seconds since 1970 |

The game's own file is kept beside the runtime as
`<name>.dlss5-dlss-original`.

Everything read from the record is untrusted (it sits in a folder other
tools write into): an entry is used only when its path resolves inside the
game folder (an absolute path is accepted only inside the game's install
folder) and its file name is one of the three runtimes for its family. The
backup path is derived from the file path, never read from the record.
Entries that fail are ignored and a warning is logged. A record that is not
JSON reads as empty. Duplicates (one file, two spellings) keep the first
(`dlssupdate.py:270-335`).

A record in a folder only an administrator can write fails to be saved;
the backup beside the file keeps the change restorable. A game folder that
is moved takes record and backups with it, which is why they live there and
not in the user's data folder.

### Why two suffixes

`.dlss5-dlss-original` is deliberately not the DLSS 5 install's
`.dlss5-autopilot-backup`. The install's uninstall restores every backup of
its suffix, and reads one beside a runtime as "this install swapped it".
With separate suffixes the two writers stack and each takes back only its
own layer:

```
the game's own  ->  DLSS update  ->  DLSS 5 install swap
                    (.dlss5-dlss-original)  (.dlss5-autopilot-backup)
```

Kept from the reference, with its reasoning (`dlssupdate.py:16-26`). The
install area must therefore never treat `.dlss5-dlss-original`,
`.dlss5-dlss-displaced-*` or `dlss5-dlss-update.json` as its own
(`installer.py:307`). This area reads the DLSS 5 install only through
[install](install.md)'s contract, and never reads legacy record names or
suffixes.

### The states of a file

Each listed file is in exactly one state (`dlssupdate.py:107`,
`:485-556`):

| State | When | Offered |
| --- | --- | --- |
| original | the game's own file: no record entry matches, and no backup beside it that differs from it. A backup of the same build beside it is also "original" (a crash between backup and swap) | update, when outdated |
| updated | the file is the build this tool wrote (hash equals the record), or there is a backup beside a file that is not ours (the record is gone, or something changed the file: restorable; restore sets the file aside) | update when outdated; restore original |
| install | a DLSS 5 install's backup sits beside the file (even when the install wrote the very build this tool wrote before it) | nothing here: "set by the dlss 5 install" and a link to the game's settings |
| missing | a runtime this tool replaced is gone, and its `.dlss5-dlss-original` is still there | restore original |

Only files with a record entry are hashed to decide "ours" (a few per
game); a file with no entry is never hashed by a read.

### Reconciling the record with the disk

Run at the start of every update and restore, never during a read: a read
runs beside installs and other jobs and reconciling changes files
(`dlssupdate.py:732-793`, `ctl_dlss.py:264-265`). A game this tool never
touched is only read. Each record entry:

| Disk | Result |
| --- | --- |
| file gone, backup there | keep the entry: restore puts the game's own back |
| file gone, no backup | drop the entry; "`<name>` is no longer at `<path>`" |
| file is ours, or a DLSS 5 backup is beside it | keep |
| file not ours, backup there, file byte-identical to the backup (a launcher verified its files and put the game's own back) | delete the now duplicate backup, drop the entry; "the game's own `<version>` is back (a launcher verified its files)" |
| file not ours, backup there, any other file (the game patched itself; another tool wrote a newer or even our own build) | keep entry and backup; "`<name>`: changed to `<version>` since the update; restore original still puts the game's own `<old>` back" |
| file not ours, no backup | drop the entry; "changed to `<version>` by something else - left as it is" |

Changed from the reference: "byte-identical" is a hash comparison. The
reference's own invariant says "exact copy" but compares version and size
(`dlssupdate.py:761`, `:231-236`) before deleting a backup.

### Restore original

Puts the game's own runtimes back and drops the record entries
(`dlssupdate.py:1018-1143`). It is driven by the record and the backups on
disk, not by the scan, so a runtime that is gone, unreadable or no longer
64-bit still has its original put back. The targets are every record entry
and every file with a `.dlss5-dlss-original` beside it, optionally limited
to some families. No target: nothing happens. A running game is refused
("`<name>` is running from this game's folder. Close it first."). Restore
has no anti-cheat guard (it puts the game's file back) and no 32-bit guard.

Per target, in order of family then path:

| Disk | Action | Report |
| --- | --- | --- |
| a DLSS 5 install covers it (its backup is beside it, or its record lists the path) | refused for this file; nothing moves; keep entry and backup | "DLSS 5 is installed over `<name>` - remove DLSS 5 first, then restore original" |
| no backup of ours, file there | file stays; drop the entry | note: "the backup of the game's own `<name>` is gone - the `<version>` stays. Verify the game's files in its launcher to get the original back." |
| no backup, no file | drop the entry | note: "`<name>` and its backup are both gone. Verify the game's files in its launcher to get it back." |
| backup there | if a file is there that is neither ours nor identical to the backup, set it aside as `.dlss5-dlss-displaced-<time>`; copy the backup over the file atomically; delete the backup; drop the entry | done: `<family>  <was or missing> -> <restored version>`; note when a file was set aside: "the `<version>` that was there is kept as `<name>`" |

A failed copy puts a set-aside file back and stops the restore, keeping the
backup: "Windows refused to replace `<path>` (`<reason>`). Nothing was
changed. Close the game and its launcher." The record is saved after each
target, so a stop leaves the finished ones recorded as finished. A record
that cannot be saved is a note ("the record could not be written").

### How a DLSS 5 install and the update stack

(`dlssupdate.py:43-58`, old suite section 12958-13383.) The DLSS 5 install
backs up "what was there", whoever wrote it.

| Order | Result |
| --- | --- |
| update, then DLSS 5 install over the file | the install backs up our build as its backup; our backup still holds the game's own; the file reads "install" and is not updated here |
| ...then DLSS 5 uninstall | the install's backup (our build) is restored: the update stays in place; the file reads "updated" again, and restore original then brings the game's own |
| ...instead, restore here while DLSS 5 is installed on top | refused: "remove DLSS 5 first". The install recorded the hash of the file it found (our build) and its uninstall puts that file back, so restore original works afterwards |
| DLSS 5 install swapped the file first (the game's own is its backup), then an update is pressed | refused for that file: "set by the DLSS 5 install"; after the DLSS 5 uninstall it can be updated here |
| DLSS 5 install of the same build over a page update | still reads "install"; restore here is refused until DLSS 5 is removed |
| DLSS 5 uninstall with no install record at all | restores only backups of its own suffix, so it leaves a runtime this page updated |
| DLSS 5 install adds a runtime the game never had | not listed here (ours) |

After a DLSS 5 install, uninstall, autopilot run or library-wide update,
the affected game is read again (queued, one at a time, a game not queued
twice) so its row and its page do not show the build from before.

### Online games and anti-cheat

Both a changed DLSS file and a swap by a launcher's verify can trip an
anti-cheat; for anything played online the README's advice is to leave the
game's own files alone (README "Keeping a game's DLSS up to date").

- **Detection** is [library](library.md)'s one detector, by files in and
  around the game and install folders (BattlEye, Easy Anti-Cheat, Riot
  Vanguard, GameGuard, XIGNCODE3, Denuvo Anti-Cheat, PunkBuster, FACEIT AC,
  Ricochet including Call of Duty's `cod.exe`, EA Javelin, ...). It returns
  the product name and the files it found. It **fails closed**: a folder it
  cannot read is treated as an anti-cheat present, as in
  [watch](watch.md). Changed from the reference: a detection failure read as
  no anti-cheat (`dlssupdate.py:666-679`).
- **update all never takes** a game with an anti-cheat.
- **Updating one game by hand** asks first, naming the product and the files
  it rests on: "`<product>` - update anyway?" / "`<game>` ships `<product>`
  (`<files>`). An anti-cheat can read a changed DLSS file as tampering, and
  online that can mean a ban. The game's own files are kept either way."
  with the buttons "update anyway" (danger style) and "cancel". The
  guard refuses the update with "`<product>` is installed with this game. An
  anti-cheat can treat a changed DLSS file as tampering; for anything played
  online, keep the game's own." when the caller did not allow it, so a path
  that skips the question cannot skip the guard (kind: anticheat).
- **Keep its own.** A per-game switch ("keep its own" under the game's
  update; "allow updates" takes it back), stored by the game folder in the
  user's settings. update all leaves such a game out; updating it by hand
  asks "`<game>` is kept as it is - update anyway?" ("You marked this game to
  keep its own DLSS files, so update all leaves it out. The game's own files
  are kept beside the new ones either way."). Anti-cheat takes precedence
  over this question when both apply. This switch exists because a game can
  be online with no file the tool can recognise (a Black Ops 7 update all
  went through before the marker `cod.exe` was known, #856).
- A game's page line for an anti-cheat or kept game is drawn without the
  accent: it is information, not a suggestion.
- Hidden games are listed nowhere on this page and update all never changes
  their files.

### Update all

`dlss_todo` = rows that are behind, with no anti-cheat, not kept, not
running (as of the last process snapshot), not hidden
(`ctl_dlss.py:153-159`). Update all stays opt-out (every game behind, unless
kept), because the anti-cheat detector fails closed: a game whose folder
cannot be read is left out like one with an anti-cheat. Behaviour:

- The button reads "update all (N)" and is absent when N is 0.
- A confirmation lists up to four names and "and N more": "update dlss in N
  games?" / "`<names>`. Each game's own files are kept beside the new ones;
  restore original puts them back." with "update" and "cancel".
- Games are updated one after another, never in parallel, under the same
  job queue installs, autopilot and uninstall wait in ([platform](platform.md),
  "Jobs"): two writers of one runtime at once is how the game's own file gets
  lost. A job that waits for another says so in the status line ("waiting:
  update all").
- Each game runs the whole single-game update above, so every guard
  applies again at that moment: a game that started running meanwhile is
  refused and counted as failed.
- A game that finished with nothing to do (everything current by then) is
  not counted. The status line ends "dlss: N updated" and, when some
  failed, ", M failed - `<up to three names>`" (`ctl_dlss.py:706-716`).

### The page and the game page

- **Page rows** are ordered once per scan, then kept (a row that jumps
  while its update runs loses the person's place): first the games that can
  be updated now, then games that are behind but need a look (anti-cheat,
  running, kept), then the rest, each by name; new rows after the ordered
  ones (`ctl_dlss.py:129-151`).
- **Header** says "N games behind NVIDIA's newest" with "N to update now, M
  need a look first", or "every game is on NVIDIA's newest", or the offline
  texts above. The band says "newest from nvidia: `<versions>`", how many
  games were checked and how long ago, how many 32-bit games were skipped,
  and "offline: newest from an earlier check" when it applies.
- **Empty states:** no library ("no games yet" and a way to the games);
  never read ("not checked yet" and "check my games"); reading ("reading N
  of M - `<game>`"); no 64-bit games ("no 64-bit games in the library");
  none found ("none of the N games checked ship NVIDIA DLSS files").
- **A row** shows, per runtime, `<version>  ->  <newest>` with the family
  label; "updated" with "the game's own `<version>` kept"; "running:
  `<process>`"; the anti-cheat name; "kept as it is"; "set by the dlss 5
  install" with a link that opens the game's page; "missing"; "N copies".
  A failed update leaves its reason on the row, with the game's folder
  replaced by "the game folder" in the text; a failure after a partial
  success starts with "`<families>` updated;".
- **The game's page** carries a line while the game is behind:
  `<first behind family> <version> -> <newest> [and N more] [- <anti-cheat> |
  - kept as it is]`, where N counts the other behind families (families, not
  files) (`ctl_dlss.py:172-196`). Pressing it shows the dlss page and runs
  the update; when a scan of every game is running, the update waits for the
  scan to end instead of being dropped (`:546-557`, `:489-496`).
- Progress of a download is reported as a percentage, at most about every
  0.15 s.
- A job that raises while its answer is shown still ends the job and clears
  the busy flag; otherwise a "working" row would block every later job
  (`ctl_dlss.py:626-640`).

### Stale reads

What the page shows is the last read, and a read goes stale. The rules:

- **Writes never trust a stored row.** The rows are for display. Update and
  restore read the files live just before acting, and replace-time checks
  (above) read the target again after the download. "Behind" in the cache
  never authorises a write by itself.
- **When games are read.**
  - A few seconds after the window opens, quietly, when the last full read is
    older than 24 hours or the library has games not read yet (a game whose
    bitness is not known is waited for). If the library scan or an install
    is running, try again 15 s later (`ctl_dlss.py:203-260`).
  - When the page is opened under the same conditions.
  - On **check again**: every game, with the newest builds asked for again
    and every walk forgotten.
  - A read started only because the library has new games reads those games
    and the games whose folder changed since their last read; it keeps the
    time of the last full read, so the day-old read still comes when due.
  - A read does not start while another job holds the busy flag, nor while
    the library is being read (a game mid-enrichment has no bitness and was
    once counted "32-bit skipped" for a day); it starts when that ends,
    once, however often it was asked.
- **After a change** the affected game is read again at once (its own job,
  an install, an uninstall), not at the next day-old read.
- **Rows read while a scan runs** go on top of the scan's answer, which may
  have read that game before its files changed.
- **Running games** are re-checked from one process snapshot (shared by all
  games, never one per game) when the page is shown, so "running:" does not
  outlive the game.
- **Saved read.** `dlss-scan.json` in the settings folder holds the last read
  so the page opens with rows at once and is usable while a new scan runs.
  It holds the time, NVIDIA's newest per family (label and version), per
  game folder its entries (path, family, version, state, original, label,
  whether a backup exists), anti-cheat and its files, the running process,
  a stamp, the counts of 32-bit games and of games seen, the row order and an
  offline flag. It is written atomically; a file that is not that shape reads
  as empty (`dlssupdate.py:1149-1170`).

Changed from the reference: the stamp that decides whether a game is read
again is the newest modification time of the game folder and the install
folder themselves (`ctl_dlss.py:71-83`). A directory's time does not move
when a file deep inside it is replaced, which is what a launcher's verify
does to `Engine\Plugins\...\nvngx_dlss.dll`. Such a change stays unseen
until the day-old read. Keep a stamp per runtime file instead (path, size,
last write time of each file the read listed) and compare those few files on
a partial read and when the page is shown; a mismatch re-reads that game.

### Online and offline

The only hosts this area talks to are `github.com` (the tag redirect),
`raw.githubusercontent.com` (the files) and, through the shared path, the
API fallbacks. No game name or path is sent anywhere.

## What must never happen

- The game's own runtime is lost. At every moment of every operation the
  game's own file exists as the runtime, as our `.dlss5-dlss-original`, as
  the DLSS 5 install's backup, or set aside under a suffix of ours. Nothing
  deletes a runtime that is not a build this tool wrote (by hash), or a
  backup that is not an exact copy of the file on disk.
- A backup is never overwritten, and never made from our own build.
- A file the DLSS 5 install swapped is not touched by an update; an
  install's backup is changed only by the hand-over in restore, and only
  when it holds our build.
- A running game's files are never replaced.
- An update never starts without its guards and the post-download re-read,
  and never half-writes a runtime (write beside, then rename).
- A download that is not a 64-bit DLL, or not NVIDIA's, never reaches a
  game folder.
- A record entry never leads outside the game folder, never names another
  file than the three runtimes, and never supplies a backup path.
- Update all never takes an anti-cheat game, a kept game, a hidden game or a
  running game.
- A write failure never leaves a partial state without saying: either
  nothing changed, or the report says what went through.
- Two jobs never write at once.
- "Nothing was changed" is never said after something was.
- Reading never writes (no reconciling, no cleanup of backups, no record
  writes), apart from the saved read itself.

## Requirements added by the takeover and the rewrite

- **Verify what is written into a game.** A download is replaced only after
  it passes: the DLL checks above, and on Windows an Authenticode check that
  the file is signed by NVIDIA Corporation with a valid chain (an interface,
  so the test host supplies a fake). A cached download is checked again
  before use, not trusted for having been fetched once. The reference checks
  shape only, and `size` is always unknown (zero) for these files, so even
  the published-size check never runs (`sources.py:1150`,
  `dlssupdate.py:826-831`).
- **A size ceiling** of 256 MiB for one runtime (the largest published
  runtime plus a wide margin), enforced while streaming (see [net](net.md)).
- **Unsigned is refused.** A runtime not signed by NVIDIA Corporation never
  reaches a game folder, with: "`<file>` is not signed by NVIDIA - refusing
  to install it." A warning would still put an unverified DLL into a game.
- **Pin to the commit.** The file URL uses the tag the redirect named; a
  moved tag would change what a build means. Resolve the tag once and use
  that value for all three files of one answer (as the reference does) and
  for the cache key, so two files of one answer never come from different
  tags.

## Bugs and questionable choices in the reference not to port

- `dlssupdate.py:604-612`: the version rule compares file stamps with the
  SDK tag; see Is a file behind.
- `dlss.py:334`: the walk stops after 6 matches, hiding files from an
  update; `dlss.py:301-307`: an exhausted budget is only logged.
- `dlssupdate.py:378-384`, `:231-236`: ownership by version and size.
- `dlssupdate.py:796-806`: the copy is checked by size only.
- `ctl_dlss.py:71-83`: the stale-read stamp is a directory time.
- `dlssupdate.py:438-447`: detection's walk cache is reached into by its
  private key tuple to avoid a second walk. A shared walk result with a
  named interface replaces the reach-in.
- `dlssupdate.py:560-601`: the newest is kept in two process-wide caches,
  one in each module; "refresh" resets the other module's cache by hand
  (`:579`). One owner, one cache, one refresh.
- `dlssupdate.py:989-1000`: the update can take a specific older build
  (`build`), but nothing in the reference calls it with one. Dead: not
  ported. There is no "step back one build": restore original is the way
  back, and one way back is easier to keep correct.
- `dlssupdate.py:943-950`: `update` takes a `catalog` argument only so tests
  can pass one; the C# takes NVIDIA's list through an interface.
- `dlssupdate.py:160-161`: the "Nothing was changed" sentence is removed
  from an error text with a regular expression after the fact. Build the
  error from parts.
- `dlssupdate.py:1010`: after the first refused runtime of a game the other
  runtimes are not tried. Kept deliberately (the usual cause, a running
  game or a locked folder, is shared), and said on the row: "`<reason>`;
  the other runtimes were not tried".
- `dlssupdate.py:837`, `:846-849`: two of the failures raise with no refusal
  kind, so the page cannot draw them as their own state; give every refusal a
  kind.
- `dlssupdate.py:682-691`: the write probe creates a file in the game
  folder; a crash leaves `.dlss5-dlss-write-test-*`. Remove leftovers with
  the part files.
- `ctl_dlss.py:55-68`: library "kind" and hidden-set are read from the UI
  controller; the game list must come from the library service so the
  engine (and its tests) do not need the window.

## To verify against primary sources

| Claim | Source that settles it |
| --- | --- |
| NVIDIA publishes `nvngx_dlss.dll`, `nvngx_dlssd.dll`, `nvngx_dlssg.dll` at `lib/Windows_x86_64/rel` of the `NVIDIA/DLSS` repository at each release tag, as plain files (not Git LFS pointers) | the repository tree and `raw.githubusercontent.com` answer for one tag; its README and licence |
| NVIDIA versions each runtime separately: whether a runtime's stamped file version can read lower than the tag, and by how much, for each family | the version resources of the three files at several tags; the SDK's release notes and version table |
| How to read a published file's version without downloading all three files (a ranged read of the resource section, a manifest in the repository, the release notes) | the repository; GitHub raw Range support |
| The published files are Authenticode-signed with a stable subject (`NVIDIA Corporation`) | `Get-AuthenticodeSignature` on the files; NVIDIA's signing documentation |
| Frame generation and ray reconstruction runtimes can be swapped independently of the game's Streamline plugin (`sl.dlss_g.dll`, `sl.dlss_d.dll`, interposer) without breaking the game | the Streamline programming guide's compatibility rules; reports of swaps (DLSS Swapper's notes); the 10 reports in the corpus that show swapped frame-generation files (1014, 289, 497, 579, 723, ...) |
| `https://github.com/<repo>/releases/latest` redirects to the newest non-prerelease, non-draft release by creation date, and a HEAD request is not counted against the API limit | GitHub docs on releases and rate limits |
| Windows reads the fixed file info's four numbers as NVIDIA's displayed version (and a string `FileVersion` can differ) | the version resource of real NVIDIA files; `VS_FIXEDFILEINFO` documentation |
| A running game, any process holding the DLL open, blocks replacing or renaming the file; a launcher's verify does not delete unknown files such as `*.dlss5-dlss-original` (Steam, Epic, GOG, EA, Ubisoft, Xbox) | each launcher's verify behaviour, tested; Windows file-sharing documentation |
| Anti-cheat treatment of a changed `nvngx_*.dll` per product | each vendor's FAQ; reports |
| Xbox and Game Pass folders refuse writes unless the publisher allows mods | Xbox app documentation for "Enable mods" |

## Fixtures

- **Runtime files with real version stamps:** the reference's `tests/fake_pe.py`
  (a PE32+ or PE32 with one `.rsrc` section holding a version resource,
  padded to a runtime's size, machine 0x8664 or 0x14C). Port it as a
  generator in the test project and keep the generated files out of the
  repository.
- **NVIDIA's list:** a tag answer `v310.9.1` and a catalog entry per family
  (`dlss`, `dlssd`, `dlssg`) with the label `310.9.1 (NVIDIA SDK)` and a raw
  URL; one answer with the tag `v310.9.16` (report 1014); the empty answer
  (offline). The old dlss-page check built these (`cat()` in
  `_tools/dlss_page_check.py`, history at `17d8a94~1`).
- **Game folders** the old page check built: Alpha Unreal (nested
  `Engine/Plugins/Runtime/Nvidia/DLSS/Binaries/ThirdParty/Win64/nvngx_dlss.dll`
  3.7.20 and `nvngx_dlssg.dll` 3.5.0 beside the executable), Bravo Current
  (310.9.1), Charlie Online (EasyAntiCheat folder, a read-only file), Delta
  Running (a running process), Echo 32-bit, Foxtrot no DLSS, Golf Storage
  (two copies of one build), Hotel Installed (a DLSS 5 manifest plus
  `nvngx_dlss.dll.dlss5-autopilot-backup`), Lima Partial (second runtime
  refused).
- **The old suite's dlss-update section** (`test_all.py` lines 12958-13383
  at `17d8a94~1`): about 45 checks, one per row of the stacking and
  reconciling tables above; each becomes a behaviour test.
- **Record files:** a good record; one with entries outside the folder, naming
  another file, a family that does not match and a non-JSON file; a record
  that names the game's own build (not trusted); a record with a missing
  hash (invalid in the C#).
- **Real corpus lines:** the 10 reports listed above have the page's "updated
  on the dlss page to 310.9.1 (NVIDIA SDK); the game's own 3.7.0 kept beside
  it" evidence lines, in `_tools/reports/` (1014, 289, 275, 412, 461, 497,
  515, 579, 311, 723). The diagnosis area owns their text; this area
  supplies the facts (file path, original and written versions, label).
- **Anti-cheat folders:** from the diagnosis area's marker fixtures.

## Open questions

1. **Frame generation and ray reconstruction:** offer them by default or
   only super resolution by default, with the others behind a confirmation
   naming the risk, until the Streamline compatibility rule is verified?
2. **Scan scope:** include folders the user added by hand and emulator
   entries? (The reference lists whatever the library holds with an
   executable.)
