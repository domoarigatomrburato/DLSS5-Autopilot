# The game library

The library is the list of games the person can set up: what every store and
launcher says is installed, game folders people keep outside any launcher,
the 19 emulators, and folders added by hand. For each one the tool decides
which executable is the game, reads it (see [pe](pe.md)), shows a cover and
says up front when an anti-cheat or an engine that crashes ReShade is
involved. The result is kept between launches. This spec covers finding the
games, choosing the executable, the library cache, cover art, anti-cheat and
RE Engine detection, emulators and the search. Watching an installed game
is [watch](watch.md)'s. Read from the reference's
`core/games.py`, `core/library.py`, `core/emulators.py`, `core/covers.py`,
`core/anticheat.py`, `core/reengine.py`, `core/refw.py`,
`core/pe.py` (executable choice), `core/ui/art.py` and
`core/ui/ctl_library.py` (scan flow, search, hiding), and from tests written
against them.

## What the user gets

### One entry per game

A library entry holds:

| Field | Meaning |
| --- | --- |
| name | what the store calls it, else the folder name (see Names) |
| folder | the game's root folder |
| source | `Steam`, `Epic`, `GOG`, `EA`, `Ubisoft`, `Battle.net`, `Rockstar`, `Amazon`, `Heroic`, `Xbox`, `Folder`, `Emulator`, `Manual` |
| exe | the chosen executable, or none |
| candidates | every executable found, best first |
| bitness | 32 or 64, or unknown |
| api, reason, detected api | the graphics API in use, why, and what the executable said before any override |
| error | why the executable could not be read; empty when fine |
| protected-executable notice | set when Windows refuses to open the executable (see [pe](pe.md)) |
| install root | the folder an earlier install of ours wrote to, when it differs from the executable's folder |
| emulator profile | when the executable is a known emulator |

- **Install folder.** Files go beside the executable, not in the game's root:
  Kingdom Come 2 lives in `Bin\Win64MasterMasterSteamPGO\KingdomCome.exe`
  and a ReShade proxy elsewhere is never loaded. Once an install has
  happened, the folder it wrote to wins (see Previous install below).
- **Installed** is read through [install](install.md)'s typed contract,
  never from the record's file or field names:
  - `Installed`: installed;
  - `Installing` or `Removing`: an operation was interrupted; the entry
    shows as installed, says so, and the game page offers to finish it;
  - `Unreadable`: installed, with "install record damaged"; uninstall runs
    install's known-names cleanup;
  - no record: not installed. A loose add-on is not an install: a Downloads
    folder full of hand-fetched components used to show as installed
    (`core/games.py:192-221`, `core/games.py:34-64`).

  Changed from the reference: it also took "one of our add-ons beside a
  loader" as installed with no record, and read a record holding only a
  "rolled back" note as not installed. A rollback now leaves no record, and
  without one nothing says which files are ours.

### Names

- A store's own name wins. Steam: the manifest's `name`, else the folder
  name. Epic: `DisplayName`, else the folder name. GOG: `gameName`. The
  registry stores that give none: the folder name. Rockstar: the key name.
- A game added by hand takes the nearest folder name going up from the
  executable that is not a generic one: `bin`, `bin64`, `binaries`, `win64`,
  `win32`, `x64`, `x86`, `retail`, `shipping`, `release`, `system`, `exe`,
  `executable`. So `D:\Games\World War Z\bin` is "World War Z", `Deus Ex\System`
  is "Deus Ex", and `Ghostwire\Snowfall\Binaries\Win64` is "Snowfall" (the
  nearest non-generic folder, which is the project folder, not the game's
  root; `test_all.py:7088-7095`). `Game`, `Content` and `Engine` are not
  generic: they can be a real game's own folder (`core/games.py:713-740`).
  Questionable: an Xbox folder `...\Content` added by hand is then named
  "Content" (report 897); see Open questions.
- A manually added executable that is a known emulator is named
  `<profile name> (<system>)`.

### Where games come from

Each store is independent: one failing is logged and reported ("<store> could
not be read: <error type>") and the others run (`core/games.py:1128-1141`). A
source that finds nothing is not an error. The scan reads files and the
registry only; there is no network access (`core/games.py:3-4`).

| Source | Where it looks | What it takes | Filter | Evidence |
| --- | --- | --- | --- | --- |
| Steam | The Steam root: registry `HKCU\Software\Valve\Steam` values `SteamPath`, `InstallPath`; then `HKLM\SOFTWARE\WOW6432Node\Valve\Steam`; first existing directory; then `C:\Program Files (x86)\Steam`, `C:\Program Files\Steam`. Libraries: the root plus every `path` in `steamapps\libraryfolders.vdf`. Games: subfolders of `<library>\steamapps\common` | name and `installdir` from `appmanifest_*.acf` | see Steam rules | `core/games.py:231-338` |
| Epic | `%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\*.item` (JSON) | `DisplayName`, `InstallLocation`, `LaunchExecutable` | skip `bIsApplication` or `bIsExecutable` false (engine plugins, Unreal Engine itself, content packs; Quixel Bridge's location was the whole engine tree); older manifests without the flags stay; location must be a directory; the launch executable is the pick only if the file exists | `core/games.py:358-392`, `test_scan.py:47-72` |
| GOG | `HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\<id>` and `HKLM\SOFTWARE\GOG.com\Games\<id>` | values `path`, `gameName` | path must be a directory | `core/games.py:398-424` |
| EA app / Origin | `HKLM\SOFTWARE\WOW6432Node\Electronic Arts\<x>` and `HKLM\SOFTWARE\Electronic Arts\<x>`, value `Install Dir`; plus every subfolder of `C:\Program Files\EA Games` and `C:\Program Files (x86)\EA Games` | folder name | directory must exist | `core/games.py:465-496` |
| Ubisoft Connect | `HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\<id>`, value `InstallDir` | folder name | directory must exist | `core/games.py:499-513` |
| Battle.net | `HKLM` and `HKCU` `SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall`, subkeys whose name contains `battle.net` (any case); values `InstallLocation`, `DisplayName` | display name | directory must exist | `core/games.py:516-548`; see the bugs |
| Rockstar | `HKLM\SOFTWARE\WOW6432Node\Rockstar Games\<title>` and `HKLM\SOFTWARE\Rockstar Games\<title>`, value `InstallFolder` | the key name | skip keys `launcher`, `rockstar games launcher`, `rockstar games social club`, `social club` (these register an install folder and are not games) | `core/games.py:596-631` |
| Xbox / Game Pass | for drive letters C to H: `X:\Program Files\ModifiableWindowsApps` and `X:\XboxGames`: every subfolder; the game folder is its `Content` subfolder when there is one (XboxGames puts the files one level down) | folder name | skip `GameSave` (cloud saves in empty containers) and `Minecraft Launcher` (a large runtime tree, not a game), any case | `core/games.py:551-588` |
| Amazon Games | `%LOCALAPPDATA%\Amazon Games\Library\*`, and `X:\Amazon Games\Library\*` for C to H | folder name | | `core/games.py:634-653` |
| Heroic | `%APPDATA%\heroic\legendaryConfig\legendary\installed.json`, `gog_store\installed.json`, `nile_config\nile\installed.json` (a dictionary, or a list, of records) | `title` or `app_name`; `install_path` or `path` | directory must exist | `core/games.py:672-698` |
| Plain folders | for drive letters C to J that exist and are not removable: `Games`, `Game`, `Oyunlar`, `Juegos`, `Spiele`, `Jeux`, `Giochi`, `My Games`, `PC Games`, `Installed Games` at the drive root, one level of subfolders | folder name | skip names starting `.` or `$`; skip the names below | `core/games.py:743-777` |
| Emulators | see Emulators | | | |
| Added by hand | see Folders added by hand | | | |

**How each store is found (settled in M2d).** Playnite's library plugins
(MIT) are the reference for finding installed games; the table above, from
the Python reference, for what the tool keeps. Where they differ:

- Steam: Valve's text files are parsed with the `ValveKeyValue` package; the
  folders under `common` drive the list (the rules above); names from the
  manifest's `name`, then the folder; Steam's tools are dropped by appid
  (228980, 250820) and by name prefix (`Proton `, `Steam Linux Runtime`,
  `Steamworks Common`, `SteamVR`).
- Epic: Playnite's filters on top of the flags above; among manifests that
  share a folder, the one whose `AppName` is the `MainGameAppName` wins.
- GOG: uninstall entries named `<digits>_is1` with publisher `GOG.com`.
- Battle.net and Rockstar: uninstall entries whose `UninstallString` names
  the client's uninstaller (`--uid=` for Battle.net), Test and Beta entries
  and the clients themselves dropped; names from `DisplayName`.
  Battle.net's `product.db` is not read.
- Amazon: the app's `GameInstallInfo.sqlite` (installed rows; read-only,
  with `Microsoft.Data.Sqlite`); the library folders when the database is
  missing or unreadable.
- Xbox: the folders on every fixed or removable drive; the store's pick is
  the `PC` executable of `MicrosoftGame.config`'s `ExecutableList`, else the
  first.
- Heroic: the three shapes; DLC entries (`is_dlc`) dropped.
- EA: also the EA app's install file, `<ProgramData>\EA Desktop\<id>\IS`,
  AES-CBC encrypted with a key derived from the machine's hardware (the
  format as the open-source GameFinder documents it; our own code, SHA3 from
  BouncyCastle). Installed means `softwareId`, `baseSlug` and
  `baseInstallPath` all set and the folder a directory; the name is the
  folder's and there is no store pick (`executableCheck` is relative to a
  registry value and can name an anti-cheat launcher). Without the hardware
  identity (the Windows side reads it through WMI) this part is skipped.
  The Windows `IHardwareIdentity`, in the key's order: `Win32_BaseBoard`
  `Manufacturer`, `SerialNumber`; `Win32_BIOS` `Manufacturer`,
  `SerialNumber`; the volume serial of `C:` from `GetVolumeInformationW`,
  uppercase hex without leading zeros; `Win32_VideoController`
  `PNPDeviceID`; `Win32_Processor` `Manufacturer`, `ProcessorId`, `Name`.
  Each WMI value is the first instance's string; any failure means no
  identity (null), never an exception.

