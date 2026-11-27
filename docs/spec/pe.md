# Reading a game's executable

The tool decides what to install from what a game's executable and its folder
say about the game: whether it is 32-bit or 64-bit, which graphics DLLs it
links, which version a DLL is stamped with, and from these the graphics API
the game draws with. Nothing is executed; files are only read, and the
answers are explained in words so a wrong one can be checked. This spec
covers that reading and the API decision. Choosing which executable of a
folder to read is in [library](library.md). Read from the reference's
`core/pe.py` (all of it) and `core/games.py` (the overrides), and from tests
written against them.

## What the user gets

### Reading rules

- **Only the needed bytes are read.** Real executables exceed 500 MB
  (Deathloop.exe is 486 MB) and the import table can sit near the end. Every
  read is a bounded range: the headers, the section table, the descriptors,
  one short name at a time. A file is never loaded whole.
  - Changed from the reference: the Agility SDK export reader loaded up to
    64 MiB of the file into memory (`core/pe.py:1428-1434`) and caught every
    exception around it. Reading by range everywhere is simpler to bound and
    to test.
- **Build on the BCL, write only the missing piece.** The runtime's own PE
  reader (`System.Reflection.PortableExecutable`) gives the headers, sections
  and data directories on every host. The import, delay-import, export and
  version-resource parsers are the part to write, over ranged reads (about
  150 lines). No PeNet or other PE package: none is shown to read large
  files lazily and to be safe on hostile input, and the missing piece is
  small.
- **Unparsable is not the same as empty.** A file that is not a PE, that
  has no PE header, or that cannot be read is an error with a stated cause. A
  PE that simply has no graphics import is a normal answer, "no static
  graphics import".
  - Changed from the reference: `pe_imports` returned an empty list for every
    failure (`core/pe.py:248-249`), so a corrupt file and a game that loads
    its renderer at run time looked the same, and both went on to "Unknown".

### Architecture and bitness

| Input | Result | Evidence |
| --- | --- | --- |
| COFF `Machine` 0x8664 | 64-bit | `core/pe.py:128-129` |
| COFF `Machine` 0x014C | 32-bit | `core/pe.py:130-131` |
| any other `Machine` | error "Unsupported machine type: 0x%04x" | `core/pe.py:132` |
| no `MZ` at 0, or file shorter than 0x40 | error "Not a Windows executable (no MZ signature)." | `core/pe.py:118-119` |
| `e_lfanew` (at 0x3C) does not point at `PE\0\0` | error "No PE header found." | `core/pe.py:122-124` |
| the file cannot be opened | error "Cannot read <name>: <reason>" | `core/pe.py:126-127` |
| the file can be listed but its content is access-denied | **protected executable**, not an error (see below) | `core/games.py:138-157` |

Only the first 0x40 bytes and six bytes at the PE header are read.

- Changed from the reference: an optional-header magic that disagrees with
  `Machine` (0x10B with 0x8664, or 0x20B with 0x014C) is reported as a
  corrupt header. The loader refuses such a file, so calling it a game would
  mislead. Low stakes; no reference test relies on it.
- ARM64 (0xAA64) and ARM64EC (0xA641) are named in the "unsupported" error
  instead of a bare hex number. See Open questions.

### Protected executables

Windows can refuse to open a store game's executable (Xbox / Game Pass,
`WindowsApps`) with access denied while the folder beside it is readable and
writable.

- The check is one byte read of the executable; only a permission error
  counts. Any other error is not "protected".
  - A protected executable yields no architecture and no imports. The
    graphics API is then guessed from the files beside it (the rules below
    still apply with no imports) and the game carries a **protected
    executable** notice instead of an error.
  - The notice has three wordings, because the Xbox app means nothing to a
    Game Pass library on another drive (`core/games.py:81-118`):

    | Where | Wording to keep (meaning) |
    | --- | --- |
    | executable under `XboxGames` or `WindowsApps` | Windows protects this Xbox game's executable so the tool cannot read it; choose 64-bit or 32-bit in the game's details (almost every Game Pass game is 64-bit) and check the graphics API there; that guess comes from the files beside the game |
    | anywhere else | the same, without naming Xbox |
    | the game's settings, once chosen | architecture is the one chosen, the API is chosen or guessed from the files beside the game; neither is read from the game |
  - The choices (architecture 32 or 64, graphics API) are stored per game
    folder and live in [library](library.md). A readable executable ignores
    a stored architecture and clears the notice.
- A corrupt but readable executable stays an error even when both choices
  are stored (`test_scan.py:540`, "corrupt readable xbox exe stays fatal").

### Imports

- **Static imports.** Data directory 1. **Delay-load imports.** Data
  directory 13. Both give DLL names, lower-cased, from the descriptor tables.
  - PE32 (magic 0x10B) and PE32+ (0x20B) both. A directory count at or below
    the wanted index means the table does not exist, whatever bytes follow
    (`core/pe.py:187-194`).
  - An RVA is mapped through the section table; an RVA in no section is
    skipped, not an error. A name is at most 256 bytes, ASCII.
  - A delay-load descriptor whose attributes have bit 0 clear holds virtual
    addresses from an old linker, which cannot be resolved without the image
    base; those are skipped (`core/pe.py:229-233`).
  - The scan stops at the all-zero terminator or after 1024 descriptors.
