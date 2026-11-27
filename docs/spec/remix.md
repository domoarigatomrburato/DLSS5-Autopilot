# RTX Remix

A Remix mod rebuilds an old DirectX 8/9 game with path tracing. NVIDIA's Remix
replaces the game's `d3d9.dll` with a small bridge; the real renderer, a
150-230 MB dxvk-remix build, sits in a `.trex` folder beside the executable
(GTA IV) or one folder down (Portal RTX keeps it under `bin\`). The frame is
path traced there, DLSS upscales it, and only then does the DLSS-NR (neural
rendering) snippet run. So on this route there is **no ReShade, no feeder and
no add-on**: the neural pass is a stage inside the Remix runtime. This spec
covers four things: telling which library games have a Remix mod; fetching
two mods and a community runtime; switching DLSS 5 on inside the mod's
renderer (the `remix` route); and taking all of it out again. Read from
`core/remix.py`, `core/remixdl.py`, `core/remixlist.py`,
`core/ui/remixpage.py`, the remix paths of `core/installer.py`
(`3951-4175` install, `2195-2250` preview, `5631-5668` uninstall), `core/sources.py`
(`104-117`, `924-943`), the README's "RTX Remix", and the old test sections 28,
29 and 32 (`d5801a3~1:test_all.py`). The diagnosis of Remix logs is another
spec; the log vocabulary it needs is listed at the end.

## What the user gets

### Which of my games have a Remix mod

The **remix** page names the games in the library that have a known mod, and
lists every project the tool knows. Nothing is mirrored; a project is a link
unless it is one of the two installable ones (below).

- A project is a record: game, name patterns, project name, URL, one honest
  note, and whether it is installable (`remixlist.py:24-37`). Three finished
  games ship Remix already (the *built-in* set); nineteen community projects
  are the *mods* set. Both are data, checked to exist on 2026-09-03; the URL is
  the authority, not the list.
- **Matching** a library game: the lowercase text of the game's name, its
  executable name and its folder, with `_` read as a space, is searched for
  each pattern as a substring; built-ins are tried before mods, first hit wins
  (`remixlist.py:214-248`). Nothing matched, or an empty name: none.
- A game whose folder already holds a Remix runtime is shown as "already in";
  one with a mod this tool wrote shows a remove button; others show **download
  & install** when installable, otherwise a link (`remixpage.py:36-70`).
- Rule of thumb printed on the page: Remix only reaches DirectX 8 and 9 games
  with a fixed-function pipeline, "roughly 2000 to 2005"; anything later needs a
  shim someone wrote; each game needs its own mod; "a mod existing is not the
  same as it running well". More lists: ModDB's RTX page and NVIDIA's
  compatibility wiki (`remixlist.py:198-211`).
- Changed from the reference: match on the game's name, the executable's stem
  and the **last folder name** of the game, not the whole folder path
  (`remixlist.py:214-216` joined the full path, so a user directory called
  `gmod_stuff` or a library folder `Thief Collection` matched), and use whole
  words. Why: false matches tell a user they own a game with a Remix mod.
  Known false positives of the old patterns, all to become fixtures: `thief`
  (every Thief game), `vampire`, `populous`, `garry`, `prince of persia` (the
  other Prince of Persia games), `underground 2` (any game with that name),
  `portalrtx`, `speed2`, `dungeon keeper` (the first game).

| Game | Patterns | Project | Installable |
| --- | --- | --- | --- |
| Grand Theft Auto IV | `grand theft auto iv`, `gtaiv`, `gta 4` | xoxor4d/gta4-rtx, GTAIV RTX Remix Compatibility Mod; the one the route was tested against; its runtime already carries DLSS 5 | yes |
| Need for Speed: Underground 2 | `underground 2`, `nfsu2`, `speed2` | Ekozmaster/NFSU2-RTX-Remix | yes |
| Garry's Mod | `garry`, `gmod` | Xenthio/garrys-mod-rtx-remixed; needs the game in a fixed-function mode | no |
| Deus Ex | `deus ex` | onnoj/DeusExEchelonRenderer; a renderer that gives a fixed-function pipeline first | no |
| Thief Gold | `thief gold`, `thief` | Night1099/thief-gold-rtx-remix; NewDark 1.27 | no |
| The Elder Scrolls III: Morrowind | `morrowind`, `openmw` | BrunchyChineapple/Morrowind-RTX-Remix-source; separate loose files for OpenMW | no |
| Vampire: The Masquerade - Bloodlines | `bloodlines`, `vampire` | CattoSalad/VTMB-RTX-Remix; a knowledge base rather than a one-click mod | no |
| Prince of Persia: The Sands of Time | `sands of time`, `prince of persia` | kaminoer/pop-sot-rtx | no |
| Saints Row 2 | `saints row 2`, `saintsrow2` | BRAGme/sr2-rtx-remix-proxy | no |
| Saints Row: The Third | `saints row the third`, `saintsrowthethird` | PurrsianMilkman/Saints-Row-The-Third-RTX-REMIX-compatibility-mod; the 2011 DirectX 9 release only | no |
| Red Faction | `red faction` | BRAGme/RedFaction-RTX; version 1.20 NA | no |
| Total Overdose | `total overdose` | Utkar5hM/TotalOverDoseRTXRemix | no |
| Assassin's Creed II | `assassin's creed ii`, `assassinscreediigame`, `assassin's creed 2` | Kamzik123/ac2-rtx; later than the era Remix is built for; expect rough edges | no |
| Populous: The Beginning | `populous` | xmarre/Populous-3-RTX-Remix; an experiment, in its author's words | no |
| Silent Storm | `silent storm` | WormSlayer/silent-storm-rtx | no |
| Dungeon Keeper 2 | `dungeon keeper` | mencelot/dk2-dxwrapper-with-path-tracing-support | no |
| Grand Theft Auto: Vice City | `vice city` | GmanRO/GTA-VICE-CITY-RTX-REMIX-.ASI-compiled-within-linux- | no |
| Cry of Fear | `cry of fear` | michaelabilliot/CryofFear_RTX-REMIX | no |
| Chess Titans | `chess titans` | Kamilkampfwagen-II/Chess-Titans-RTX | no |
| *built in:* Portal with RTX | `portal with rtx`, `portalrtx` | official, free for owners of Portal; Steam app 2012840 | n/a |
| *built in:* Portal: Prelude RTX | `prelude rtx` | official, free; Steam app 2410180 | n/a |
| *built in:* Half-Life 2 RTX | `half-life 2 rtx`, `hl2rtx` | official demo, free; Steam app 2477290 | n/a |

All project URLs are `https://github.com/<owner/name>`; the Steam ones are
`https://store.steampowered.com/app/<id>/`. Only two projects publish a
complete install as a plain zip on their own releases page (`installable`);
the others publish a small proxy and their INSTALL file asks the user to
download NVIDIA's runtime and rename a DLL by hand, so they stay links
(`remixlist.py:31-37`).

### Recognising a Remix install

- `find_runtime(folder)` returns the `.trex` folder under a game folder, or
  nothing (`remix.py:90-122`):
  - directories only, breadth first, **three levels** deep;
  - a `.trex` counts only when it holds `d3d9.dll` (an empty `.trex` is not a
    Remix install);
  - these folders are never entered (they hold hundreds of thousands of files
    and never a `.trex`), compared in lower case, and so is every other dot
    folder: `content paks saved logs movies sounds music videos localization
    shadercache derivedcache cache textures maps levels audio data assets mods
    screenshots steamapps redist _commonredist rtx-remix rtx_comp common update
    pc`;
  - symbolic links and junctions are not followed.
- The route looks beside the executable first, then in the game's library
  folder (`installer.py:1705-1707`).
- **Which fork** the runtime is, is *read out of the runtime binary*, never
  guessed: both community forks embed their option prefix as a plain ASCII
  string. The file (150-230 MB) is read in 4 MiB chunks with an overlap of the
  longest marker, never held whole (`remix.py:130-161`):

  | Marker found in `.trex\d3d9.dll` | Flavour | rtx.conf key to switch it on |
  | --- | --- | --- |
  | `rtx.neuralRendering` | *neural* (lunks/dxvk-remix-plus-dlssnr, a drop-in for any Remix game) | `rtx.neuralRendering.enable` |
  | `rtx.neuralUplift` | *uplift* (Kim2091's gta4-atmos-dlss5 fork, shipped inside xoxor4d's GTA IV mod) | `rtx.neuralUplift.enable` |
  | both | *neural* (the newer fork's name) | |
  | neither, or the file unreadable | none: NVIDIA's own runtime has no DLSS-NR stage | |

  Why read it: writing the wrong key would silently set an option the
  runtime never reads. The GTA IV runtime contains `rtx.neuralUplift` and
  `rtx.neuralUplift.bypassCallerCheck` and no `rtx.neuralRendering`.
- **Only the remix route works in a Remix game.** An install of any other
  route (and its preview) is refused before a byte is written: "This game has
  an RTX Remix mod installed ... Only the remix route works here: every other
  route installs ReShade, which crashes a Remix game before it draws a frame,
  and on DirectX 9 it would write over the Remix runtime itself" (`installer.py:2043-2054,
  3584-3596`). A Remix game is always recommended the remix route and nothing
  else steers it (`dlss.py:547-548`). DXVK is never offered on this route.

### The remix route: what install does

Three things, all inside the mod's `.trex` plus one config line, nothing beside
the executable (`installer.py:28-36`):

1. **`nvngx_dlssnr.dll`** into `.trex\`, the build chosen for the user's card
   (the same selection, GPU compatibility check and fallbacks as the other
   routes; a user-forced build that will not run on the card is refused; if no
   build fits: "No suitable nvngx_dlssnr build found for <card>", listing those
   tried; a mismatch the user chose to ignore installs with a warning).
   `nvngx_dlssnr.dll` has to sit *inside* `.trex`, beside the runtime.
2. **One line in `rtx.conf`**: `<key> = True` (key from the table above).
3. **Only if "swap the Remix runtime" is on**, and only where the runtime has
   no neural pass: a community runtime in place of the mod's (next section).

- **`rtx.conf` location.** An existing file wins: first beside the `.trex`'s
  parent, then in the game folder; with neither, a new one is created in
  `.trex`'s parent (the folder the game starts in), so a mod's own conf is
  edited rather than shadowed by a second copy (`remix.py:172-184`).
- **Editing it** (`set_option`, `remix.py:196-236`) is byte-safe: every other
  line is passed through untouched; the file's own line ending (CRLF if any
  CRLF is present, else LF) is reused; a line for the key (leading blanks, the
  key, optional blanks, `=`) is replaced in place by `<key> = True`, and a
  second line for the same key is dropped; otherwise the line is appended. If
  the file does not end with a newline, one is added first (a plain append once
  produced `...skyIndirectRadianceScale = 3rtx.neuralUplift.enable = True`: one
  corrupt line and two options lost). Whether it ended with a newline is
  recorded so uninstall can hand the file back in the shape it arrived in.
- **A ReShade proxy is removed.** A ReShade `dxgi.dll` killed GTAIV.exe with
  0xc0000005 before its window appeared. Every ReShade proxy (`dxgi`, `d3d9`,
  `d3d10`, `d3d11`, `d3d12`, `opengl32`, the reference's full list at
  `installer.py:631-`), under whatever name, goes: a proxy from our earlier
  install is held until commit and then deleted ([install](install.md),
  "Switching routes"), a foreign one is backed up first.
  A `d3d9.dll` is touched **only if it is ReShade**: in a Remix game that name
  is the Remix bridge client (`installer.py:3020-3035,5462-5476`).
- **Blockers** (preview and install refuse, nothing written):
  - no `.trex`: "No RTX Remix runtime (a '.trex' folder) was found in this
    game. This route is only for a game that already has an RTX Remix mod
    installed.";
  - a runtime with no neural pass and no swap: "...has no DLSS 5 neural pass
    (NVIDIA's own runtime has none). Turn on 'swap the Remix runtime' ... -
    experimental: it also replaces any game-specific fixes the mod's own
    runtime carries."
- **Preview ("what will happen?")** lists, under `.trex`, the writes
  (`nvngx_dlssnr.dll`; with the swap also `d3d9.dll` and `remix_nvngx.dll`,
  with their backups), the removals of ReShade proxies, the one line as
  `<conf> (<key> = True - this one line, nothing else in the file is touched)`,
  the install record, and the warning "the mod's own Remix runtime is being
  replaced; it is backed up and 'uninstall' puts it back" when swapping.
- **After install** the user is told: Alt+X opens the Remix menu, then
  *Developer Settings Menu*, then *Post-Processing*, where the neural pass is
  toggled (the CLI says "Enable Neural Uplift (DLSS-NR)"); it runs inside the
  runtime after DLSS, so the game's own DLSS/RR settings still apply; and that
  no ReShade, feeder or add-on goes into a Remix game. The ReShade overlay and
  its key are not mentioned on this route.
- **Reliability** (shown with the route): *beta* without the swap ("all this
  does is put nvngx_dlssnr.dll beside it and switch the pass on in rtx.conf;
  nothing is injected; only a handful of Remix mods ship a runtime with the
  pass"), *experimental* with it (see below).
- The record names the `.trex` folder, the rtx.conf path, the key, the flavour
  and the final-newline fact; `nvngx_dlssnr.dll` is in the written files. The
  rtx.conf is never in the written files (uninstall deletes those) (`installer.py:435-438`).

### The runtime swap (experimental, opt-in)

For a mod whose runtime has no neural pass. Off by default: "a mod's runtime
is often a fork carrying fixes for this exact game, and swapping it can break
the mod itself" (reports: the game did not start after the swap, or stopped at
the Remix console; its author reports it running in Portal with RTX on an RTX
4090 on Linux; not run on this tool's test machine). CLI: `--route remix
--remix-swap`.

- Fetches `d3d9.dll` and `remix_nvngx.dll` from the newest release of
  `lunks/dxvk-remix-plus-dlssnr` (API `releases/latest`) into `.trex\`.
  `remix_nvngx.dll` must keep exactly that name: the snippet checks that the
  calling module's path contains `nvngx.dll` and fails with 0xbad00002
  otherwise. The mod's originals are backed up beside as
  `<name>.invidia-claritatis-backup`; uninstall puts them back.
- When the API is out of reach, GitHub's `releases/latest/download/<name>`
  redirect resolves each asset by name; the recorded version is then the word
  `latest`. A release missing either asset: "The dxvk-remix-plus-dlssnr release
  is missing <names>."
- **The expected hash** of each runtime file is GitHub's asset `digest`, passed
  to the [net](net.md) verifier; a file that does not match never enters the
  cache.
- **Whose runtime is it** (decides renew, skip, or replace). The remix record
  holds the SHA-256 of the swapped `d3d9.dll` and `remix_nvngx.dll`:

  | State | Outcome |
  | --- | --- |
  | swap on, runtime has no neural pass | swap |
  | swap on, runtime has the pass, and its hash equals the one we swapped in | renew it ("update all") |
  | swap on, runtime has the pass and is not ours | the mod's own runtime is never replaced |
  | swap off, record says we swapped, and the hash still matches | still ours: the record keeps saying so |
  | swap off, record says we swapped, and the hash differs | the mod has updated its own runtime since: it is the mod's again, so `d3d9.dll` and `remix_nvngx.dll` leave our ownership, and the old backup is moved to `.invidia-claritatis-superseded` (not restored on uninstall, it would put the older runtime back), with a note to delete it |
  | swap on, mod updated since our swap | the old backup is moved to `.invidia-claritatis-superseded`, and the *current* runtime becomes the new backup, by [install](install.md)'s backup rule; a backup is never overwritten. A backup that cannot be made: "Could not back up the mod's own runtime before replacing it (...). Nothing was replaced." |

  Changed from the reference: judged by **SHA-256**, not by size alone
  (`installer.py:1767-1784`). Two builds of a runtime can share a size, and a
  wrong "ours" costs the backup of the mod's original. The reference also
  overwrote the old backup through a `.tmp` rename, which this rules out.
- After a swap the flavour is read again (default *neural*).

### Fetching a mod (GTA IV and NFS Underground 2 only)

For the two projects that publish a complete install as a plain zip on their own
releases page, **download & install** fetches the file the author published
and copies it into the game folder. Every other project stays a link.

- **Refusals, nothing written:**
  - the folder already holds a Remix runtime: "An RTX Remix mod is already
    installed in this folder ... Remove it yourself first ... overwriting
    somebody's working mod is not something this tool will do on its own."
    (also when the mod is not this tool's);
  - the project is not on github.com: "...cannot be fetched automatically.
    Use its own page.";
  - no `.zip` release: "This project publishes no .zip release...";
  - a zip that is not a *complete* install (next point): the long refusal that
    names the missing `.trex/d3d9.dll` and sends the user to the mod's page;
  - another job (install, uninstall, DLSS update) running: wait and press again.
- **What is "complete".** The archive carries the renderer itself: an entry
  that is `.trex/d3d9.dll` (case-insensitive) at any depth. The **mod root** is
  the directory holding that `.trex`, the shallowest if several; only that
  directory's contents are copied, relative to it. Real layouts (read off the
  published archives):

  | Archive | Root | Result |
  | --- | --- | --- |
  | GTA IV: everything under `GTAIV-Remix-CompatibilityMod/`, plus `_installer_options/...` outside it | `GTAIV-Remix-CompatibilityMod` | lands `.trex`, `d3d9.dll`, `dinput8.dll`, `dxvk.conf`, the `.asi` and the launch `.bat` files; `_installer_options` is not copied |
  | NFSU2: `.trex/d3d9.dll`, `.trex/bridge.conf`, `rtx.conf`, `dxvk.conf` at the root | `""` | all |
  | Thief Gold: `INSTALL.txt`, `d3d9.dll`, `remix-comp-proxy.ini`, `rtx.conf` | none | refused (a proxy; expects NVIDIA's runtime) |
  | Assassin's Creed II: `.trex/bridge.conf`, `dinput8.dll`, a plugin, no renderer | none | refused |
  | Saints Row 3: `dxvk.conf`, `rtx.conf`, `INSTALL.md` | none | refused (configuration only) |
  | Garry's Mod: `bin/x.dll`, `garrysmod/y`, confs | none | refused (needs its own launcher) |
  | Deus Ex: `IngestionHelper.exe`, `omniversehelper/a` | none | refused (a renderer plus dev tools) |

- **The project list** is data shipped with each release (projects, pages,
  installable asset patterns), so it is reviewed with the code and changing
  it needs a release.
- **The expected hash** of a mod zip is GitHub's asset `digest` from the
  release answer, passed to the [net](net.md) verifier. A release read from
  the github.com pages (no digest) is not fetched: "GitHub's API could not be
  reached, so this mod cannot be verified - try again later".
- **Which release and asset.** Up to the ten newest releases, newest first; the
  reference took the first `.zip` asset it met. Changed from the reference: each
  installable project carries the asset-name pattern of its install zip in the
  project data, and that asset is the one fetched; the completeness check
  stays as the final guard. Why: `resolve` (`remixdl.py:140-170`) claimed to
  consider every zip but returned the first, so a release that also ships a
  dev-tools zip first would be refused although another asset is the mod.
- **Size.** Zips up to 2 GiB (GitHub's asset cap; one real mod zip is 524 MB),
  with the declared length checked first and the limit enforced while
  streaming, as in the net spec. The cache name is built from the repo, tag and
  asset and validated as a cache file name.
- **Copy and record.** For each member under the root: the destination is
  checked to stay inside the game folder ("The archive tries to write outside
  the game folder (<name>) - refused."); a file that already exists is first
  backed up as `<name>.invidia-claritatis-backup`; then written in 1 MiB chunks.
  A backup is never overwritten: when one is already there (left by a
  half-removed earlier mod), the fetch refuses before anything is written,
  naming the file: "An older backup of <name> is still here; remove the
  earlier mod first." Progress: download as (bytes, total),
  unpacking as a percentage with "unpacking i/n" when there are more than 50
  files, every 25 files. The log names the asset, tag and size, the root, and
  the first six top-level names written.
- **The record** `dlss5-remix-mod.json` in the game folder, kept apart from
  the DLSS 5 install record on purpose (the mod is not our install; removing
  DLSS 5 must never remove somebody's mod, and vice versa): `mod_url`, `tag`,
  `asset`, `files` (relative, `/`), `backups`, `complete` (false after a
  failure part way). It is written whatever happens, so a failed copy still
  leaves a record that can take the mod out.
  - Changed from the reference: the record is on disk **before** the first
    existing file is overwritten, and names every file that may be touched and
    every backup made, so a crash, a power cut or a killed process leaves a
    record that is correct. The reference wrote it in a `finally` after the
    copy, which does not run on a kill. The takeover rule "an install recorded
    before it writes" applies. Writes of the record are atomic and flushed.
- **Remove the mod** (button, with a confirmation naming the project, the
  number of files and what stays): needs the record ("No record of a Remix mod
  installed by this tool in this folder, so there is nothing safe to remove.");
  restores each backup over its file, deletes the other recorded files, then
  removes the directories the mod created, deepest first, then the record. The
  game's own files and a DLSS 5 install in the same folder are left alone.
  - Changed from the reference: a backup is restored by **moving** it back
    (`remixdl.py:305-308` copied then deleted, and a failure part way lost
    the backup); every recorded path is checked to stay inside the game
    folder before it is used (`remove` trusted the record: a damaged or edited
    record could delete outside); a file whose content no longer matches what
    was written (size and hash recorded at install) is **kept and listed**, not
    deleted, because the user or the DLSS 5 install edited it (the remix route
    adds a line to the same `rtx.conf`); and every emptied parent directory is
    pruned, not only the immediate one (`remixdl.py:324-332`).
- The game is re-read after a fetch or a removal and appears on the remix
  route by itself (the reference said "rescan and the game comes up").

### Uninstall of the remix route

From the install record alone, and it restores in this order (`installer.py:5631-5668`):

- every file in the written list is deleted: `nvngx_dlssnr.dll` inside `.trex`,
  and the swapped runtime files, each replaced by its backup when one exists
  (the mod's originals come back);
- ReShade proxies we backed up come back, ours are gone;
- the **one rtx.conf line** is removed by name (never by deleting the file),
  using the recorded relative path; a path that resolves outside the game
  folder or its roots is ignored with "ignored a Remix configuration path
  outside the game folder"; when the file did not end with a newline before
  the install, the trailing newline is taken away so the file is byte for byte
  what it was;
- in a Remix game, `d3d9.dll` is never deleted by the no-record fallback
  ("RTX Remix is installed here: d3d9.dll is its runtime, left alone"): with no
  record to tell the bridge from ours, a file left behind is harmless and a
  deleted runtime is not.
- Changed from the reference: the install records the line the key had before
  (the value the mod shipped, or "absent"), and uninstall puts that line back
  instead of deleting the key (`remix.py:196-236,249-274`). Why: a mod that
  shipped `rtx.neuralUplift.enable = False` loses its own setting on
  uninstall, and the runtime's default may differ.
- Changed from the reference: rtx.conf is written through a temporary file and
  renamed (atomic, flushed), since it is the user's or a mod author's file;
  a failure leaves it untouched and says "Close the game and the Remix
  toolkit, then try again".

### What the runtime says (for the diagnosis)

The log is `<game>\rtx-remix\logs\remix-dxvk.log`. Two fork vocabularies exist;
a diagnosis that knows only one reads the other's session as "never attempted"
(a swapped-runtime log shared as failed, 0 of 4; old test section around
5553). Quoted from the fork's sources, verified against strings in a real
runtime (`remix.py:287-378`):

| Line (substring) | Meaning |
| --- | --- |
| `[DLSS-NR] Loaded ` | snippet loaded (Kim2091 fork) |
| `[DLSS-NR] Snippet initialized` | initialised |
| `[DLSS-NR] Created the <name> feature (id N, preset P) at WxH` | the pass is running; `at WxH` optional |
| `NVIDIA DLSS-NR snippet loaded from ` | loaded (lunks runtime, no `[DLSS-NR]` prefix) |
| `NVIDIA DLSS-NR evaluated (count=N` | working, with the evaluation count |
| `NVIDIA DLSS-NR inactive:` / `NVIDIA DLSS-NR skipped: <reason>` | switched off / skipped |

Failure phrases and what to tell the user (each maps to a next step):

| Phrase | Advice, in substance |
| --- | --- |
| `nvngx_dlssnr.dll could not be loaded` | Windows refused it: a build for another card or antivirus quarantine; install again, the tool picks a matching build |
| `nvngx_dlssnr.dll was not found` / `... not found` | it must sit inside `.trex`; install again |
| `nvngx_dlssnr.dll does not export the Vulkan NGX entry points` / `does not export the full NVSDK_NGX_VULKAN_* surface` | wrong build: Remix renders on Vulkan; install again, let the tool pick |
| `snippet failed to load` | check the DLL is in `.trex` and the driver is 570 or newer |
| `the caller check could not be bypassed` | `remix_nvngx.dll` must keep exactly that name |
| `[DLSS-NR] Failed to create the Neural Uplift feature` / `Failed to create DLSS-NR feature:` | NGX refused: build does not match the card; install again |
| `NVIDIA DLSS-NR not available:` | the reason is on the same line; file missing or build for another card; if it repeats, report |
| `NVSDK_NGX_VULKAN_Init_Ext failed for DLSS-NR:` / `AllocateParameters failed for DLSS-NR:` / `NVSDK_NGX_VULKAN_EvaluateFeature failed for DLSS-NR:` | NGX would not start, hand out parameters, or failed on first frames; reason on the line |

## What must never happen

- A ReShade, feeder or add-on file written into a Remix game; a ReShade proxy
  left in one. Any other route installing there.
- The mod's runtime replaced without the user's opt-in, or replaced when it
  already has the neural pass and is not ours.
- A mod's own file overwritten without a backup, or another person's Remix
  mod written over (the fetch refuses when a runtime is present).
- `rtx.conf` rewritten beyond the one key; its line endings changed; its last
  line glued to the new one; the file deleted by uninstall.
- A mod removal that removes the DLSS 5 install, or an uninstall that removes
  the mod. The two records stay apart.
- A path in a record or an archive that leaves the game folder.
- A partial install without a record that can take it out.
- A non-complete archive (a proxy that expects NVIDIA's runtime) dropped in.

## Bugs and questionable choices in the reference not to port

- `remixdl.py:183-279`: record written only at the end, in `finally` (above).
- `remixdl.py:291-338`: remove trusts record paths, copies instead of moves,
  deletes edited files, prunes one directory level (above).
- `remixdl.py:140-170`: first `.zip` wins (above).
- `remixdl.py:95-108`: a name ending `.trex/d3d9.dll` also matches
  `foo.trex/d3d9.dll` (a folder called `foo.trex`), and member names with
  backslashes are not read. Match the path component `.trex` exactly, with
  both slashes read the way Windows reads them (the net spec's rule).
- `remixdl.py:232`: an existing file is backed up only when no backup is there
  yet; a stale backup from a half-removed earlier mod lets the file be
  overwritten unsaved. The fetch refuses instead (above).
- `remix.py:196-274`: lost original value of the key (above); non-atomic writes.
- `remix.py:50-82`: the skip list contains plain words (`data`, `pc`, `common`,
  `update`, `mods`) a game may keep its `.trex` under; the match is by
  depth-3 breadth-first search. Keep the walk but treat the list as a data
  question (see open questions).
- `sources.py:933-936`: a failed API call of *any* kind falls to the
  `latest` redirect and records the version as `latest`, which cannot be
  compared later. Resolve the redirect to its tag (a HEAD on `/releases/latest`
  reads it from `Location`) and record that.
- The runtime swap downloads two executables from one maintainer with no hash
  or signer check and no size limit. Changed: each is checked against
  GitHub's asset digest and the net spec's limits (above).
- `remixlist.py:7-12`: the list says "every entry was checked on 2026-09-03";
  it ships inside the program, so a moved project is a stale link until a new
  release. Kept: the list is shipped data and is checked at each release.
- `remixlist.py:206-211`: the page states "anything after 2010
  will not work": unverified and contradicted by the list's own entries
  (Assassin's Creed II, Saints Row: The Third); say what NVIDIA's wiki says.

## To verify against primary sources

| Claim | Source that settles it |
| --- | --- |
| Remix replaces `d3d9.dll` with a bridge client; the runtime lives in `.trex` beside the exe (or under `bin\`); the bridge is `d3d9.dll`, and `rtx.conf` is read from the game's working directory | NVIDIA RTX Remix runtime docs and its GitHub wiki (NVIDIAGameWorks/rtx-remix) |
| the in-game developer menu is Alt+X, and the neural pass is in Developer Settings Menu > Post-Processing | the fork's docs; the Remix toolkit manual for the menu key |
| the option prefixes `rtx.neuralUplift` / `rtx.neuralRendering` and `.enable`; each fork embeds its prefix as a plain string | the forks' sources (Kim2091/dxvk-remix branch gta4-atmos-dlss5, `rtx_neural_uplift.cpp`, `rtx_ngx_wrapper.cpp`; lunks/dxvk-remix-plus-dlssnr) and a scan of one real runtime each |
| the caller check on `nvngx.dll` in the module path and error `0xbad00002`; `remix_nvngx.dll` must keep its name | the fork's source |
| driver 570 or newer, Vulkan NGX entry points, and that Remix renders on Vulkan | NVIDIA NGX documentation, the fork's README |
| a 32-bit ReShade `dxgi.dll` crashes GTAIV.exe with 0xc0000005; ReShade crashes Remix games in general | ReShade and Remix issue trackers; a reproduction |
| asset names `d3d9.dll`, `remix_nvngx.dll` in lunks' release; lunks' runtime ships no `remix_nvngx.dll` for a mod whose runtime has no pass | the release page |
| the two installable projects' zips (`GTAIV-Remix-CompatibilityMod/`, NFSU2 root layout) | the releases of xoxor4d/gta4-rtx and Ekozmaster/NFSU2-RTX-Remix, re-read before each release of the tool |
| GitHub's per-asset cap of 2 GiB | GitHub docs on release assets |
| that releases expose an asset digest (`sha256:`) that can verify the downloads | GitHub REST "release assets" schema |
| which games Remix supports (DirectX 8 and 9, fixed function) | NVIDIA's Compatibility wiki page |

## Fixtures

- Fake Remix game folder: `.trex\d3d9.dll` with the marker `rtx.neuralUplift`
  (and variants: `rtx.neuralRendering`, both, none) and an `rtx.conf` that
  does **not** end with a newline (`...skyIndirectRadianceScale = 3`), in CRLF
  and LF; with a proxy `dxgi.dll` (ReShade) and with a `d3d9.dll` that is the
  bridge (old tests 5256-5420, 5421-5550, 5671-5760).
- `rtx.conf` cases: key absent, present once, present twice, present with
  different spacing, value `False`, and a file with a mod's own comments.
- The seven archive layouts in the table above, as lists of names; plus a
  built zip with `TheMod/.trex/d3d9.dll`, `TheMod/d3d9.dll`, `TheMod/rtx.conf`,
  `TheMod/own.cfg` and a pre-existing `own.cfg` (install, then remove: the
  folder must be as before), a not-a-mod zip, a zip with a `../x` member, and a
  folder that already has a `.trex` (the fetch must refuse and write nothing).
- Real release JSON: `releases?per_page=10` of xoxor4d/gta4-rtx and
  Ekozmaster/NFSU2-RTX-Remix; `releases/latest` of lunks/dxvk-remix-plus-dlssnr
  (to be captured; none is in the reference).
- A log of each vocabulary (the corpus has two Remix reports under
  `_tools/reports/`) and a synthetic one per failure phrase.
- Library entries for the matching rules: each project's real Steam folder and
  exe names, and the false-positive titles above.

## Open questions

1. **Should the remix route stay one route with an opt-in swap**, or split into
   two choices in the UI ("use the DLSS 5 runtime the mod has" / "replace the
   runtime")? The model today is one route and a checkbox.
2. **Mod removal when DLSS 5 edited the same `rtx.conf`.** With the "keep files
   changed since install" rule above, removing a mod after installing DLSS 5
   keeps `rtx.conf` and says so. Acceptable, or take the DLSS 5 line out
   first, then remove the mod?
3. **The skip list and the depth of three** in the runtime search: keep as is,
   or search from the executable's folder and its parent only?
4. **More installable projects.** Only two are installable because the others
   publish proxies. Add others when their release archives become complete, by
   a data change alone?