The plan's list is Steam, Epic, GOG, EA, Ubisoft, Battle.net, Rockstar and
Xbox. Amazon and Heroic are in the reference (`README.md:112`) and stay,
since each costs one small source.

- Changed from the reference: itch is dropped in v1 (`core/games.py:656-669`).
  Its games are reached as plain or hand-added folders; the source is
  deferred to M8.

**Steam rules.**

- A folder in `common` is a game only if its manifest says it is fully
  installed. Steam deletes a game's `appmanifest` when it is uninstalled and
  leaves the folder (saves, shader caches, config, sometimes nothing): on one
  real library that was 29 of 32 folders, and 46 of 63 on the developer's own,
  and every one used to be offered as a game to install into.
  - A manifest counts when `StateFlags` has bit 4 (4, fully installed). A
    manifest without `StateFlags` counts. One that is queued or still
    downloading (Battlefield 6 sat as an empty folder with `StateFlags` 1042)
    does not: nothing to install into.
  - No manifest, or one not yet complete: the folder is believed only if it
    has an `.exe` directly in it (one listing, capped at 400 entries; a
    folder that full counts, an unreadable one counts: "leave it in the
    list"). That keeps a game whose manifest is missing for another reason
    and drops the leftovers, without walking in.
  - A folder that holds an install record of ours is never hidden, even when
    the game is gone: it is the one place the person needs the entry, to
    press uninstall and get the leftovers out.
  - One unreadable manifest is skipped, not fatal: Steam may be rewriting it.
- A folder reached by two paths (junction, symlink, a library listed twice) is
  listed once, by its resolved path.
- A library listed in `libraryfolders.vdf` that is not a directory is ignored.
  A library without `steamapps\common` is ignored.

**Names of the launcher libraries themselves.** A plain folder scan often
finds the stores' own libraries (`D:\Games\SteamLibrary`) and they are not
games. A `Folder` entry whose folder name (any case) is one of `steamlibrary`,
`steamapps`, `epic games`, `gog galaxy`, `ea games`, `ubisoft`, `xboxgames`,
`amazon games`, `battle.net`, `common`, `riot games`, `rockstar games` is
dropped (`core/games.py:1152-1170`).

### The scan

- **Order**: Steam, Epic, GOG, EA, Ubisoft, Battle.net, Rockstar, Amazon,
  Heroic, Xbox, plain folders, emulators. Progress is reported before each
  ("Scanning Steam...") and, for each game, "Inspecting games... i/n: name"
  (the last two games used to be missing from that count, `test_scan.py:133`).
- **One folder, one game.** Entries with the same resolved folder are merged,
  the first source winning; that is why plain folders come after the stores.
- **One executable, one game.** After the executables are known, two entries
  with the same resolved executable (case-insensitive) are listed once: Steam
  gives the library folder, the Rockstar launcher the `GTAIV` folder inside it
  (`test_all.py:8690-8720`). The first source wins, except that an entry that
  carries something wins over one that does not: an install recorded against
  it, or a graphics API or architecture set by hand (both are keyed by that
  entry's folder and would be lost). Games with an emulator profile (several
  games of one system share an emulator's executable) are never merged.
- **The result is sorted by name**, ignoring case.
- **A game with no executable** is not hidden silently: the scan note says "N
  folders had no executable" and the log names which ("no executable under
  <folder> - <name> will not be listed"). This was reported as "sometimes it
  does not see my games" because the folder was dropped without a word.
  - Changed from the reference: the scan result keeps such entries, with the
    reason, and the screen decides how to show them; the engine does not
    delete them.
- **A slow game is logged** ("inspected <name> in 1.3s") when it takes at
  least a second.
- **Drives.** A drive letter Windows will not talk about (a card reader with
  no card, a locked BitLocker volume, a drive that went away) raises
  "wrong parameter" (error 87) from a plain directory test, and one such
  letter used to take the whole Xbox, folder and emulator scan down (issue
  #2). Every directory test on a drive path must treat an I/O error as "not
  there".
  - Changed from the reference: the Xbox, Amazon and emulator searches use the
    hard-coded letters C to H, the plain-folder search C to J
    (`core/games.py:565`, `:641`, `:763`, `core/emulators.py:234`). Enumerate
    the machine's fixed and network-less drives instead, and use the same list
    in every source; a Game Pass library on another letter was a reported
    case (#897). Removable drives (USB sticks, which hold backups and
    transfers, an install landed on one, #18) stay excluded from the plain
    folder search only.
- **Full scan and quick scan.**
  - A **full scan** reads every source, including the emulator search, and
    inspects every game.
  - A **quick scan** (the default "rescan" once a library is cached) asks
    every store what it has, keeps a game already known as it was read, and
    inspects only folders not seen before (#144: six drives, one an old USB
    disk, and every new game meant walking all of them again). It does not
    search for emulators; "full rescan" does that. Kept: a game that is
    still on disk and either a store still lists it, or it is an emulator, a
    folder added by hand, or anything of ours installed
    (so it can be uninstalled). Dropped: a game a store listed before and no
    longer lists (a removed Steam game often leaves its folder, and a scan
    that only added would keep it forever), and any game whose folder is gone.
  - "Read the library when the tool opens" is a setting; off means no scan at
    all at start, and rescan and add-a-game still work.
- A scan that fails as a whole stops with "the scan stopped - details in the
  log", or a one-line cause when it is a known kind; the list already shown
  is kept.

### Folders added by hand

- **Add a game**: the person picks a folder (the old window asked for the
  folder the `.exe` is in). The entry is read like any other, with source
  `Manual`.
  - A folder with no executable: nothing is added, and the message says "No
    executable was found in: <folder>".
  - A folder that is already in the library, by folder or by executable: that
    game is opened, brought back if hidden, the search and filter are
    cleared, and the status says "<name> is already in the list". A second
    card for the same executable only merged on the next start.
  - Added: remembered in the settings as a list of absolute folder paths.
- **After every full scan** the remembered folders are added back: a folder
  that is a directory, is absolute, is not already in the results (by folder,
  ignoring case) and still has an executable. A saved list is a file people
  edit, so a non-list, a non-string, a relative path or a missing folder is
  skipped. The saved library is dropped on a new tool version and a full scan
  finds only what launchers list, which is why they are remembered separately
  (#441). Store entries come first so that a store's entry for the same
  executable wins the one-entry-per-executable merge.
- A manual entry survives a scan that does not list it
  (`ctl_library.py:819-824`).
- Changed from the reference: the **executable the person picked** in a
  folder with several (the "target exe" control) is remembered with the
  folder, as the graphics API and the architecture are. In the reference it
  lived only in the cached library and the install record: after a version
  change dropped the cache, a rescan went back to the ranking's pick and
  showed the person a different executable than the one they chose (#56).

### Choosing the executable among candidates

The executable is chosen again on every scan, so the choice must be stable and
explainable. The candidates are every `.exe` in the game folder, ranked.

**The walk.**

- Depth at most 5 below the game folder; the root is depth 0.
- Not entered: any directory whose name starts with `.`, and any whose name
  (any case) is one of: `_commonredist`, `commonredist`, `redist`,
  `redistributable`, `redistributables`, `directx`, `dotnet`, `vcredist`,
  `vc_redist`, `easyanticheat`, `easyanticheat_eos`, `battleye`, `punkbuster`,
  `installers`, `installer`, `prerequisites`, `prereq`, `support`, `docs`,
  `manual`, `soundtrack`, `artbook`, `extras`, `dxsetup`, `crashreportclient`,
  `epicwebhelper`, `thirdparty`, `steamvr`, `openvr`, `__installer`,
  `dotnetfx`, `movies`, `content`, `data`, `assets`, `textures`, `host64`
  (the last is where our own install puts the 32-bit feeder helper).
- The walk ends after 400 executables or 6000 directories, or when it is
  cancelled.
  This exists because a tree can hold hundreds of thousands of directories
  with no `.exe` in any (C:\XboxGames' empty GameSave folder, the Minecraft
  Launcher's runtime tree), and the scan stopped for minutes and died. A
  warning is logged: "stopped looking for executables under <folder> after N
  folders - too big to search".
- Changed from the reference: the walk goes breadth first, in name order,
  and does not follow junctions or symbolic links. The 6-second wall-clock
  limit (`core/pe.py:1182-1183`) made the result depend on the speed of the
  disk and the order of `os.walk` (arbitrary), so which executables survived
  the cap differed from one run to the next. Limits are by count only (and a
  cancellation token); shallow executables are always seen first.

**The score.** Higher is more likely the real game (`core/pe.py:1219-1269`).

| Rule | Points |
| --- | --- |
| the name contains `shipping` (Unreal's suffix) | +1000 |
| path (below the game folder, forward slashes, any case) contains `/binaries/win64/` or `/bin/win64/`, or starts `binaries/win64/` | +400 |
| else `/binaries/win32/` or `/bin/win32/` | +300 |
| else `/bin/` or starts `bin/` | +200 |
| `/engine/binaries/` or starts `engine/binaries/` (engine tooling) | -900 |
| name (letters and digits only) contains, or is contained in, the folder name (letters and digits only) | +350 |
| directly in the game folder | +120 |
| the name looks like a helper (the skip list below) | -1500 |
| file size in MB, capped at 300 | +1.2 per MB (at most 360) |
| per path separator in the relative path | -15 |
| `<exe name>_Data` folder beside it (Unity; the player is under a megabyte and size alone ranked the launcher higher, #152) | +300 |

**Adjustments after scoring.**

| Rule | Points | Why |
| --- | --- | --- |
| a trial or demo (`[ ._-]*(trial\|demo)` at the end of the name) with the full executable of the same name in the same folder | -600 | NeedForSpeedHeatTrial.exe beat the game (#131); a demo-only install keeps its executable, and the trial stays in the list |
| a launcher (`[ ._-]*launcher` at the end) with the same-named executable beside it | -600 | UBOAT Launcher beside UBOAT (#152) |
| any launcher-like name while some candidate is not one | -600 | GTAVLauncher beside GTA5 (#191); a folder holding only a launcher must still offer it |
| Valve's Source layout: `bin\engine.dll` (in `bin`, `bin\x64` or `bin\win64`) and a `gameinfo.txt` in a folder beside it; every game-like, non-launcher executable directly in the root | +500 | hl2.exe is 100 KB while `bin\` holds megabytes of tools (elementviewer, hlmv, hammer), and Half-Life 2: Episode Two was set up in front of elementviewer.exe (#669) |
| two executables in one folder whose names differ only by a 32/64 marker (`x86`, `x64`, `win32`, `win64`, `32`, `64`, with an optional `bit`), one 32-bit and one 64-bit by header | the 32-bit one -400 | Subnautica.exe and Subnautica32.exe (#190). 400, not 600: below -500 a file is dropped, and a person who plays the 32-bit build could not pick it |

**Result.** Candidates scoring above -500 in descending score; if none, all
of them in descending score (never an empty answer when there is a file).
Ties are broken by the relative path, ignoring case.
- Changed from the reference: ties were broken by the order the filesystem
  walk returned them, which is not defined. The stable order makes tests and
  scan results repeatable.

**Names that look like helpers** (a name containing any of, in any case):
`srcds` (Valve's dedicated server), `unins`, `setup`, `vcredist`, `dxsetup`,
`dotnet`, `prereq`, `redist`, `crashhandler`, `crashreport`, `crashpad`,
`easyanticheat`, `battleye`, `touchup`, `installer`, `activation`, `cleanup`,
`helper`, `webhelper`, `unitycrashhandler`, `ue4prereqsetup`,
`ue5prereqsetup`, `epicwebhelper`, `dlss5-feed-host` (our own 32-bit helper,
ranked above a 32-bit game it was taken for the game), `digitalextras`,
`extrasapp`, `artbook`, `soundtrack` (a bonus app shipped beside the game won
the tie in #497). "Looks like a game" is the negation, also used by the
watcher and diagnosis.

**Launcher-like** (the stem): contains `launcher` followed by a separator or
the end (`GTAVLauncher`, `UBOAT Launcher`, but not `SpaceLauncherSimulator`);
or starts with `launch` followed by a separator or the end (`Launch`, `Launch
Game`); or is `start_protected_game` (Easy Anti-Cheat's bootstrapper); or
starts with `eos`, `epic`, `ea`, `origin`, `uplay` or `ubisoft`, an optional
separator, then `launch` or `start`. A launcher starts the game as a new
process: whatever is put in front of it is loaded by the launcher and
unloaded when it hands over, so nothing draws and the report comes back "it
never started" with every file present (#191).
- The **real executable for a launcher** is the first candidate that is not
  that file, is not launcher-like and looks like a game; none when there is
  nothing better (a launcher-only folder must not be answered with a guess).
  Used by the install preview's launcher warning and by diagnosis.

**The store's pick versus the ranking.** A store's launch executable is often
a stub that starts the real one: Epic's manifest names `GWT.exe` in the root
of Ghostwire Tokyo and the game is `Snowfall\Binaries\Win64\GWT.exe`; files
beside the stub are never loaded and the install "does nothing". When the top
ranked candidate is under a `Binaries` folder (any case, any depth) and the
store's pick is not, the ranking wins and the log says "the store names X but
the game runs from <path> - using that". Any other store pick stands.

**The person's pick.** An executable chosen in the list is never replaced by
the store's stub or by an earlier install's record, and the files go beside
it (Conan Exiles was installed beside the launcher after the Shipping
executable was chosen, #56). Choosing clears the previous install folder.

**Previous install.** The executable is ranked fresh each scan, so a game
installed against `Bin\Win64\Game.exe` could be listed against a different
executable next time, and uninstall greyed out while the files stayed.
So: look for our record in the folder of the current executable, the game
folder, and every candidate's folder, in that order. The first that holds one
becomes the install folder, and the executable the record names (when that
file is still there) becomes the executable, so the architecture and API shown
describe what was actually patched; failing that, a candidate in that folder.
The log says when the record is not beside the executable.

**A single executable or a folder as the target.** A path to an `.exe` is the
executable and its only candidate; a folder runs the ranking and the best is
chosen; "No .exe found in <folder>." for a folder with none; "<path> not
found." for a path that is neither (`core/pe.py:1415-1425`).

### Reading one game

The reading order for each entry, which is what the scan and the person's
choices call again (`core/games.py:963-1022`):

1. Find the executable and the candidates (above). A store's pick that is a
   file stays; else the best candidate. No executable: the error "no
   executable found", and the entry is kept without one.
2. Prefer the ranking over a stub; adopt the previous install (unless the
   person chose the executable).
3. Try to open the executable. Permission denied: protected, bitness comes
   from the person's choice if any. Else bitness from the header.
4. Detect the API from the executable (see [pe](pe.md)).
5. A graphics API chosen by hand for this folder replaces it, with the reason
   "set by hand (detected <api>)". The import table can lie: R.U.S.E. links
   D3D11 and renders with D3D9 (#24).
6. An emulator profile is attached when the executable's file name is a known
   one; a `Manual` entry is renamed after it.
7. When no API was chosen by hand, an emulator that cannot present through
   Direct3D gets the API it really uses (see Emulators).

- **Overrides** are stored per game folder, keyed by the folder path with any
  case: API (one of DX8, DX9, DX10, DX11, DX12, Vulkan, OpenGL; anything else
  clears it) and architecture (the integer 32 or 64 only; `64.0`, `"64"`,
  `true`, lists and other values are rejected when set, ignored when read,
  and a settings file holding a list instead of a mapping reads as none).
  `test_scan.py:490-520` has the reject list.
  - An architecture override applies only to a protected executable; a
    readable one ignores it and clears stale error and notice
    (`test_scan.py:410`).
  - Setting the API back to automatic restores the detected API and, for an
    emulator, the emulator rule.
- **Supported or not**: an entry has no executable: "No game executable
  found."; has an error: that error; protected and architecture or API still
  not chosen: "Choose this game's 'architecture' and 'graphics api' in the
  game's settings - Windows protects its executable, so they cannot be read
  from it."; architecture unknown: "Could not read the architecture."; API
  DX8 on a 64-bit executable (set by hand): "DirectX 8 is 32-bit only, and
  this executable is 64-bit - set 'graphics api' to the one it really draws
  with in the game's settings." Everything else is supported
  (`core/installer.py:761-788`). A protected executable with the Agility SDK
  folder beside it reads DX12 from the files and needs only the architecture
  (`test_scan.py:447`).
- **Folder is a locked store folder** when a path segment (any case) is
  exactly `XboxGames` or `WindowsApps`; `ModifiableWindowsApps` is the one that
  is meant to be touched and must not match by substring
  (`core/games.py:120-135`). The check says nothing about write access.
  Preflight probes that (an install spec matter) and says "Windows does not
  let anything write into this Xbox / Game Pass folder. Only games whose
  publisher allows modding have 'Enable mods' (or 'Manage > Files > Browse')
  in the Xbox app; if yours has it, turn it on and press rescan. If it does
  not - Microsoft Flight Simulator, most Store titles - no tool can change
  this folder, and the Steam version of the game is the one that can be set
  up."

### Emulators

An emulator is another D3D11, D3D12, Vulkan or OpenGL application: once
ReShade is in, the feeder behaves as in a normal game. Direct3D 11 and 12 are
the proven backends; Vulkan works through ReShade's layer registration for
64-bit emulators and OpenGL through the feeder's interop; both are newer and
less proven, and the tool says so per game. The feeder needs a depth buffer;
emulators often expose several and ReShade may latch onto the wrong one.

**The 19 profiles**, identified by the executable's file name (any case)
(`core/emulators.py:58-215`):

| Emulator | System | Executables | Presents through Direct3D | What the tool tells the person |
| --- | --- | --- | --- | --- |
| DuckStation | PlayStation 1 | `duckstation-qt-x64.exe`, `duckstation-nogui-x64.exe`, `duckstation.exe` | yes | Renderer = Direct3D 11 (or 12); raise the internal scale to 2x to 4x |
| PCSX2 | PlayStation 2 | `pcsx2-qt.exe`, `pcsx2x64.exe`, `pcsx2x64-avx2.exe`, `pcsx2.exe` | yes | Renderer = Direct3D 11 (or 12); upscale 2x or more |
| Dolphin | GameCube / Wii | `Dolphin.exe`, `DolphinQt.exe` | yes | Backend = Direct3D 11 (or 12) |
| PPSSPP | PSP | `PPSSPPWindows64.exe`, `PPSSPPWindows.exe` | yes | Backend = Direct3D 11 |
| Xenia | Xbox 360 | `xenia.exe`, `xenia_canary.exe` | yes | D3D12 is the default |
| Cemu | Wii U | `Cemu.exe` | no | Vulkan or OpenGL; set Vulkan (beta path) |
| RPCS3 | PlayStation 3 | `rpcs3.exe` | no | Vulkan or OpenGL; set Vulkan (beta path) |
| Ryujinx | Switch | `Ryujinx.exe`, `Ryujinx.Ava.exe`, `Ryujinx.Headless.SDL2.exe` | no | Vulkan or OpenGL only; set Vulkan |
| yuzu / suyu / Eden / Citron | Switch | `yuzu.exe`, `suyu.exe`, `eden.exe`, `citron.exe`, `sudachi.exe` | no | Vulkan or OpenGL only; set Vulkan |
| shadPS4 | PlayStation 4 | `shadPS4.exe`, `shadps4.exe` | no | Vulkan only (beta path) |
| Azahar / Citra / Lime3DS | 3DS | `azahar.exe`, `citra.exe`, `citra-qt.exe`, `lime3ds.exe` | no | Vulkan or OpenGL; set Vulkan |
| melonDS | DS | `melonDS.exe` | no | OpenGL only; the least reliable path |
| Flycast | Dreamcast | `flycast.exe` | yes | set the renderer to DirectX 11 |
| xemu | Xbox | `xemu.exe` | no | Vulkan or OpenGL; set Vulkan |
| Vita3K | PS Vita | `Vita3K.exe` | no | Vulkan or OpenGL; set Vulkan |
| RetroArch | multi-system | `retroarch.exe` | yes | set the video driver to d3d11 or d3d12 |
| mGBA | Game Boy Advance | `mGBA.exe` | no | OpenGL only; the least reliable path |
| Snes9x | SNES | `snes9x-x64.exe`, `snes9x.exe` | yes | set the output method to Direct3D |
| Play! | PlayStation 2 | `Play.exe` | no | Vulkan or OpenGL; set Vulkan |

Also in the reference are two per-profile notes: DuckStation ("Raise the
internal resolution scale to 2x-4x so DLAA has detail to work with.") and
PCSX2 ("An upscale multiplier of 2x or more is recommended."), and Cemu's:
"Vulkan goes through ReShade's layer registration; OpenGL installs as
opengl32.dll and is the least reliable."

- **An emulator entry** is named `<name> (<system>)`, its folder and
  executable are the emulator's, source `Emulator`.
- **The API of an emulator that cannot present through Direct3D**: eden.exe is
  a Qt program whose imports read as DX12, so the Direct3D routes were offered
  and the person had to know to set Vulkan by hand (#352). When the detected
  API starts with `DX` and the profile cannot use Direct3D, the API becomes
  Vulkan if the profile's hint says Vulkan, else OpenGL, with the reason
  "<name> draws with Vulkan or OpenGL only (its imports read as <api>)". Only
  melonDS and mGBA come out OpenGL. One rule, applied at the scan, at "back to
  automatic", and in the label beside the dropdown. It never overrides an API
  chosen by hand.
- **Searching for emulators** (full scan only): roots are the registry's
  Add/Remove Programs entries whose `DisplayName` contains a profile name
  (`HKLM` both views and `HKCU`, `Uninstall`; `InstallLocation` must be a
  directory), `%ProgramFiles%`, `%ProgramFiles(x86)%`, `%LOCALAPPDATA%`,
  `%APPDATA%`, Desktop, Downloads, Documents, and on each drive: `Emulators`,
  `Emulator`, `Games`, `Emu`, `RetroArch`, `PS2`, `PS1`, `Emulation`, `Roms`,
  `Apps`, `Programs`, `Oyunlar`, `Tools`, `Portable`, `EmuDeck`,
  `Emulators_Portable`, `LaunchBox`, plus the drive root itself except `C:`
  (people unpack to `D:\PCSX2`), plus every Steam library's `steamapps\common`
  (RetroArch, DuckStation and Dolphin ship on Steam). Each root is searched
  for a matching `.exe` in the root and two levels below, skipping names that
  start with `.`; duplicates by resolved path; a root that is not a directory
  or whose letter errors is skipped. Progress says "Looking for emulators:
  <root>".
- The wrong thing to port: `Play.exe`, `Dolphin.exe`, `xenia.exe` and similar
  are generic names, and any program with that file name in a game folder is
  given a profile and renamed (see the bugs).

**Render backend of an emulator.** Every hint above tells the person to open
a settings dialog and pick Direct3D. Library owns detection: the profile, and
reading the current backend for the status. The edit itself is
[install](install.md)'s ("Emulator config edits": key by key, each key's old
value recorded, no whole-file backup) and is deferred to M8; in v1 the
install shows the hint. The table below is where the status reads the
backend from (`core/emulators.py:378-838`).

| Emulator | Config file (first that exists) | Setting | Value written | Already DXGI |
| --- | --- | --- | --- | --- |
| DuckStation | `settings.ini` beside the exe if `portable.txt` or `settings.ini` is there; `Documents\DuckStation\settings.ini`; `%LOCALAPPDATA%\DuckStation\settings.ini` | `[GPU] Renderer` | `D3D12` | `D3D11`, `D3D12` |
| PCSX2 | `inis\PCSX2.ini` beside the exe if `portable.ini` or `portable.txt` is there; `Documents\PCSX2\inis\PCSX2.ini` | `[EmuCore/GS] Renderer` | `15` (D3D12) | `3` (D3D11), `15` |
| Dolphin | `User\Config\Dolphin.ini` beside the exe if `portable.txt`; `Documents\Dolphin Emulator\Config\Dolphin.ini`; `%APPDATA%\Dolphin Emulator\Config\Dolphin.ini` | `[Core] GFXBackend` | `D3D12` | `D3D` (D3D11), `D3D12` |
| PPSSPP | `PSP\SYSTEM\ppsspp.ini` under the memstick folder: with `installed.txt` beside the exe, the path in it (or `Documents\PPSSPP` when empty), then `Documents\PPSSPP`, then `memstick` beside the exe; without it `memstick` beside the exe, then `Documents\PPSSPP` | `[Graphics] GraphicsBackend` | `2 (DIRECT3D11)` | `2`, `2 (DIRECT3D11)`, `DIRECT3D11` |
| Xenia | `xenia-canary.config.toml` or `xenia.config.toml` (canary first for an exe named canary) beside the exe if `portable.txt`; then `Documents\Xenia` | `[GPU] gpu` (quoted) | `"d3d12"` | `d3d12` |
| RetroArch | `retroarch.cfg` beside the exe; `%APPDATA%\RetroArch\retroarch.cfg` | `video_driver` (quoted, no section) | `"d3d11"` (not d3d12: that driver still lacks features some cores rely on) | `d3d11`, `d3d12` |
| RPCS3 | `config.yml` beside the exe | `Renderer:` | read only | none |
| Cemu | `settings.xml` beside the exe; `%APPDATA%\Cemu\settings.xml` | `<api>` 0 OpenGL, 1 Vulkan | read only | none |

Names the status uses: PCSX2 `-1` Automatic, `3` D3D11, `15` D3D12, `12`
OpenGL, `14` Vulkan, `13` Software, `11` Null; PPSSPP `0` OpenGL, `2` D3D11,
`3` Vulkan (PPSSPP writes `3 (VULKAN)` and reads the number); Dolphin `D3D`
D3D11, `OGL` OpenGL, `Software Renderer`, `Null`; DuckStation and Xenia use
their own words; RetroArch `d3d11`, `d3d12`, `gl`, `glcore` (OpenGL), `vulkan`,
`d3d9`, `sdl2`, `gdi`. The status reads "unknown" when no file or no value.

### The library cache

Finding the library is the slowest thing the tool does: every store's folders
are walked, every executable is read for architecture and renderer, and the
compatibility pass walks the game folder again looking for DLSS runtimes. On
a large library across drives that is minutes, and it happened on every launch
even when nothing had changed (#8, #18, #32, #67).

- **File**: `%LOCALAPPDATA%\dlss5-autopilot\library.json`, beside the
  settings, not beside the executable (the executable is run from Downloads,
  a USB stick, a folder Defender has an interest in).
- **Contents**: schema number (2), the tool's version, the GPU's compute
  capability (`sm`), each game's fields (above, without the emulator profile
  and the candidates' content is only paths), a **stamp** per game, and the
  compatibility rows (DLSS and route outlook per game, keyed by folder and
  executable; owned by the routes spec).
- **When it may be used**: only when the schema number is equal, the tool's
  version is equal and the GPU's `sm` is equal. What the tool believes about a
  game, its renderer above all, is exactly what changes between releases (1.7.1
  called Unity games OpenGL; a cache that survived the update would keep
  handing back the answer the update was written to fix). The compatibility
  columns are read against the card in the machine, so a different card means
  looking again. Otherwise the full scan runs as if there were no cache.
- **The stamp** is, for the executable, the folder and the install folder, the
  modification time (whole seconds) and size, or none. A game whose stamp is
  not what it was is **changed**: it is inspected again on the spot, off the UI
  thread, and its compatibility row is not reused. A game with a protected
  executable is always re-read, since protection can change without a
  timestamp change. A game whose folder no longer exists is dropped (the game
  or its drive is gone). The rest come back as they were.
- Changed from the reference: the folder stamp only catches a file dropped in
  the **game's root** (`core/library.py:48-62`: `g.exe`, `g.folder`,
  `g.install_root`). A mod or add-on dropped beside the executable in
  `Bin\Win64` changes that folder's time, not the root's, and the
  compatibility row went stale. Stamp the executable's folder too. A directory
  time only changes when a direct child is added or removed, so even this is a
  hint; compatibility checks that matter still read the files.
- **After loading**, the list passes through the one-executable-per-game merge
  again: a folder picked by hand is saved beside the store's entry for the
  same game. Graphics API overrides are applied on load exactly as on a scan
  (a cached game that came back with its detected renderer would be installed
  for the wrong one, #24, #66, #70); a protected executable takes its
  architecture from the override, and its API from the detected one.
- **Writes**: to a temporary file beside it then replaced atomically; a
  failure never raises into the tool. A full drive logs "the library cache was
  not saved: the drive is full"; any other failure is logged with its cause. A
  temporary file left by a failed write is removed (#148, it took more of the
  space it lacked on every save).
  - Changed from the reference: flush to disk before the replace (the plan's
    rule for every atomic write).
- **Nothing here is load-bearing**: a missing, unreadable, corrupt or stale
  file means the normal full scan. A cache holding no games is the same as
  none.
- **Forget**: deleting the file means the next launch scans from scratch.
- When the library is saved: after a scan; after a changed game is re-read; and
  when a game is added by hand. Not while games are still being re-read.

### Cover art

Steam's library cache has pictures only for games Steam installed; a game from
Epic, Ubisoft, EA or a plain folder was an icon on an empty tile. For each
picture kind (**cover**, **hero** (the wide background), **logo**) the first
source that has it wins, in this order (`core/ui/art.py:109-161`):

1. pictures the person chose;
2. Steam's own cache;
3. the Xbox folder's own files (looked up only when none of the person's,
   Steam's or a downloaded cover exists);
4. pictures downloaded earlier.

With no cover at all, an online lookup is queued (if allowed), and the
executable's own icon is shown, else a plain tile. A blurred copy of Steam's
hero belongs to Steam's hero only, not to one chosen by hand.

- **Steam's cache**: `<Steam>\appcache\librarycache\<appid>\` (sometimes one
  hashed folder deeper). Cover: `library_600x900.jpg`, else
  `library_capsule.jpg`; hero `library_hero.jpg`; blurred
  `library_hero_blur.jpg`; logo `logo.png`. The appid comes from
  `appmanifest_*.acf` (`appid` and `installdir`), matched on the folder name
  (any case), for Steam entries only.
- **Chosen by hand**: copied, never referenced (the original is in Downloads or
  on a stick and moves). At most 8 MiB; the file's own first bytes decide the
  type, not its name: JPEG `FF D8 FF`, PNG `89 50 4E 47 0D 0A 1A 0A`, BMP `42
  4D` with more than 26 bytes. Messages: "That file could not be read.";
  "That picture is larger than 8 MB."; "That is not a JPEG, PNG or BMP
  picture."; "The picture could not be saved in the tool's cache."; "Windows
  could not read that picture." (the toolkit's decoder is asked too, and a
  file with a JPEG header it cannot draw is removed again). The person can
  go back to the automatic pictures.
- **Xbox folders** (local only): `MicrosoftGame.config`, then
  `appxmanifest.xml`, at most 2 MiB each; the first that gives anything wins.
  Cover: `Square480x480Logo`, `Square310x310Logo`, `Square150x150Logo`,
  `StoreLogo`, then the `<Logo>` element. Hero: `SplashScreenImage`, then
  `Image`. A path may be shipped scale-qualified (`StoreLogo.png` as
  `StoreLogo.scale-100.png` and friends): the largest file by size wins. A
  path with `..` is refused.
  - Changed from the reference: parse the files as XML, not with a pattern over
    text, with DTD processing off, and keep the file inside the game folder by
    the same whole-part rule as the net spec (a drive letter such as `C:\x`
    escaped the folder in `core/covers.py:231-246`: the pattern only removed
    leading slashes).
- **Online lookup** is **off until the person says yes**. The tool's privacy
  promise is that nothing leaves the PC unless asked for, and a store search
  carries the game's name: there is no default of on. Three states: not asked,
  yes, no.
  - The first time a game has no cover on this PC a banner (not a dialog:
    nothing waits on it) asks: "some games have no cover on this PC - look
    them up online? (sends their names to Steam's store)", with "look them up"
    and "no". The setting stays in the view menu.
  - When it is off, or switched off while a lookup runs, no request is made
    and no result kept (a download that finished after the switch went off is
    discarded). A lookup cut short by the switch records nothing, so
    switching on asks again.
  - One lookup at a time on one background worker, so fifty games are fifty
    polite requests in a row. A game is asked at most once per run; switching
    the setting on again allows another try.
  - **Epic first** for an Epic game: the launcher's catalog cache
    (`%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Catalog\catcache.bin`,
    at most 64 MiB, base64 of a JSON list of items with `id`) names the images
    of the item the manifest installed in this folder. The manifest is found
    by `InstallLocation` compared ignoring case and slash direction, trailing
    slash removed. The item is the manifest's `CatalogItemId`, then
    `MainGameCatalogItemId`; an item whose `namespace` differs from the
    manifest's is not this game's. Image types in order: cover
    `DieselGameBoxTall`, `OfferImageTall`, `DieselStoreFrontTall`; hero
    `DieselGameBox`, `DieselGameBoxWide`, `OfferImageWide`; logo
    `DieselGameBoxLogo`. Only `https://` URLs are used (an `http://` logo is
    not).
  - **Steam's store by name** for everything else, and for an Epic game with
    no cover from Epic: `https://store.steampowered.com/api/storesearch/?term=<name>&l=english&cc=US`
    with the name's trademark signs `™ ® © ℠` removed and whitespace
    collapsed. The match is accepted only on a close name, because a wrong
    cover is worse than none.
  - **The match rules** (`core/covers.py:380-453`): normalise a name to
    lower-case words: trademark signs and apostrophes (`'`, `` ` ``, curly
    quotes, `ʼ`, `´`) removed, Unicode decomposed and accents dropped, case
    folded, `&` as "and", every other non-alphanumeric run as one space. An
    app result (type `app`, positive id, non-empty name) is **the game** when:
    1. its normalised name equals the game's; else
    2. both names are equal once an edition suffix is dropped
       (`[the] complete|definitive|enhanced|deluxe|digital deluxe|standard|ultimate|gold|premium|special|anniversary|collectors|legendary|game of the year|goty edition`,
       `directors cut`, `goty`) and the result is not an add-on (the words
       `dlc`, `soundtrack`, `ost`, `pack`, `bundle`, `upgrade`, `demo`, `season
       pass`, `expansion`, `skin`, `skins`, `costume`, `artbook`, `art book`,
       `supply drop`, `episode`, `chapter`, `trial`, `benchmark`, `playtest`,
       `dedicated server`, `sdk`, `wallpaper(s)`, `prelude`, `mask`,
       `cosmetic`); else
    3. for a game name of two words or more, exactly one result of the form
       `<the game's name>: <subtitle>` whose subtitle is not an add-on
       ("DEATH STRANDING 2: ON THE BEACH").
    A sequel ("The Division 2" for "The Division") matches none.
  - **Pictures from the appid**: apps published since 2024 keep them under a
    hash folder and the plain path is a 404, so the store's own asset list
    comes first: `https://api.steampowered.com/IStoreBrowseService/GetItems/v1/?input_json=<json>`
    with `{"ids":[{"appid":N}],"context":{"language":"english","country_code":"US"},"data_request":{"include_assets":true}}`;
    when `asset_url_format` starts with `steam/apps/<N>/${FILENAME}`, the
    names `library_capsule_2x`, `library_capsule` (cover), `library_hero`,
    `library_logo` are taken from it if they match `^([0-9a-f]{40}/)?[A-Za-z0-9_.-]+\.(jpg|png)$`
    and fetched from `https://shared.fastly.steamstatic.com/store_item_assets/<format with the file name>`.
    Then the plain paths: cover `library_600x900_2x.jpg`, `library_600x900.jpg`
    (fastly), and `library_600x900.jpg` (cloudflare); hero `library_hero.jpg`
    (fastly then cloudflare); logo `logo.png` (fastly then cloudflare), on
    `https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/<N>/`
    and `https://cdn.cloudflare.steamstatic.com/steam/apps/<N>/`. Not having the
    asset list is fine.
  - **Each request**: 15 second timeout; one try; a declared length over the
    limit (8 MiB, 2 MiB for text) or a body over it is "not there"; a
    retry-class status (5xx, 408) or 403 or 429 or a failed connection is
    "offline"; any other status is "not there". A body counts as a picture
    only if its first bytes say JPEG, PNG or BMP: a captive portal's HTML page
    answered with 200 must never be kept as a cover.
  - **Stored** under `%LOCALAPPDATA%\dlss5-autopilot\art\<key>\`, `key` the
    first 20 hex digits of the SHA-1 of the folder path (lower case,
    backslashes, trailing slash removed), so two games never share an image;
    files `got-<kind>.<jpg|png|bmp>` and `user-<kind>.<ext>`, a temporary
    `.part` file renamed into place, an older picture of the same kind
    removed; `meta.json` with `name`, `folder`, and either `miss` and `ttl` or
    `source` and `when`.
  - **Not asked again**: a game with no match is remembered for 7 days, one
    whose connection failed for 1 day, so no game searches on every launch;
    unless the game was renamed since (a different question) or a cover has
    arrived. The log says "art: <name>: pictures from epic" or "steam <appid>",
    "no match online; asking again in a week", or "no answer (<text>); asking
    again tomorrow".
  - Never raises into the screen: every failure is a recorded miss.
  - Changed from the reference: the requests go through the shared HTTP path
    of the net spec (limits, TLS, no https to http redirect, disposal). The
    reference used its own `urlopen` here (`core/covers.py:536-559`), with
    the default redirect handler.

### Anti-cheat

ReShade with add-ons injects a DLL and detours graphics entry points; every
kernel-level anti-cheat treats that as tampering. The result is one of: the
game refuses to start; ReShade is silently prevented from loading so nothing
happens and the person assumes the tool is broken; an account ban. Arma 3 and
Arma Reforger are the common report: both ship BattlEye, both do nothing when
set up, and neither is a tool bug. Saying so up front beats letting someone
install and wonder.

- **Detection is by file, not by a game list**, so it covers games nobody
  told us about. For the install folder and the game folder (when they
  differ both are searched), every entry's name and, for each non-hidden
  subfolder (a name starting `.` or the hidden attribute), the names of its
  first 80 entries in name order (ordinal, so every file system gives the
  same answer), is matched against the markers. The result: the products (sorted, unique) and the evidence, the
  file names (sorted, unique, at most 4).
- **Matching** is on a whole word of a name, any case: the character before
  and after the marker must not be a letter or digit; digits may follow
  (`mhyprot3.sys`). "vanguard" in an asset name told the owner of Rise of the
  Tomb Raider, a single-player game, that Riot Vanguard was installed (#187).
- **Every warning names the file it rests on**, so a wrong one can be checked.
- **One detector, failing closed.** This is the only anti-cheat detector;
  install, the DLSS page's swaps, update all and the watcher's game start all
  call it. A folder or subfolder that cannot be read gives the answer
  "unknown", and every caller treats unknown as an anti-cheat present: a ban
  costs more than one more button press. A marker found elsewhere still makes the
  answer "present", naming it. Changed from the reference: a
  detection failure read as no anti-cheat (`dlssupdate.py:666-679`).

| Marker | Product |
| --- | --- |
| `beservice`, `beclient`, `battleye` | BattlEye |
| `easyanticheat`, `eac_launcher` | Easy Anti-Cheat |
| `vgk.sys`, `vgc.exe` (Vanguard installs under Program Files, never into a game folder; only its two files are evidence, not the word) | Riot Vanguard |
| `gameguard` | nProtect GameGuard |
| `xigncode` | XIGNCODE3 |
| `denuvo-anti-cheat`, `denuvoanticheat` (Denuvo alone is copy protection most single-player games carry; only its anti-cheat is) | Denuvo Anti-Cheat |
| `punkbuster` | PunkBuster |
| `faceit` | FACEIT AC |
| `ricochet`, `cod.exe` (Activision's PC titles since Warzone/Vanguard all start from `cod.exe` in the root and carry no file named for the anti-cheat, #856) | Ricochet |
| `eaanticheat`, `ea_anticheat` | EA Javelin |
| `zenlesszonezero`, `genshinimpact`, `starrail`, `mhyprot` (the driver installs system-wide, so the executable name is the evidence; the anti-cheat closes the game the moment a proxy DLL is seen, #29) | HoYoverse anti-cheat |
| `elytra` (a kernel anti-cheat that new Unreal shooters ship as a folder beside the game with its installer in it; WARDOGS) | Elytra Anti-Cheat |
| `ace-base`, `anticheatexpert`, `endfield` (Arknights: Endfield carries ACE with nothing named after it; its message named our `dxgi.dll` an "illegal hijack module", #991, #992) | ACE (Anti-Cheat Expert) |

- **What follows from a finding** belongs to the install and update specs, but
  the rule is here so they agree: an installed game with anti-cheat is
  marked in the library and the install asks for confirmation first (a ban is
  on the person who chooses to install anyway); "update all" skips games with
  anti-cheat; a card shows the product's name; an installed game with
  anti-cheat still shows as installed (it was dropping out of the installed,
  working and update tabs).
- **The warning text** (`core/anticheat.py:146-153`), with the product names
  joined by ", " and the evidence in brackets: "<product> is installed with
  this game. Anti-cheat and ReShade add-ons do not coexist. Expect one of:
  the game refuses to start, ReShade is blocked so nothing happens at all, or
  your account is banned. This is not something the tool can work around - it
  is the anti-cheat doing its job. If you play this game online, do not
  install here. (found: <files>)".
- **The swap warning**, shown before replacing a DLSS runtime the game ships:
  "Swapping <name> replaces a file the game itself ships. The one that is
  there is backed up and comes back when you uninstall, so nothing is lost.
  Two things to know before you do it: a launcher that verifies its files
  will simply put its own copy back, and in an online game an anti-cheat can
  treat a changed file as tampering. For a single-player game this is the
  usual way to move off an old build. For anything you play online, keep the
  game's own file."

### RE Engine

Capcom's RE Engine (the Resident Evil 2, 3 and 4 remakes, RE7, RE8 Village,
Requiem) is documented to crash on ReShade with add-on support, which every
route here needs: its own tamper checks kill ReShade the instant it loads,
worst on titles that also carry Denuvo (Resident Evil Requiem). Not an
anti-cheat in the usual sense, but the effect on the tool is the same. The
fix, confirmed by players on Requiem itself: **REFramework loaded first**
(praydog's mod, which installs itself as `dinput8.dll`, loads before the
game's own checks and patches around them, after which ReShade loads
normally as `dxgi.dll`).

- **Detected by the engine's own marker file**, `re_chunk_000.pak`, in the
  install folder, so a title nobody has told us about is covered. Any other
  engine file is not used.
  - Changed from the reference: the install folder is the executable's, and
    RE Engine keeps the marker in the game's root, which is the executable's
    folder in the games known so far. Look in the executable's folder and the
    game folder (as the anti-cheat does), so a game with its executable in a
    subfolder is still seen.
- **Warning** (`core/reengine.py:26-39`): "This looks like a Capcom RE Engine
  game (re_chunk_000.pak is in the folder) - the engine behind the Resident
  Evil 2/3/4 remakes, RE7, RE8 (Village) and Requiem. ReShade's add-on
  support, which every route here needs, is documented to crash several RE
  Engine titles the moment it loads - Resident Evil Requiem worst of all,
  since it also carries Denuvo. This is the engine's own tamper protection,
  not a mistake in the setup. This install puts REFramework in first: a
  separate, actively maintained mod that loads before the game's own checks
  and patches around them, after which ReShade survives. It is what players
  report using on Requiem itself. Not guaranteed on every title or every game
  update - REFramework's own build has to catch up first." Shown in the
  preview and the install report, except on the Remix route.
- **The component**: REFramework's **nightly** train, `praydog/REFramework-nightly`,
  newest release (`https://api.github.com/repos/praydog/REFramework-nightly/releases/latest`),
  asset named `REFramework.zip` (any case), member `dinput8.dll` extracted
  beside the game's executable. The tagged releases (one zip per game) lag
  newer titles by a long way: Requiem (codename RE9) has no build there; the
  nightly is reported to ship one universal `dinput8.dll` that detects the
  running game. Installed **before** ReShade.
- **A `dinput8.dll` already there**, REFramework or not, that the install
  record does not list as ours (by hash), is the game's or the person's: it
  is backed up and restored on uninstall by [install](install.md)'s rules. A
  `dinput8.dll` the record lists with a matching hash is ours, and uninstall
  removes it.
  - Changed from the reference: it guessed by content (a file of at least
    1 MiB that contains the bytes `REFramework` was "REFramework, not the
    game's", `refw.py:49`) and left a person's own REFramework unbacked. The
    record's hash decides, so no file is read whole to guess.
- The install, the record and the removal of this file are in the install
  spec.

### Search, hiding, sorting and filtering

- **Search** (type anywhere; Ctrl+F): the query is split on whitespace and
  lower-cased; every word must occur in the lower-cased text
  `<name> <folder> <source>`. An empty query matches everything. Typing
  re-draws the list; nothing is read from disk.
  - A search that finds nothing offers to look for a game that way: add one
    by hand (#374).
- **Hidden games**: a person hides a game (right-click). Hidden games are not
  listed and not counted in "all"; "view" shows them again; the number of
  hidden games is shown. A game opened by "add a game" that was hidden is
  brought back. The hidden list is a list of folders in the settings; a
  settings file that holds anything else reads as none.
  - Changed from the reference: folders are compared as paths ignoring case,
    as the overrides are. The reference compared exact strings
    (`core/ui/ctl_library.py:427`, `:480`).
- **Games with no executable** are not listed and are counted separately.
- **Architecture filter**: all, 64-bit only, 32-bit only (remembered). A game
  whose architecture is unknown is always shown.
- **Status filter**: all, installed, working, needs a look, update. The status
  of a card (installed, working, needs a look, update (n), "reinstall - was
  <api>", anti-cheat, unsupported, "choose architecture / api") is produced
  from the install record, the last verdict and the routes; the rules belong
  to the app spec. The tab counts count what the grid can show: a 32-bit game
  is not one of "3 installed" while the view shows 64-bit only.
- **Sort**: scan order (the default), name, store, graphics API, route,
  status; ascending, and the same column again flips it. Remembered. A
  settings value that is not a list of a known column and a boolean reads as
  the default (a hand-edited settings file raised `TypeError` and the window
  never opened).

## What must never happen

- **A leftover folder offered as a game** (Steam), or a game that is only half
  downloaded.
- **An entry that cannot be uninstalled.** A folder holding an install record
  of ours is never dropped by a scan, quick or full, even when the game is
  gone.
- **A scan that does not finish**, or takes minutes in a tree with no
  executables; or one store, one drive or one file taking the others down
  (unready drive, unreadable manifest, a malformed registry value, a
  settings file of the wrong shape).
- **A game's name sent over the network without a yes.** No lookup, no
  request, until the person has said yes; switching off stops at once, mid
  lookup.
- **A downloaded body kept as a picture without checking what it is.**
- **A picture or file path read from a file leading outside its folder.**
- **A launcher chosen over the game** when anything else is available.
- **A stale answer from the cache**: another tool version, another GPU, a
  changed executable.
- **An emulator config edit that is not [install](install.md)'s**: key by
  key, each key's old value recorded, no whole-file backup.
- **Anything the scan does touching a game folder.** The scan only reads.
- **A guess shown as a fact**: anti-cheat warnings name their file;
  protected executables are labelled as guesses.

## Bugs and questionable choices in the reference not to port

- `core/games.py:230-269`, `core/ui/art.py:50-74`: a regular expression over
  Valve's text format. The first `"name"` or `"installdir"` anywhere is taken
  and the `\\` escape is the only one handled. Use a real KeyValues parser
  (the maintained `ValveKeyValue` package is the candidate; "build on").
- `core/games.py:516-548`: Battle.net by uninstall keys whose **name** contains
  `battle.net` only reads the 64-bit view of `Uninstall` (the Battle.net
  client registers in `WOW6432Node`), and a key named `Battle.net` is the
  client itself, not a game. Unverified that any Blizzard game is found at
  all. See Open questions.
- `core/emulators.py:299-327`: the registry search tests whether the
  Add/Remove Programs name contains the **profile's name**, and three profiles
  are named `yuzu / suyu / Eden / Citron`, `Azahar / Citra / Lime3DS` and
  `Play!`: those never match. Match each executable's stem or product name.
- `core/emulators.py:217-221`, `core/games.py:1001-1006`: a profile is
  attached by file name alone, and `Play.exe`, `Dolphin.exe`, `Cemu.exe`,
  `xenia.exe` are names other programs use. A game called `Play.exe` is
  renamed "Play! (PlayStation 2)" and forced to Vulkan or OpenGL.
- `core/games.py:637`, `:659`, `:675`: an unset `LOCALAPPDATA` or `APPDATA` is
  read as an empty string, which makes a path relative to the current
  directory. A missing environment variable means the source is skipped.
- `core/games.py:565`, `:641`, `:763`: fixed lists of drive letters; two
  different lists for one machine.
- `core/games.py:25-31`, `:61`: marker file names of two older generations of
  the tool, one of them Turkish (`dlss5kur-kurulum.json`, `dosyalar`), and
  `dlss5-installer.json`. Not read: install reads version 1 records only, and
  nobody else reads record names.
- `core/games.py:495`: `import sys as _sys` and `del _sys`; `core/games.py:34`
  a global cache of rolled-back records; dead scaffolding.
- `core/games.py:358-392`: Epic manifests of DLC and add-ons that share the
  base game's `InstallLocation` are merged by folder with "first wins" and
  `glob` order is arbitrary, so the game could be listed under a DLC's name.
  Prefer the manifest whose `AppName` equals `MainGameAppName` (to verify:
  field names), and skip `bIsIncompleteInstall`.
- `core/games.py:297-316`: the Steam list includes the manifests of Steam's own
  tools (Steamworks Common Redistributables, Proton, SteamVR); they appear as
  games or as "folders with no executable". Filter by appid (to verify the
  ids).
- `core/pe.py:1245-1246`: a folder-name match on `sn in fn or fn in sn` has no
  minimum length, so an executable named `a` or `x` matches most folders.
  Candidate rule: a match counts when the shorter side has 3 or more
  characters. Not changed without a corpus check; open question.
- `core/pe.py:1182-1183`: a wall clock in the executable walk (see above).
- `core/library.py:128-130`, `:141-144`: rows keyed by `"<folder>\x00<exe>"`
  strings in JSON; use a structured key.
- `core/ui/ctl_library.py:183-188`: the search text includes the folder path, so
  "d" or "games" match every game on a drive. Intended ("search by folder"), not
  clearly wanted; open question.
- `core/ui/ctl_library.py:179`: a failed row returns `False`, a successful one a
  tuple of six values (`tuple` or `False`): a sum type.
- `core/refw.py:49`: `is_reframework` reads the entire file into memory to
  guess ownership; the record's hash replaces it.
- Names of internal gates and issue numbers in user texts.

## To verify against primary sources

| Claim | Source that settles it |
| --- | --- |
| Steam: registry keys `HKCU\Software\Valve\Steam` (`SteamPath`) and `HKLM\SOFTWARE\WOW6432Node\Valve\Steam` (`InstallPath`); `libraryfolders.vdf` shape today (an `"apps"` list per library, `"path"`); `appmanifest` fields and the `StateFlags` bits (4 fully installed; 2 update required; 1024 update started; 16, 32, 64, 128 ...) and what an update in progress carries | Valve's SteamKit and Steamworks documentation; SteamDB's EAppState; a real library |
| Steam's tools appids to skip | SteamDB (228980 Steamworks Common Redistributables, 250820 SteamVR, Proton, Steam Linux Runtime) |
| `appcache\librarycache` layout: file names, hashed subfolders since the 2025 library redesign | a current Steam client |
| Epic: `.item` fields `DisplayName`, `InstallLocation`, `LaunchExecutable`, `bIsApplication`, `bIsExecutable`, `bIsIncompleteInstall`, `AppName`, `MainGameAppName`, `CatalogItemId`, `CatalogNamespace`, `MainGameCatalogItemId`; `catcache.bin` format and `keyImages` types | the open-source clients Legendary and Heroic's docs; a real manifest |
| GOG: registry values `path`, `gameName`, `exe`, `launchCommand`; whether `exe` should be preferred | GOG Galaxy documentation; the GOG registry on a machine |
| EA app: where installs are recorded today (registry `Install Dir` under `Electronic Arts` is the Origin era; the EA app keeps its data under `ProgramData\EA Desktop`) | a machine with the EA app |
| Ubisoft Connect: `HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\<id>`, `InstallDir` with forward slashes | a machine with Ubisoft Connect |
| Battle.net: which registry or file lists an installed game and its path (uninstall keys and their names; `ProgramData\Battle.net\Agent\product.db`) | a machine with Battle.net and two games |
| Rockstar: key names, `InstallFolder` | a machine with the Rockstar launcher |
| Xbox: where Game Pass libraries live and how to list them without guessing drive letters (`MicrosoftGame.config` `ExecutableList`, the Gaming Services package repository, `PackageManager`); the `Content` subfolder; the executable protected by access denied; "Enable mods" | Microsoft GDK documentation; a Game Pass PC |
| Amazon Games: library locations (`%LOCALAPPDATA%\Amazon Games\Library` versus `C:\Amazon Games\Library`) | the apps' documentation; machines |
| Heroic: the three `installed.json` files and their shapes | Heroic's source |
| Each emulator's profile: executable names, portable markers, config paths and keys, value spellings (`settings.cpp`, `Pcsx2Config.cpp`, `MainSettings.cpp`, `Config.cpp`, `xenia_main.cc`, `retroarch.cfg`, `CemuConfig.h`, `system_config_types.h`) | each emulator's source at its current release; the renamed or retired projects (yuzu, Citra) and their forks |
| An emulator rewrites its config on exit, so an edit made while it runs is lost | run each emulator and compare |
| Steam store search API shape (`items`, `id`, `name`, `type`), `IStoreBrowseService/GetItems` with `include_assets`, `asset_url_format`, the CDN paths | Steamworks Web API docs; live requests |
| Epic CDN image URLs `https` and types `DieselGameBoxTall` etc. | a real `catcache.bin` |
| MicrosoftGame.config and appxmanifest image attribute names and scale variants | Microsoft docs |
| Anti-cheat markers: each file name belongs to its product; the lists of products | the vendors' docs; a game of each |
| `re_chunk_000.pak` is in every RE Engine title's root; REFramework-nightly's asset name and that its `dinput8.dll` is universal; that stable releases lag | the REFramework repository; the games |
| EA app install file as built (M2e): the key derivation against a real `IS` file (and whether its JSON starts with a BOM); the volume serial from `C:` or the Windows volume; which instance when a machine has two GPUs; padding or non-ASCII in WMI values; schema versions other than 21; `executableCheck` / `launchers[].exePath` as a future store pick, with an anti-cheat launcher filter | a machine with the EA app and a few installed games |
| Store sources as built (M2d): uninstall lists read in both views under `HKLM` and `HKCU`; Battle.net's uid format and its client's own entry; Rockstar's launcher and Social Club entries under the uninstall pattern; the columns of Amazon's `DbSet` table; that `MicrosoftGame.config` sits in the `Content` folder; Ubisoft's forward slashes through `Path.GetFullPath`; NTFS junctions resolved once (8.3 short names are not) | a Windows machine with each store |
| Windows: `GetDriveTypeW` removable for USB hard disks (they report fixed); error 87 on unready drives; WOW6432Node views | Win32 documentation |

## Fixtures

- **Store manifests as text**, from the old tests: Steam `.acf` strings
  (`test_scan.py:74-122`: `"StateFlags" "4"`, `1042`, leftovers, a folder with
  our record), Epic `.item` files (`test_scan.py:47-72`: flags true and false,
  none), the catalog `catcache.bin` and a manifest by install folder
  (`test_all.py:19590-19660`), an Xbox `MicrosoftGame.config` with scale
  variants (`test_all.py:19640-19700`). Missing, to write: a real
  `libraryfolders.vdf` (both shapes, with `\\` and escaped quotes), a GOG,
  EA, Ubisoft, Rockstar, Battle.net registry tree (as a fake registry
  interface), a Heroic `installed.json` for each of the three launchers.
- **A game-folder builder**: executables (by size, header bitness), folders
  (Unreal `Binaries\Win64`, Unity `_Data`, Source `bin\engine.dll` plus
  `gameinfo.txt`, Subnautica twins, GTAV launcher beside GTA5, NFS trial,
  UBOAT launcher), install records. The old checks: `test_all.py:13525-13545`
  (trial), `:14329` (UBOAT), `:15262-15280` (launcher), `:17121-17170`
  (bitness twins), `:28235-28255` (Half-Life 2 and Episode Two),
  `:25964-26003` (Unreal 1/2/3), `:7090-7095` (names).
- **Cache**: a saved library with a stamp per game; a changed executable; a
  changed folder; a protected executable; a different version; a different
  GPU; a corrupt file; a full drive on save (`test_scan.py:500-520`,
  `test_all.py:8720-8900`, `:14048`).
- **Emulator configs**: the cases at `test_all.py:3925-4100` (config text per
  emulator, expected single changed line, notes, backup, idempotence,
  restore); a config in each encoding with a BOM, CRLF, and comments.
- **Cover art**: the store search items for the match rules (the examples
  above: "The Division 2" against "The Division", "DEATH STRANDING 2: ON THE
  BEACH", edition suffixes, DLC names), the asset lists for the two Steam
  layouts, a PNG, a JPEG and BMP header, an HTML captive-portal page, a
  64 MiB-plus catalog.
- **Anti-cheat folders**: `test_all.py:7270-7340` (HoYoverse, EA Javelin,
  Elytra in its own folder, the Rise of the Tomb Raider false positives, the
  vgk and mhyprot positives).
- **Protected executable**: a folder whose executable cannot be read (test
  double that denies the open), with and without an Agility SDK beside it
  (`test_scan.py:400-560`).
- **Real corpus**: the reports under `_tools/reports/` carry `game:`, `exe:`
  and `arch/api:` lines for about 180 real installs: usable as a list of what
  real executables are called and what was found. Reports 897 (a protected
  Game Pass executable on another drive; the entry's name was "Content") and 025
  (Microsoft Flight Simulator, `? / ?`) are the Xbox cases.

## Open questions

1. **Battle.net.** The reference's method is probably wrong (see bugs).
   Options: read `ProgramData\Battle.net\Agent\product.db` (a protobuf
   file), or Blizzard's uninstall entries by publisher. Needs a machine with
   Battle.net to settle. Recommendation: verify first, then choose the smaller.
   Settled for v1: the uninstall entries (Playnite's method); verify on Windows.
2. **Xbox discovery.** Hard-coded `XboxGames` and `ModifiableWindowsApps`
   under fixed letters cannot find a Game Pass library elsewhere; Windows
   knows its libraries. Use `MicrosoftGame.config` `ExecutableList` to pick
   the executable by name as well (a documented schema, better than ranking
   a protected file)? Recommendation: yes for both, after a check on a real
   machine. Settled for v1: the folders on every fixed or removable drive
   from the drive list, and `ExecutableList` for the pick; verify on a Game
   Pass PC.
3. **The cache's version gate.** A new tool version always drops the cache,
   so every update costs a full scan. A detector version number would keep
   it across releases that change nothing in detection, at the price of
   discipline. Recommendation: keep the reference's rule until scans are fast.
4. **Naming by hand-added folders.** An Xbox `...\Content` folder added by
   hand is named "Content" because `Content` is not a generic name. Skip it
   when its parent is under `XboxGames`? Recommendation: yes (the rule
   stays "nearest non-generic", with `Content` generic only below an
   `XboxGames` game folder). Same question for the folder-name bonus (+350) has no minimum length; and the search text
   includes the folder. Keep, or constrain?
5. **Steam tool filtering** (Steamworks Common Redistributables, Proton,
   SteamVR): by appid list in code, or leave them to the "no executable" path?
   Settled: appids plus name prefixes (see the store notes).
6. **Frame of an empty library.** What does the first run show when no store
   finds anything: the reference only offers "add a game". Is there a first-run
   hint for a library on an unusual drive?
7. **Localised folder names.** The plain-folder names include `Oyunlar`,
   `Juegos`, `Spiele`, `Jeux`, `Giochi`. Keep the list as data (cheap), or
   only English plus `Games`? Settled: kept.
8. **REFramework supply chain.** The nightly is an unsigned continuous build
   with no published hash: the plan's rule is a verified hash or signer where
   publishers provide one. Pin a known-good tag, or accept and say so?