- **A delay import is a dependency.** The linker recorded it; the loader
  resolves it on first use. Grand Theft Auto V links d3d9.dll statically and
  delay-loads the renderer it draws with (issue #77).
- **Matching is by whole file name, ignoring case.**
  - Changed from the reference: it tested substrings (`core/pe.py:450`,
    `any(d in i for i in imports)`), so a name that merely contains
    `d3d11.dll` matched. An import names a file exactly.

### Version stamped in a DLL

- **File version** is read from the file's own `VS_FIXEDFILEINFO`
  (signature 0xFEEF04BD) in its version resource, the `ms`/`ls` words as four
  numbers. It is what NVIDIA stamps with the DLSS build number, and the only
  way to know what is in a game folder now: the install record says what the
  tool wrote, and a launcher that verifies its files puts its own copy back
  without telling anybody (`core/pe.py:63-72`).
- **A trailing zero is dropped down to three parts**: 310.8.0.0 is "310.8.0";
  310.8.1.0 is "310.8.1"; 3.7.20.0 is "3.7.20"; 1.2.3.4 stays four parts
  (`core/pe.py:101-104`).
- **No version resource, no fixed block, a block shorter than 52 bytes or
  with the wrong signature: the answer is the empty string**, not an error.
- Changed from the reference: it called the Windows version API and
  returned "" on every other host (`core/pe.py:75-76`), so no test of it
  could run off Windows and the test suite had to write DLLs Windows would
  accept (`tests/fake_pe.py`). Parse the resource tree in the file: resource
  directory (data directory 2), type 16 (RT_VERSION), first entry. The result
  must equal what Windows reports for the same file on a sample of real DLLs
  (see To verify).

### The D3D12 Agility SDK request

A 64-bit D3D12 game can ask Windows for a newer D3D12 runtime than the one
installed, by exporting two symbols the D3D12 loader reads from the process
executable (`core/pe.py:1428-1474`):

- `D3D12SDKVersion`: a 32-bit number, the SDK build asked for.
- `D3D12SDKPath`: a string, a path relative to the executable's folder.

**Unmet request** means: the executable exports `D3D12SDKVersion`; no
`D3D12Core.dll` exists at the exported path (an empty or unreadable path
means `.`, leading and trailing slashes of either kind stripped); and
Windows' own `System32\D3D12Core.dll` exports a version lower than the one
asked for. Any doubt (no export, no system DLL, anything unreadable) is "met".
The verdict is a sentence naming the executable, the requested version, the
path, and the version this Windows has (`core/pe.py:790-794`). Seen on Age of
Empires III Definitive Edition (version 4 at `.\` on Windows 10, issue
#1035). The install preview and diagnosis use it to say why no D3D12 device
can be made.

- The verdict is computed once per scan of a game and kept with the scan
  result. Changed from the reference: it kept a process-wide dictionary keyed
  on path, time and size because the preview asked on every redraw
  (`core/pe.py:765-775`); that is a workaround for the caller's design.

## The graphics API decision

The result is three things: a **label**, a **reason code**, and the reason
in words that names the evidence.

- Labels: `DX8`, `DX9`, `DX10`, `DX11`, `DX12`, `DXGI`, `Vulkan`, `OpenGL`,
  and `Unknown`. `DXGI` means "Direct3D 11 or 12, not settled": a game that
  imports only `dxgi.dll`, an Unreal 4/5 layout with no import, a run-time
  `dxgi` name. All but `DXGI` and `Unknown` are what a person may pick by
  hand (`core/games.py:888`); the person settles a `DXGI` game as DX11 or
  DX12, and the [routes](routes.md) spec says what each label is offered.
  - Changed from the reference: these games were labelled DX12, although
    DX11 is common (UE4 defaults to D3D11), so the recommended route took
    D3D12-only routes for games that may draw with D3D11.
- **Confidence**, one of four, read from the reason code:

  | Confidence | Reason codes |
  | --- | --- |
  | `read`: a graphics DLL in the executable's own tables | `static-import`, `delay-load` |
  | `inferred`: files beside it decide | `agility-sdk`, `unreal-plugin`, `dlss-files`, `sibling-import` |
  | `guessed`: an engine layout or a name in the bytes | `unity`, `unreal`, `unreal-3`, `unreal-classic`, `run-time-name`, `sibling-string` |
  | `none` | `none` |

  The UI says "a guess" for `guessed` and offers the API choice first; the
  routes spec shows it when it asks the person.
- Changed from the reference: it returned only the label and a free-text
  reason, and other code took decisions on the label alone. The reason code
  (for example `static-import`, `agility-sdk`, `dlss-files`, `delay-load`,
  `unity`, `unreal`, `unreal-3`, `unreal-classic`, `run-time-name`,
  `sibling-import`, `sibling-string`, `none`) lets diagnosis and tests rely
  on a value, not on a sentence.
- **`Unknown` means the file carried no evidence.** The reason says "graphics
  DLL loaded at runtime; assuming DX11/DX12 via dxgi.dll". Downstream
  treats a 64-bit `Unknown` like a DXGI game (`core/installer.py:603`,
  `core/ui/ctl_game.py:577`); that is the routes spec's rule, not this one's.
  For a protected executable, `Unknown` must not be assumed: the person
  chooses (`test_scan.py:422`).

### Why order matters

Many Unreal games run on DX12 yet link `opengl32.dll` for unrelated reasons
(Hell is Us, Fatekeeper import both `dxgi.dll` and `opengl32.dll`). Checking
OpenGL first would pick the wrong proxy DLL and break the game, so the
presence of a DXGI-family import decides before OpenGL is looked at
(`core/pe.py:436-446`). Confusing DX11 with DX12 does not change the proxy
DLL ReShade goes in as (`dxgi.dll` either way), but it does change the
route recommended, because the native, upstream and OptiScaler routes need
D3D12.

### The rules, in order

The first rule that applies decides. "Evidence beside the executable" is
defined after the table.

| # | If the executable | Result | Reason code | Evidence |
| --- | --- | --- | --- | --- |
| 1 | statically imports `d3d12.dll` | DX12 | static-import | `core/pe.py:459-460` |
| 2 | statically imports `d3d11.dll` and the folder has a D3D12 Agility SDK or the game's own frame-generation/ray-reconstruction DLL | DX12 | agility-sdk | `core/pe.py:461-468` |
| 3 | as 2, and an Unreal plugin folder ships `nvngx_dlssg.dll` or `nvngx_dlssd.dll` | DX12 | unreal-plugin | `core/pe.py:469-476` |
| 4 | statically imports `d3d11.dll`, none of the above | DX11 | static-import | `core/pe.py:477` |
| 5 | statically imports `d3d10.dll`, `d3d10_1.dll` or `d3d10core.dll` | DX10 | static-import | `core/pe.py:478-482` |
| 6 | statically imports `dxgi.dll` (no d3d11/12/10) | DXGI | static-import | `core/pe.py:484-485` |
| 7 | statically imports `vulkan-1.dll` | Vulkan | static-import | `core/pe.py:487-488` |
| 8 | statically imports `opengl32.dll` | an engine rule if one applies, else OpenGL | engine or static-import | `core/pe.py:489-493` |
| 9 | statically imports `d3d9.dll` | the d3d9 sequence below | per step | `core/pe.py:494-571` |
| 10 | statically imports `d3d8.dll` | DX8 | static-import | `core/pe.py:572-575` |
| 11 | imports no graphics DLL and the folder has a D3D12 Agility SDK or the game's own DLSS-G/RR DLL | DX12 | agility-sdk | `core/pe.py:576-582` |
| 12 | imports no graphics DLL statically and the delay-load table names one | that API, by the static table's ranking (`d3d12`, `d3d11`, the `d3d10` family, `dxgi`, `vulkan-1`, `opengl32`, `d3d9`, `d3d8`) | delay-load | changed from the reference, below |
| 13 | imports no graphics DLL and the run-time search names a renderer | that renderer | per source | `core/pe.py:583-594` |
| 14 | nothing above | Unknown | none | `core/pe.py:595` |

- Changed from the reference: rule 12. The reference looked at the
  delay-load table only inside the d3d9 sequence, so an executable that
  delay-loads its renderer and links no graphics DLL statically reached the
  run-time search and was called a guess ("named in the exe"), although the
  linker recorded the dependency. It sits after rule 11 so that the Agility
  SDK beside the executable still settles DX12, as rule 2 does for a static
  `d3d11.dll`.

After the table, a result of `DX8` for a **64-bit** executable becomes
`Unknown`, with the reason "... but DirectX 8 is 32-bit only, and this exe is
64-bit". Windows ships no 64-bit `d3d8.dll`, so a 64-bit executable that
names it does not draw with it, and reading it as DX8 sent a 64-bit game down
the 32-bit DXVK path (`core/pe.py:416-432`). A person may still set DX8 by
hand; the install check then refuses a 64-bit executable
(`core/installer.py:775-782`).

### The d3d9 sequence

A `d3d9.dll` import is the weakest evidence in the file: engines keep it for
a launcher, a video player or an old settings dialog long after they stop
drawing with it. Red Dead Redemption 2 imports d3d9.dll and no DXGI at all
and renders with D3D12 (issue #12); Grand Theft Auto V was read as DirectX 9
(#77). So every other kind of evidence is asked first, in this order, and
the first hit decides (`core/pe.py:494-571`):

| Step | Evidence | Result |
| --- | --- | --- |
| 1 | the game's own `nvngx_dlss.dll`, `nvngx_dlssg.dll` or `nvngx_dlssd.dll` beside it, or a `D3D12\D3D12Core.dll` that fits its bitness | DX12, reason also says "if the game is set to Vulkan, pick that in the settings before installing" |
| 2 | the **delay-load** table names `d3d12.dll` | DX12 |
| 3 | the delay-load table names `d3d11.dll` | DX11 |
| 4 | only for a **64-bit** executable: a run-time search returns a record, not a string (an engine rule, a marker file, or another DLL's import table), naming `d3d12.dll`, `d3d11.dll`, or a DX11/DX12 engine verdict (an Unreal 4/5 layout gives `DXGI`) | that API |
| 5 | none | DX9 ("imports d3d9.dll, no DXGI") |

- A bare `dxgi.dll` in the delay-load table does not count at step 2 or 3: a
  DirectX 9 game can use DXGI only to enumerate displays (`core/pe.py:520-523`).
- Step 4 excludes mere **strings** in the executable ("named in the exe").
  Every executable here has "d3d9.dll" in its own bytes, because the import
  table's name string is there, and SDL2 carries the literal `d3d11.dll` for
  its render backend, so a string would call an SDL game DX11 and send it to
  a dxgi proxy it never loads (`core/pe.py:536-545`). A 32-bit game that
  reaches step 5 is DirectX 9 in every case seen and has the most to lose from
  a wrong answer, so step 4 skips it.
- Step 1 covers the Agility SDK too (it was a separate step in the
  reference, which counted the game's own `nvngx_dlssg.dll` or
  `nvngx_dlssd.dll` as Agility evidence even beside another DLSS tool).
- `ddraw.dll`-only games (DirectX 7) are not a label; see the Unreal 1 rule.

### Evidence beside the executable

All of these read files in the executable's own folder (not its tree)
unless stated, and all of them are limited by bitness.

- **Bitness fit.** A 32-bit executable cannot load a 64-bit DLL, so for a
  32-bit executable only a 32-bit file is evidence; an unreadable file is
  none; an unknown executable bitness keeps every file (`core/pe.py:264-278`).
  A folder can hold both builds of a game: Subnautica ships Subnautica.exe
  and Subnautica32.exe, and the 64-bit build's runtimes labelled the 32-bit
  one DX12 (issue #190).
- **D3D12 Agility SDK** beside the executable: `D3D12\D3D12Core.dll` (fits),
  or the game's own `nvngx_dlssg.dll` or `nvngx_dlssd.dll`. The SDK is loaded
  at run time through `SetD3D12SDKPath`, so a game that statically links only
  `d3d11.dll` can be a D3D12 title; Resident Evil Requiem ships DLSS frame
  generation and ray reconstruction (DX12-only features) as further evidence.
- **The game's own DLSS runtime.** `nvngx_dlss.dll`, `nvngx_dlssg.dll`,
  `nvngx_dlssd.dll`, or the Agility SDK (reason "a D3D12 Agility SDK"). NGX
  runs on D3D11, D3D12 and Vulkan and never on DirectX 9.
- **A file of ours is not the game's.** The standalone route writes
  `nvngx_dlssg.dll` itself, and a DirectX 11 game it was installed into read
  as D3D12 on the next scan (issue #458, Mass Effect Andromeda, whose add-on
  log says D3D11). Every ReShade route also places `nvngx_dlss.dll` beside a
  64-bit game. So:
  - names listed in the install record in that folder, read through
    install's reader (which also reads the Python release's
    `dlss5-autopilot.json`), compared by file name, slashes of either kind,
    are ours;
  - except a file whose original was backed up beside it
    (`<name>.invidia-claritatis-backup`, or the Python release's
    `<name>.dlss5-autopilot-backup`): the game shipped that one, and "does
    the game ship DLSS" is about the game, not the file now on disk;
  - the record is read as untrusted JSON; a bad one means "nothing is ours".
- **Another DLSS tool in the folder** brings NGX files of its own. A
  swapper's `nvngx_dlss.dll` beside Arkham Knight (DirectX 11) read as "the
  game ships DLSS" (#250). With one of the files below present (not counting
  ours), the NGX files say nothing about the game (`nvngx_dlssg.dll` and
  `nvngx_dlssd.dll` included: frame-generation mods ship their own), and only
  `D3D12\D3D12Core.dll` still does:
  - foreign files: `optiscaler.ini`, `nvngx.ini`, `fakenvapi.ini`,
    `dlss-enabler.dll`, `dlss-enabler-upscaler.dll`, `nvngx-wrapper.dll`,
    `dlssg_to_fsr3_amd_is_better.dll`, `dlssg_to_fsr3.ini`,
    `nvngx.dll_dlssnr.dll`, `dlssg_sm86.ini`, `nvngx_dlssnr.ini`,
    `gitc-uplift-helper64.exe`, `uplift-helper64.exe`;
  - a `.addon64` or `.addon32` file whose name contains `dlss`, `ngx` or
    `swapper`, unless it is one of ours: `dlss5-feed`, `dlss5-bridge`,
    `renodx-dlss5`, `renodx-dlss`, `nvngx.dll`, `standalone-dlssnr`,
    `rtx40mfg-ui`, `dlss5-feed-helper`, `dlss5-dx11-bridge` (each with
    `.addon64`, the feed also `.addon32`);
  - a RenoDX HDR add-on brings no NGX file, so it is not on the list; only a
    name saying DLSS or NGX counts. What the optiscaler route itself writes
    (`OptiScaler.ini`, `nvngx.dll_dlssnr.dll`) is ours, or a game read as DX12
    from its DLSS files would go back to DX9 on the next scan ("reinstall,
    was DX12").
- **Unreal plugin files.** An Unreal game keeps NVIDIA's plugins under
  `<root>\Engine\Plugins\Runtime\Nvidia\<plugin>\Binaries\ThirdParty\Win64`,
  not beside the executable. Only fixed paths are read, no walk: plugins
  `Streamline`, `DLSS`, `StreamlineCore`, `StreamlineDLSSG`; files
  `nvngx_dlssg.dll`, `nvngx_dlssd.dll`. Atomic Heart imports d3d11.dll, draws
  with D3D12 and got the wrong route (#1014).
- **An Unreal layout** is by shape: the executable's parent is one of
  `Win64`, `WinGDK`, `WinARM64` (any case), its grandparent is `Binaries`,
  and a folder `Engine` exists beside the project folder, three levels up
  (`core/pe.py:821-833`).

### Engine rules that settle the renderer by themselves

Consulted at rule 8 (OpenGL import) and rule 13 (nothing imported), and
inside the d3d9 sequence step 4 where they may only say DX11 or DX12. Order:

| Engine | Detected by | Result | Evidence |
| --- | --- | --- | --- |
| Unity | `UnityPlayer.dll` beside the executable | DX11, "unless the game is started with -force-d3d12, -force-vulkan or -force-glcore" | `core/pe.py:719-733` |
| Unreal Engine 1 / 2 | executable's folder is named `System` (any case) with `Core.dll` and `Engine.dll` beside it; the renderer comes from the game's ini | per device; see below | `core/pe.py:836-945` |
| Unreal Engine 3, 32-bit | `Binaries\Win32\<game>.exe` with a `CookedPC*` folder inside a sibling project folder | DX9, DX11 only if the game's config says so | `core/pe.py:983-1062` |
| Unreal Engine 4 / 5 | the Unreal layout above | DXGI, "Direct3D 12 or 11" | `core/pe.py:970-979` |

- Unity names every backend it can drive (d3d11, d3d12, opengl32, vulkan-1)
  in `UnityPlayer.dll` and in older players in the executable; taking
  `opengl32.dll` from those strings put Cities: Skylines II, House Party and
  every Unity game on the OpenGL route, where ReShade never appears (issues
  #46 to #48).
- Unreal shipping executables import no graphics DLL and carry the string
  `opengl32.dll` from an OpenGL RHI their Windows builds stopped using in
  4.27 (found on WARDOGS). Without the rule they were called OpenGL.
- **Unreal 1 and 2** keep the executable in `System\` and load the renderer
  the ini names: `[Engine.Engine]` key `GameRenderDevice` (Unreal 1) or
  `RenderDevice`, value `Module.Class`. The ini is `<exe stem>.ini`, then
  `Default.ini`, in the same folder, max 4 MiB, treated as untrusted text:
  UTF-16 with or without BOM, UTF-8 with BOM, or Latin-1 otherwise; spaces
  around `=`; a quoted value; a trailing `;` comment; section and key
  names ignoring case; first occurrence of a key wins.
  - The device module's own import table says which Direct3D it is, in the
    order `d3d12, d3d11, d3d10, dxgi, vulkan-1, opengl32, d3d9, d3d8`. A module
    importing `ddraw.dll` is Direct3D 7, which is not a label: the result is
    `Unknown` with a reason that says nothing here reaches Direct3D 7 and to
    switch the game to its OpenGL or Direct3D 9 renderer.
  - If there is no ini, or it names a module that is not there, the modules
    present are tried: `D3D9Drv`, then `D3DDrv`.
  - Without the module file, the device name decides: `d3d9drv` DX9,
    `d3d10drv` DX10, `d3d11drv` DX11, `opengldrv` and `xopengldrv` OpenGL,
    `vulkandrv` Vulkan. D3DDrv.dll is Direct3D 8 in Unreal 2 and Direct3D 7
    in Unreal 1, so the name alone settles nothing (POSTAL 2, issue #403).
  - Games it was written for: Deus Ex, UT2004, POSTAL 2, Splinter Cell: Chaos
    Theory, SWAT 4, Killing Floor.
- **Unreal 3, 32-bit** names `d3d11.dll` for the renderer it can switch to
  and imports nothing else, so the string alone made Life is Strange DX11
  and the feed failed on ReShade's D3D9 backend (#479). Result DX9, unless
  `AllowD3D11=True` is in an `*Engine.ini`, read in this order, first
  decisive line wins:
  1. the player's copy `Documents\My Games\<studio or game>\<Game>\Config\*Engine.ini`
     (and the same under `OneDrive\Documents`), counted only when its studio
     or game folder is named like the game's folder (letters and digits only,
     either contains the other; "UDKGame" is every UDK game's name);
  2. the shipped `<project>\Config\*Engine.ini`.
  An explicit `AllowD3D11=False` ends the search with DX9. At most 60 root
  entries and 60 children each are listed, 10 ini files per folder, 400,000
  characters read.
  - Changed from the reference: it took `Documents` as `%USERPROFILE%\Documents`
    and `OneDrive\Documents` (`core/pe.py:1021`). The Documents folder is
    redirectable; use the shell's known-folder answer, and its OneDrive
    variant only as a second place.

### The run-time renderer search (no static graphics import)

When no graphics DLL is imported, the engine loads its renderer by name:
Call of Juarez: Gunslinger names d3d9.dll in the executable and imports
nothing (issue #31). The name is still in the file, or in an engine DLL
beside it. In order (`core/pe.py:1065-1125`):

1. An **engine rule** above decides at once.
2. The **executable's own strings**: any of the names below, as ANSI or
   UTF-16LE, matched case-insensitively across the whole file (read in 4 MiB
   blocks with a 64-byte overlap), skipped for a file over 512 MiB. A
   Direct3D name found there is taken as it stands, because the DLLs beside
   a game mention renderers of their own (Bink, CEF and SDL name
   `d3d11.dll`) and must not overrule the game.
3. Only when the executable names nothing, or only `opengl32.dll` or
   `vulkan-1.dll` (engines that can drive several backends name them
   without using them), the **sibling DLLs** are consulted:
   - the import tables of up to 60 `.dll` files in the folder (sorted by
     name), then
   - the strings of the 6 largest that are between 512 KiB and 64 MiB.
4. **Ranking** is the static table's order: `d3d12`, `d3d11`, `d3d10`, `dxgi`,
   `vulkan-1`, `opengl32`, `d3d9`, `d3d8`. A Direct3D name anywhere
   outranks OpenGL or Vulkan, because an engine that names all of them draws
   with Direct3D on Windows.

Labels for those names: `d3d12` DX12, `d3d11` DX11, `d3d10` DX10, `dxgi` DXGI,
`vulkan-1` Vulkan, `opengl32` OpenGL, `d3d9` DX9, `d3d8` DX8. The last one
was missing at first, so Unreal Engine 2's only record (`System\D3DDrv.dll`
imports d3d8.dll) was thrown away (#403). A run-time `d3d9.dll` with the
game's own DLSS beside a 64-bit executable is promoted to DX12; beside a
32-bit one it stays DX9, because DLSS has no 32-bit build (#190).

**Not consulted as sibling evidence** (their imports say nothing about the
game):

- every graphics DLL name above (a proxy DLL: DXVK, ReShade, a wrapper), plus
  `reshade32.dll`, `reshade64.dll`, `reshade.dll`, `ddraw.dll`, `dinput8.dll`,
  `winmm.dll`, `version.dll`, `dbghelp.dll`, `winhttp.dll`, `wininet.dll`,
  `d3dcompiler_47.dll`;
- the DLSS, OptiScaler, Streamline, XeSS and FidelityFX files our routes or
  the games ship: `nvngx_dlssnr.dll`, `nvngx_dlss.dll`, `nvngx_dlssd.dll`,
  `nvngx_dlssg.dll`, `_nvngx.dll`, `nvngx-wrapper.dll`, `remix_nvngx.dll`,
  `optiscaler.dll`, `dlss5-bridge.dll`, `dlss-enabler.dll`,
  `dlss-enabler-headless.dll`, `rtx40mfgcore.dll`, `sl.interposer.dll`,
  `sl.common.dll`, `sl.reflex.dll`, `sl.dlss.dll`, `sl.dlss_g.dll`,
  `sl.dlss_d.dll`, `sl.pcl.dll`, `libxess.dll`, `libxess_dx11.dll`,
  `libxess_fg.dll`, `libxell.dll`, `amd_fidelityfx_dx12.dll`,
  `amd_fidelityfx_vk.dll`, `amd_fidelityfx_upscaler_dx12.dll`,
  `amd_fidelityfx_framegeneration_dx12.dll`, `amd_fidelityfx_loader_dx12.dll`,
  `ffx_fsr2_api_x64.dll`, `ffx_fsr2_api_dx12_x64.dll`, `ffx_fsr2_api_vk_x64.dll`,
  `ffx_fsr3upscaler_x64.dll`, `ffx_backend_dx12_x64.dll`,
  `ffx_backend_vk_x64.dll`;
- **middleware that names every API it instruments**: `gfsdk_aftermath_lib.x64.dll`,
  `gfsdk_aftermath_lib.dll`, `amd_ags_x64.dll`, `amd_ags_x86.dll`,
  `eossdk-win64-shipping.dll`, `eossdk-win32-shipping.dll`. NVIDIA's
  Aftermath crash library names `d3d12.dll` and `dxgi.dll` in every build,
  Vulkan games and the Remix runtime included, and made Enshrouded (Vulkan)
  DX12, so OptiScaler went in as `dxgi.dll` and never loaded (#130);
- every file the install record lists as written by us.

## What must never happen

- **A guess dressed as a reading.** The reason always names the evidence
  (file, table, string, folder). A reading from strings or a marker file is
  worded as a guess ("named in the exe", "loads X at run time").
- **A file the game's process could not load counting as evidence**: a
  64-bit DLL beside a 32-bit executable.
- **Our own files counting as the game's**, or another tool's NGX files
  counting as the game's DLSS.
- **Executing, loading or mapping the game's code.** Files are read as bytes.
- **A scan that does not finish.** Every walk, listing and string search has
  a bound (counts and sizes above) and honours cancellation; one slow disk
  must not change the answer, only the time.
  - The reference had a file-size bound but no time bound on the string
    scans (a 512 MiB executable and six 64 MiB DLLs per game, across the
    library); the cost is paid again on each scan of such a game, so the
    library cache (see [library](library.md)) must hold the answer.
- **A path built from file content escaping the folder.** The
  `D3D12SDKPath` export, an ini value or a manifest name is only ever
  joined under the executable's folder or compared by name, never opened as
  given.
- **A malformed file raising out of a scan.** Every parser treats its file as
  untrusted: truncated tables, offsets beyond the end, loops in lists, huge
  counts. The result is "no evidence" or a named error.

## Bugs and questionable choices in the reference not to port

- `core/pe.py:248-249`, `:105-106`, `:795-796`, `:1473-1474`: broad
  `except Exception` returning "empty" or "no evidence", hiding corrupt files
  and real faults alike.
- `core/pe.py:450`: substring matching on import names.
- `core/pe.py:75-76`: file version available only on Windows.
- `core/pe.py:1428-1434`: reading up to 64 MiB into memory to find two
  exports; `core/pe.py:765-775` a global cache keyed on path, time and size
  to hide the cost.
- `core/pe.py:210-214`: an RVA is mapped using the larger of virtual and raw
  size and no bound on the raw offset, so a name in the padding of a section
  reads garbage. Bound the file offset by the raw size.
- `core/pe.py:253-261`: `API_PROXY` is not used anywhere. Which proxy a route
  uses belongs to the routes spec.
- `core/pe.py:316-344` vs `core/installer.py` (`other_ngx_hooks`): the same
  lists of our add-ons and foreign NGX files kept twice because of an import
  cycle (`core/pe.py:308-312`). One source of truth.
- `core/pe.py:1021`: `Documents` taken as a fixed path.
- Names in reasons that read as tool-internal ("gate 2.0.5") do not belong in
  text the person sees.
- `core/pe.py:484-485`, `:970-979`: a game that imports only `dxgi.dll`, and
  every Unreal 4/5 game with no import, are labelled DX12 although DX11 is
  common (UE4 defaults to D3D11). The text says "DX11/DX12" but the label,
  and so the recommended route, says DX12. Labelled `DXGI` here.
- `core/pe.py:697-716`: the string search matches the name anywhere in the
  bytes, including inside a longer name (`xd3d11.dll`, `libd3d11.dll`).

## To verify against primary sources

| Claim | Source that settles it |
| --- | --- |
| COFF header, optional header magic 0x10B/0x20B, `Machine` values 0x8664, 0x14C, 0xAA64, 0xA641, data directory indexes (1 import, 2 resource, 13 delay import), `NumberOfRvaAndSizes` | Microsoft PE format specification |
| Delay-load descriptor layout (32 bytes), attributes bit 0 ("RVAs not virtual addresses"), the name at offset 4 | Microsoft PE format spec, "Delay-Load Directory Table"; `delayimp.h` |
| `VS_FIXEDFILEINFO` signature 0xFEEF04BD, resource type 16, and that Windows' `GetFileVersionInfo` uses the same first block a hand parser reads | Win32 docs; compare on real NVIDIA DLLs |
| `D3D12SDKVersion` and `D3D12SDKPath` exports: type (a pointer to a UINT, a string), path relative to the executable, read from the process executable | DirectX Agility SDK documentation and developer blog |
| Windows' own `System32\D3D12Core.dll` exports `D3D12SDKVersion` | `dumpbin /exports` on Windows 10 and 11 |
| Windows ships no 64-bit `d3d8.dll` | Windows SDK / DirectX documentation |
| NGX/DLSS runs on D3D11, D3D12, Vulkan and not D3D9; DLSS frame generation and ray reconstruction are D3D12-only | NVIDIA DLSS programming guide; Streamline docs (DLSS-G and DLSS-RR also exist for Vulkan, which would weaken the "D3D12-only" inference of rules 2, 3, 11) |
| Unity picks Direct3D 11 on Windows unless `-force-d3d12`, `-force-vulkan`, `-force-glcore` | Unity manual, command-line arguments and graphics API selection |
| Unreal: 4.27 and later Windows builds do not use the OpenGL RHI; default RHI per version (UE4 D3D11, UE5 D3D12); how a shipped game states its RHI (`DefaultGraphicsRHI`, `-dx11` and `-dx12`) | Unreal Engine docs and release notes |
| Unreal 1/2 `[Engine.Engine] RenderDevice` / `GameRenderDevice`; D3DDrv is Direct3D 7 in UE1 and 8 in UE2 | UT99/UT2004 ini documentation, Unreal Wiki |
| Unreal 3: layout `Binaries\Win32`, `CookedPC*`, `AllowD3D11` in `*Engine.ini`, the player's copy under `Documents\My Games\...\Config` | UDK documentation; sample on Arkham Origins, Life is Strange |
| That the Documents known folder, not `%USERPROFILE%\Documents`, is where UE3 writes | `SHGetKnownFolderPath(FOLDERID_Documents)` documentation |
| `nvngx_dlssg.dll` / `nvngx_dlssd.dll` under an Unreal plugin path, per plugin | NVIDIA Streamline / DLSS Unreal plugin packaging |
| Engines named in the "named in every API" list really do name every API | Inspect the libraries; NVIDIA Nsight Aftermath documentation |
| On Windows: the hand-parsed file version equals `GetFileVersionInfo`/`VerQueryValue` on real `nvngx_dlss*.dll` files, one with two language blocks | a Windows machine |
| On Windows: shipped games export `D3D12SDKPath` as a `const char*` (pointer, `ImageBase` subtracted) or as an inline array | `dumpbin /exports` and a hex read on a few Agility SDK games |
| On Windows: a `WindowsApps` or `XboxGames` executable fails with `UnauthorizedAccessException` on open or first read; opening a running game's executable with `FileShare.ReadWrite \| Delete` works | a Game Pass game, installed and running |
| On Windows: `PEHeaders` accepts an executable over 2 GB given a size capped at `int.MaxValue` | a game executable over 2 GB, or a padded copy |

## Fixtures

The old tests copied real Windows binaries (`where.exe`, `cmd.exe`,
`Magnification.dll`); C# tests must run on macOS and Linux, so build the
files:

- **A synthetic PE builder** for tests, in the test project: PE32 and PE32+,
  a chosen `Machine`, a list of static imports, a list of delay imports (RVA
  style and the old virtual-address style), an export table with
  `D3D12SDKVersion` and `D3D12SDKPath`, a version resource, trailing padding
  (a 500 MB file as a sparse file for the size bounds), and arbitrary
  strings appended in ANSI or UTF-16LE. Start from `tests/fake_pe.py` at the
  tag `python-reference` (`git show python-reference:tests/fake_pe.py`,
  which already writes a valid version resource, both bitnesses) and the
  stubs in `test_all.py` (the 32-byte `_pe_stub`, `test_all.py:17100-17112`).
- **Folders** built from a table: exe name, bitness, imports, files beside it
  (with the bitness of each), an install record, backups, Unreal layouts,
  `System` layouts with an ini in each encoding.
- **Expected results**, one test each, from the old checks: sections 46
  (`test_all.py:7400-7520`, run-time `d3d9.dll`, Unity, siblings, Streamline
  files), `test_all.py:9467-9528` (the d3d9 sequence by faked tables),
  `test_all.py:17110-17210` (Subnautica, Mass Effect Andromeda, #190 and
  #458), `test_all.py:23519-23640` and `:25960-26010` (Unreal 1/2/3),
  `test_all.py:24587-24595` (DX8 on 64-bit), `test_all.py:13500-13520`
  (Enshrouded and Aftermath), `test_all.py:29800-29860` (Agility unmet).
- **The real-game corpus**: 100 or so of the 184 reports under
  `_tools/reports/` carry `arch/api: <bits> / <label> (<reason>)`. They are
  gold labels for the reason texts and the label for a named game; table of
  examples to reproduce (each needs a synthetic folder for its shape):
  - RDR2: 64-bit, imports d3d9.dll, ships nvngx_dlss.dll: DX12 (report 066);
  - GTA V (GTA5.exe): 64-bit, d3d9 import: DX9 in 1.7.2, the case #77 (report 077);
  - Mass Effect Andromeda: 64-bit, imports d3d11.dll, Agility SDK: DX12;
  - Call of Juarez Gunslinger: 32-bit, Unknown in 1.x, DX9 after #31 (report 031);
  - Life is Strange: 32-bit, DX11 in 1.x (wrong, #479: DX9);
  - Deadlock: 64-bit, no imports, `Qt5Gui.dll` beside it names d3d11: DX11;
  - Unity titles (Nivalis Nights, Dread Delusion): DX11, reason names Unity;
  - The Outer Worlds: 64-bit, Unknown;
  - frequencies across the corpus: DX12 about 73, DX11 about 30, DX9 about
    30, Vulkan about 13, OpenGL about 7, Unknown about 10.
- **No real NVIDIA or game binaries** in the repository. Tests build
  synthetic PE files (headers, imports, delay imports, exports, resources)
  in code. For the version parser, where a real stamp matters (three or four
  `nvngx_dlss.dll` versions, a DLL carrying two language blocks), commit only
  the extracted version-resource bytes with the expected values beside them,
  taken once from a Windows machine.
- **Agility**: an executable exporting version 4 and path `.\` against a
  stubbed `System32\D3D12Core.dll` exporting 3 and 600 (the system root is
  injectable, not `%SystemRoot%`).

## Open questions

1. **ARM64 and ARM64EC games.** Say "unsupported" with the architecture
   named, or accept them as 64-bit? The tool targets NVIDIA GPUs on x64
   Windows, so unsupported seems right; Unreal's `WinARM64` folder is in the
   reference's layout list (`core/pe.py:753`).
2. **Managed AnyCPU executables** report `Machine` 0x14C and run as 64-bit.
   A launcher written in .NET would read as 32-bit. Out of scope unless a
   report shows it.
3. **Unreal 4 default.** Should an Unreal 4 game with no static import be
   narrowed from DXGI to DX11 (the engine default), and what in a shipped
   game settles it? Needs a check against several shipped games before changing
   it.
4. **Reading the Windows version of `D3D12Core.dll`.** Needs an
   injectable system folder; confirm how the tool treats a non-Windows host
   (always "met").
