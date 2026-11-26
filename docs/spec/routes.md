# Routes

A route is one way of putting DLSS 5 neural rendering into a game. There are
nine. This spec says which routes a game is offered and which one is
recommended and why, what the person can override, what each route puts in
the game folder and configures, what the person sees as settings per route,
and the warnings and refusals that come before the first write. It is the
behaviour of the reference's route logic (`core/dlss.py`), the route
branches of `core/installer.py`, `core/optiscaler.py`, `core/dxvk.py`,
`core/vulkan.py`, `core/reshade_ini.py`, `core/feedcfg.py`, `core/mfg.py`,
`core/ownfg.py`, `core/refw.py`, `core/reengine.py`, `core/anticheat.py` and
the settings rules in `core/ui/ctl_game.py`, plus the README sections "Which
route a game gets", "Frame generation" and "Settings worth knowing". Where the
components come from is [components](components.md); the graphics card and
the per-card runtime choice is [gpu](gpu.md); how files are written, recorded
and taken out again is the install mechanics spec, and how downloads behave is
[net](net.md). This spec only says what goes where and why.

Vocabulary: **ReShade routes** are native, upstream, bridge, feeder,
standalone, renodx and uplift: they install ReShade and an add-on.
**OptiScaler** and **remix** install no ReShade. "The game's API" is the
graphics API detection read, or the one the person set.

## What the user gets

### The nine routes

| Id | Name | What it is | Needs |
| --- | --- | --- | --- |
| `native` | native | Krish's `renodx-dlss5` add-on hooks the DLSS calls the game already makes and adds the neural pass after them; the game's own DLSS quality mode applies | 64-bit D3D12 game that ships DLSS |
| `upstream` | neural-upstream | matiasLombo's add-on runs the network at render resolution before the game's DLSS upscales: the same result on a smaller image, a fraction of the cost. It does the neural pass itself: no `renodx-dlss5` beside it | 64-bit D3D12 game that ships DLSS |
| `optiscaler` | optiscaler | No ReShade. OptiScaler takes over the game's upscaler (DLSS, or FSR 2/3 and XeSS redirected into DLSS) and runs the model over its output, with a model-resolution dial that is the biggest fps lever | D3D11 or D3D12 game that ships DLSS, FSR 2/3 or XeSS |
| `bridge` | bridge | NIGos' `dlss5-bridge` mirrors the game's own DLSS contract onto a private D3D12 session (or builds one from the driver's optical flow when the game has no DLSS) | 64-bit D3D11 or Vulkan game; D3D12 as an alternative |
| `feeder` | feeder | jlrouzies-fr's DLSS5-Feeder builds a DLAA contract from ReShade's depth buffer and shader motion vectors: always DLAA, never upscaling, the game's own DLSS ignored | any game, any bitness, D3D9 to D3D12, Vulkan, OpenGL |
| `standalone` | standalone-dlssnr | kibblerz's add-on does the whole pipeline itself (own feed, the network, DLAA at native or DLSS super resolution below it, frame generation) and shows the result through a window of its own. Experimental | 64-bit D3D11 or D3D12 |
| `renodx` | renodx-dlss | ShortFuse's `renodx-dlss` add-on hooks D3D9, D3D11 and D3D12 presentation in-process. Reported not working in many games; the only route for 64-bit D3D9 | 64-bit D3D9, D3D11, D3D12 |
| `uplift` | gitc-uplift | ghostinthecamera's add-on runs the network itself on the finished frame, or inside the game's own DLSS where there is one; a 64-bit helper serves 32-bit games. Switched on in its own tab in ReShade. Single-player only | D3D9 to D3D12, Vulkan, OpenGL, either bitness |
| `remix` | remix | The game has an RTX Remix mod: the neural pass runs inside the Remix runtime (`.trex`), after DLSS. Nothing is injected into the game | a game with a `.trex` runtime |

The route ids are stored in the install record and in settings and are part
of the file format.

### Which routes a game is offered, and which is recommended

The detection step reads three facts from the game folder: whether the game
ships DLSS of its own, whether it ships an FSR 2/3 or XeSS runtime, and which
files make that so ([detection of the game itself](library.md) is another area).
This spec takes them as inputs:

- **Own DLSS** (`native_dlss`): any Streamline file (`sl.interposer.dll`,
  `sl.dlss.dll`, `sl.common.dll`, `sl.dlss_g.dll`, `sl.reflex.dll`), an own
  runtime renamed to switch it off (`nvngx_dlss.dlsss`, `nvngx_dlss.dll.bak`,
  `_nvngx.dll`), or an `nvngx_dlss.dll` that is **not** this tool's. A swapped
  file counts as the game's own while its backup `nvngx_dlss.dll.dlss5-autopilot-backup`
  sits beside it (`dlss.py:764`). Looked for beside the executable and the
  install folder, then in a bounded walk of the game folder that skips the
  asset-only folders. An `nvngx_dlssg.dll` or `nvngx_dlssd.dll` is evidence of
  its own but does not by itself mean the game has super-resolution DLSS.
- **Upscaler** (`fsr` or `xess`): only asked when there is no own DLSS; from
  the FSR/XeSS runtime files, again beside the executable and in the walk.
- **API and bitness** as read or as set.

Rules, first match wins; each row lists the options in dropdown order and
which is recommended:

| # | Game | Options | Recommended |
| --- | --- | --- | --- |
| 1 | Direct3D 10 (any bitness) | feeder | feeder: needs feeder 0.13.1 or newer, which relays D3D10 through a private D3D11 device |
| 2 | 32-bit (any API but D3D10) | feeder | feeder, NGX in the 64-bit helper process; DirectX 8 and 9 are first translated to Vulkan by DXVK |
| 3 | 64-bit D3D9 | renodx | renodx-dlss (no motion vectors: a softer result) |
| 4 | Vulkan with own DLSS | bridge, feeder | bridge |
| 5 | Vulkan without | feeder, bridge | feeder |
| 6 | OpenGL | feeder | feeder |
| 7 | D3D11, own DLSS | bridge, optiscaler, feeder, standalone, renodx | bridge |
| 8 | D3D12, own DLSS | native, upstream, optiscaler, bridge, feeder, standalone, renodx | native |
| 8a | DXGI ([pe](pe.md)'s label for "11 or 12 not settled") or unknown API, own DLSS | optiscaler, bridge, feeder, standalone, renodx: what fits both D3D11 and D3D12 | none: the person is asked for the API, shown pe's DXGI evidence and its confidence; the install refuses an unknown API anyway. Changed from the reference: it assumed D3D12 and recommended native |
| 9 | D3D11, D3D12 or DXGI, no DLSS, FSR 2/3 or XeSS shipped | feeder, optiscaler, bridge, standalone, renodx | feeder |
| 10 | D3D11, D3D12 or DXGI, nothing | feeder, bridge, standalone, renodx | feeder |

Then, in this order:

1. **uplift** is appended to the options of every game it fits: D3D9, D3D10,
   D3D11, D3D12, Vulkan, OpenGL or unknown API, and a bitness that is known
   to be 32 or 64. Not D3D8. It is **never recommended** (the routes stay
   conservative: it is single-player only and the least proven), not by the rules,
   not by the shared results, not as an "autopilot" next try (it is
   "offered, never chosen for the person").
2. **remix**. A game with a `.trex` runtime found in the install or game
   folder gets remix first in the list and recommended, supported, whatever
   else was decided. The other routes stay listed for completeness; none can
   be recommended, and installing any of them is refused (below).
3. **Own DLSS on D3D12 and an RTX 50 card** (sm 120 or more): recommended
   becomes optiscaler, with the reason "the model-resolution dial: 75% costs
   about half of full size; the native route is the simpler, more proven
   alternative". Below RTX 50, or on a card not known, native stays the
   recommendation and optiscaler is offered with its note: its author tested
   RTX 50 only. Changed from the reference: it recommended optiscaler from
   RTX 20 up; the routes stay conservative where nobody has tested.
4. **FSR or XeSS and no DLSS, D3D12, RTX 50 only** (sm 120 or more):
   recommended becomes optiscaler. Other cards keep the feeder, with
   optiscaler as an option, because it is a step further from what its author
   tested.
5. **Shared results**. When the community list has at least a set number of
   reports for this exact game, those results rank the routes
   (see `community` in the reference: `MIN_REPORTS`) and nothing here runs;
   otherwise, for the game's class (API, own DLSS, upscaler), if the list
   has at least `MIN_CLASS` results on two routes and their ranges do not
   overlap, the better one becomes the recommendation, provided it is on offer, is
   not uplift, and passes `fit` for this card. The reason then says whose
   results moved it and which route the tool would have picked. A remix game
   is never moved. Any failure in reading the list leaves the recommendation
   as it was.
6. **Driver warning**. When the driver is 616.64 or newer and the
   recommendation is native, bridge, feeder or renodx (the routes that load
   `renodx-dlss5`; renodx loads ShortFuse's own build but is in the list), the
   recommendation stays and a warning names the driver, says what the fault
   is, says the evidence is a handful of reports, and names standalone (when
   offered) and rolling the driver back to 616.56 as the other answers.
   Changed from the reference: it moved the recommendation to standalone, an
   experimental route that presents through a window of its own, on a few
   reports.

Every route in the list carries a short note ("fit") the dropdown shows,
whether it is usable on this card and why not:

| Route | Usable | Note |
| --- | --- | --- |
| optiscaler | only with own DLSS or a shipped FSR/XeSS | "model resolution dial: the fps lever". Without own DLSS: the upscaler has to be on in the game's own settings, or there is nothing to hook. D3D11: "the upscaler becomes FSR on D3D12". Vulkan: the model has had nothing to attach to in reports; set the game to D3D12 or use the feeder. Below RTX 50: author tested RTX 50 only |
| remix | always | "runs inside the Remix runtime, after DLSS - no ReShade, no add-on" |
| native | always | "most proven for D3D12 games with DLSS" |
| upstream | only with own DLSS | "runs the network before the game's DLSS, at render resolution; tested on two games" |
| standalone | always | "own feed: DLAA at native resolution, DLSS SR below it, frame generation; experimental" |
| uplift | always | "one add-on, every API, 32- and 64-bit; switched on in its own tab" |
| renodx | always | "unproven - reported not working in many games" |
| bridge | always | "mirrors the game's DLSS onto D3D12", on D3D11 and Vulkan "every release tested" |
| feeder | always | "always DLAA, shader motion vectors"; with own DLSS "ignores the game's DLSS" |

The route list never changes after the person chooses another: choosing a
route resets the settings that route does not read (see "Settings").

### What the person can override

| Override | Where | Effect |
| --- | --- | --- |
| Route | the route dropdown; the command-line route flag | any of the offered routes; the recommendation is a default only. A route that is not offered for the game cannot be chosen |
| Graphics API and architecture | the game's settings | replace the detected values; asked when the executable is protected and unreadable. D3D8 with 64-bit is refused |
| Executable | when the folder has several | the one that draws |
| ReShade loads as | ReShade routes, D3D10 to D3D12 and OpenGL; not D3D8 or D3D9 (a D3D9 game not through DXVK always loads it as `d3d9.dll`), not Vulkan and not through DXVK (`ctl_game.py:897-901`) | one of `dxgi.dll` `d3d11.dll` `d3d12.dll` `d3d10.dll` `opengl32.dll`; blank means by API |
| OptiScaler loads as | optiscaler | one of `dxgi.dll` `winmm.dll` `version.dll` `dbghelp.dll` `d3d12.dll` `wininet.dll` `winhttp.dll`; blank means the first free name (below) |
| OptiScaler build | optiscaler | the six builds of [components](components.md); blank is the default build |
| Motion-vector provider | feeder | 3 LumeniteFX Kernel (default), 4 LumeniteFX QuantMotion, 2 VORT, 5 built in (feeder 2.0.0 or newer), 0 generic `texMotionVectors`, 1 iMMERSE Launchpad (0 and 1: the person installs the shader) |
| Feeder build | feeder | newest release (default), newest pre-release, or an exact tag |
| 64-bit helper process | feeder, 64-bit games | NGX in `host64\` instead of inside the game |
| DXVK | native, bridge, feeder on D3D11 games | run the game on Vulkan through DXVK |
| `renodx-dlss5` build | native, bridge, feeder, renodx | a build from the list, or "use my file" (a 64-bit add-on of the person's own) |
| `nvngx_dlssnr.dll` build | every route | auto (newest that runs on this card) or a pinned build |
| `nvngx_dlss.dll` build | where shown | auto (NVIDIA's newest) or a pinned build; and "keep the game's own", on by default |
| Ray-reconstruction build | ReShade routes, only for a game that ships `nvngx_dlssd.dll` | a swap, never an addition |
| Model resolution, preset, style, passes | optiscaler (and the feeder's work resolution on 64-bit D3D11 not through DXVK, not helper mode) | see below |
| Frame generation | see "Frame generation" | |
| Overlay key | every route but remix | the key that opens the overlay |
| VR headset | ReShade routes, 64-bit | also register ReShade's OpenXR layer; out of this spec's scope |
| Swap the Remix runtime | remix | replace the mod's runtime with a community one |
| Windows graphics preference | only where Windows has two GPUs | see [gpu](gpu.md) |
| Ignore GPU mismatch | a one-off | the install proceeds on a build that does not run on the card |

### Per-route settings the person sees

One rule set decides which settings the page shows for the chosen route; the
page, the install options and the reset on a route change all read it. A
setting that is hidden is not read by the install and is reset to its default
when the route changes. The reference's table (`ctl_game.py:862`), as rules:

| Setting | Shown when |
| --- | --- |
| route | always |
| bits (architecture) | the executable could not be read |
| executable | the folder holds several candidates |
| graphics API | always |
| OptiScaler loads as, OptiScaler build | optiscaler |
| OptiScaler preset, style | optiscaler |
| OptiScaler passes | optiscaler with a pre-super-resolution build (`wilsjo2`, `wilsjo2-mfg`, `janblade`, `jlrouzies`) |
| Model resolution | optiscaler; the feeder on 64-bit D3D11 without DXVK or the helper |
| Frame generation | optiscaler on D3D12 |
| Own frame generation files, forget | optiscaler on D3D12 (forget only when a set is chosen) |
| motion-vector provider, preset, HDR, feeder build | feeder |
| 64-bit helper process | feeder, 64-bit game |
| ReShade loads as | a ReShade route, API not Vulkan, no DXVK in use, API not D3D9/D3D8 |
| `renodx-dlss5` build | native, bridge, feeder, renodx |
| `nvngx_dlssnr` build | every route |
| `nvngx_dlss` build | not upstream, remix, uplift; and not when "keep the game's own" is on and a game file sits beside the executable |
| keep the game's own `nvngx_dlss` | not upstream, remix, uplift; not 32-bit |
| ray reconstruction | the game ships `nvngx_dlssd.dll` and the route is a ReShade route |
| DXVK | native, bridge, feeder; and API D3D11 or an executable known to need it |
| RTX 40 multi-frame generation | a ReShade route, and the card/game test below passes |
| overlay key | every route but remix |
| VR headset | a ReShade route, 64-bit game |
| swap the Remix runtime | remix |
| Windows graphics preference | two GPUs |

### What each route installs

All paths are relative to the **install folder** (the executable's folder; for
an Unreal or CryEngine launcher stub, the real executable's subfolder, said in
a note). A file a previous install wrote is overwritten; a file the game or the
person owns that is replaced is backed up first (mechanics: install spec).

**Every ReShade route** (before its own part):

- ReShade 64 or 32 as the chosen proxy name (`ReShade64.dll` or
  `ReShade32.dll` extracted from the setup), or, for a Vulkan game, the
  Vulkan layer instead and no proxy. Proxy name: the person's choice if on the
  list; else `d3d9.dll` for a D3D9 game not translated by DXVK; else
  `opengl32.dll` for OpenGL; else `dxgi.dll`.
- Any other ReShade under another name is taken out first: two copies abort
  each other. One of ours is held until commit and then deleted, by
  [install](install.md)'s hold-until-commit rule; one that is not on the
  record is backed up. When the name asked for is taken by something that is
  not ReShade (DXVK, Special K, another injector) the install is refused,
  except a hand-installed OptiScaler, which is backed up and moved aside with
  its ini and forwarder.
- Add-ons of every other route are taken out (below). A game's own
  `d3dcompiler_47.dll` older than Windows' is moved aside by
  [install](install.md)'s one mechanism (below).
- `ReShade.ini`: add-on loading (`[ADDON] AddonPath=.\`); the add-on's own
  switch (`[RenoDX.DLSS5] NeuralUplift=1`) set only when the key is absent, so a
  deliberate off survives; the search paths for feeder, standalone and uplift;
  the overlay key.
- `nvngx_dlssnr.dll` (the per-card build, [gpu](gpu.md)) beside the executable
  (in `host64\` for the feeder's split layout, in `GITC-Uplift\` for uplift).
- `nvngx_dlss.dll` (NVIDIA's newest or the chosen build) beside the executable,
  swapped over the game's own (kept as a backup), except: upstream and uplift
  (not placed), the game ships one and "keep the game's own" is on (left
  alone), or the game keeps its own under a nested folder (Unreal's
  `Engine/Plugins/...`), where that copy is swapped too.
- A game's `nvngx_dlssd.dll` swapped when the person chose a build and the
  game has one; skipped with a line when it does not, or when NVIDIA's
  download fails (a warning, not a failure).

**Route-specific files:**

| Route | Files placed | Configuration written |
| --- | --- | --- |
| native | `renodx-dlss5.addon64` | `ReShade.ini` as above; no shaders; the overlay says "no .fx files found": normal |
| upstream | `nvngx.dll.addon64` (file name load-bearing: the NGX hook only creates the feature for a caller whose module path contains `nvngx.dll`) | `ReShade.ini`; configured from its "NR Pre-Upscale" tab. Notes: with DLSS frame generation set its cadence to Quality; if the picture only gets darker the game exposes no exposure buffer: use native |
| bridge | `dlss5-bridge.addon64` (a legacy `dlss5-dx11-bridge.addon64` is removed) and `renodx-dlss5.addon64` | `ReShade.ini`; `dlss5-bridge.cfg` (below) |
| feeder | `dlss5-feed.addon64` (64-bit) or `.addon32` (32-bit) or `dlss5-feed-helper.addon64` (helper mode); `dlss5-feed-host64.exe` in `host64\` (32-bit or helper); `renodx-dlss5.addon64` (in `host64\` when split); `reshade-shaders\Shaders\DLSS5_Feed.fx`, `ReShade.fxh`, `ReShadeUI.fxh`, `DrawText.fxh`; the chosen provider's shader; in the split layout `host64\dxgi.dll` (ReShade64) and `host64\ReShade.ini` (add-ons only) | `ReShade.ini` with `DLSS5_MV_PROVIDER=<n>`; `ReShadePreset.ini` with the provider's technique above `DLSS5_Feed`; `dlss5-feed.cfg` (below); the person's ReShade keys carried over from another install |
| standalone | `standalone-dlssnr.addon64`, `nvngx.dll` (the add-on's own caller bridge, not a driver file), `reshade-shaders\Shaders\DLSS5_AIO_Feed.fx` (and `StandaloneBoundary.fx` when the release has it), the shader headers, VORT (`vort_Motion.fx`, its includes under `Shaders\Includes`, `vort_BlueNoise.png`), `nvngx_dlssg.dll` | `ReShade.ini` with search paths and no technique in any preset (the add-on schedules the two shaders itself). Notes: turn the game's own DLSS, frame generation and anti-aliasing off; game resolution = monitor's gives DLAA, lower gives DLSS super resolution; F10 compares; the log is `%LOCALAPPDATA%\RHI\Logs\standalone-dlssnr.log` |
| renodx | `renodx-dlss.addon64` (ShortFuse's) | `ReShade.ini` plus `[RENODX-DLSS] NeuralRenderingEnabled=1` when absent; for the game that closes itself at start (`mafia ii definitive edition.exe`) `DirectNeuralRenderingHookStage=0` and a note to switch it on from the overlay |
| uplift | `gitc-uplift.addon64`, `gitc-uplift.addon32`, `gitc-uplift-helper64.exe`, the two shaders `Uplift.fx` and `UpliftMask.fx`; the runtime under `GITC-Uplift\`; a `nvngx_dlssnr.dll` of somebody else's beside the executable is moved aside (RTX 50 would load it first) | `ReShade.ini` with search paths; starts switched off: open ReShade, the "GITC Uplift" tab, tick Enable |
| optiscaler | the whole OptiScaler package (`OptiScaler.dll` renamed to the proxy name, its other files at their paths; no `.pdb` `.exp` `.lib`, no setup scripts), `nvngx_dlssnr.dll` beside it; `nvngx_dlss.dll` only for a game with FSR/XeSS and no DLSS; the person's own frame generation files | `OptiScaler.ini` (below) |
| remix | `nvngx_dlssnr.dll` into the `.trex` folder; with "swap the runtime": `d3d9.dll` and `remix_nvngx.dll` into `.trex` (the mod's runtime backed up first) | one line in `rtx.conf`: the enable key for the runtime's flavour set to `True`; nothing else in the file touched |

Frame-generation `nvngx_dlssg.dll` also goes in on the standalone route
(fetched, unless the game has its own and "keep" is on; on a card below RTX 40
the add-on falls back to real frames, said in a note).

**Route-switch purging.** ReShade loads every `.addon64` in the folder, and
two add-ons that hook the same NGX calls fight (flicker, nothing, the game
exiting before its swap chain). So an install of any route first removes the
add-ons of the other routes, whatever the record says, with these sets:

| Route | Its add-ons |
| --- | --- |
| feeder | `dlss5-feed.addon64`, `dlss5-feed.addon32`, `dlss5-feed-helper.addon64` |
| bridge | `dlss5-bridge.addon64` |
| renodx | `renodx-dlss.addon64` |
| upstream | `nvngx.dll.addon64` |
| standalone | `standalone-dlssnr.addon64`, `nvngx.dll` |
| uplift | `gitc-uplift.addon64`, `.addon32`, `gitc-uplift-helper64.exe` |

and `renodx-dlss5.addon64` too, on renodx, upstream, standalone, uplift and
remix. A file of ours is held until commit and then deleted ([install](install.md),
"Switching routes"); one not on our record is backed up so uninstall restores
it. A hand-placed ReShade
add-on of somebody else's is not removed; it is warned about (below).

**The OptiScaler proxy name.** The person's choice; blank is the first of
`dxgi.dll` `winmm.dll` `version.dll` `dbghelp.dll` `d3d12.dll` `wininet.dll`
`winhttp.dll` that is free (absent or already OptiScaler), skipping the names
the person's own frame-generation files need. A game that ships its own
`dxgi.dll` therefore gets `winmm.dll` rather than losing its file. Another
copy of OptiScaler under another name is backed up and moved aside, as are
files left by releases before 0.9 (`nvapi64.dll`, `nvngx.dll`,
`OptiScaler.asi`, the remove scripts).

**`OptiScaler.ini`.** Only keys listed here are written; every other key,
comment and section stays as the person left it.

| Section and key | Value | When |
| --- | --- | --- |
| `[DlssNr] Enabled` | `true` | always |
| `[DlssNr] WorkingScale` | the model-resolution dial, 25 to 100 percent as 0.25 to 1.0; the tool's default is 75 | when set |
| `[DlssNr] Preset`, `Style` | 0 default, 1 to 3 presets; styles 0 standard, 1 natural, 2 cinematic | when set |
| `[DlssNr] Passes` | 0 auto, 2, 3 | pre-super-resolution builds only; "auto" writes 1 there |
| `[DlssNr] RunBeforeSR` | `true` | pre-super-resolution builds, unless the person set it |
| `[Log] LogToFile` | `true` | when not already (the diagnosis needs the log) |
| `[Log] LogLevel` | `2` | only when it is `auto` or absent |
| `[Hotfix] CheckForUpdate` | `false` | always (the update check compares with mainline OptiScaler, which has no neural rendering, and following it would remove what this route installs) |
| `[Menu] ShortcutKey` | hex virtual-key | when the person picked an overlay key |
| `[Upscalers] Dx11Upscaler` | `fsr22_12` | D3D11 games: the model refuses D3D11, so FSR 2.2 on D3D12 carries it and DLSS can not be the upscaler |
| `[Upscalers] Dx12Upscaler` | `dlss` | a game with FSR/XeSS and no DLSS, on D3D12 |
| `[Inputs]` | for FSR: `EnableFsr2Inputs`, `UseFsr2Inputs`, `EnableFsr3Inputs`, `UseFsr3Inputs`, `EnableFfxInputs`, `UseFfxInputs` all `true`; for XeSS: `EnableXeSSInputs=true`; D3D11 FSR: `UseFsr2Dx11Inputs=true` | a game with an upscaler and no DLSS |
| `[FrameGen] Enabled`, `FGInput`, `FGOutput` and `[OptiFG] HUDFix` | `true`, `upscaler`, `fsrfg`, `true` | "frame generation" on and the FSR 3.1 libraries (`OptiScaler/amd_fidelityfx_loader_dx12.dll`, `OptiScaler/amd_fidelityfx_framegeneration_dx12.dll`) are in the package |
| `[DLSSG] AdaMfgUnlock` | `true` | build `wilsjo2-mfg` |

Notes always given: OptiScaler is installed instead of ReShade; press Insert
(or the chosen key) in the game, turn Neural Rendering on, it is off by
default.

**`dlss5-feed.cfg`** (feeder). Defaults written, existing keys kept (the add-on
writes this file too): `enabled=1`, `mode=2` (full DLSS; 1 transport test;
0 off), `hdr=-1` (auto; 0 SDR; 1 HDR), `depth_inverted=-1` (follow ReShade),
`flags=-1`, `reset_every=0`, `warmup_rebuild=180`, `rebuild=0`, `log_frames=3`,
`create_delay=60`, `preset=0` (default; 5 and 6 legacy CNN E and F; 10 J,
11 K, 12 L, 13 M transformer), `work_resolution=100`, `mv_scale_x=1.0`,
`mv_scale_y=1.0`; `host_window=1` in the split layout. Floats are read with a
comma or a full stop, whatever locale the add-on wrote them in (issue #69: a
game that calls `setlocale` made the add-on write `1,000` and the install
failed on it).

**`dlss5-bridge.cfg`** (bridge). `vk_mirror=1`; for a game with own DLSS
`synth=0 synth_after=0`, for one without `synth=1 synth_after=3`; the person's
`ofa_grid` (0, 1, 2 default, 4) and `ofa_perf` (20 fast default, 10, 5). The
file is stamped `# dlss5-bridge keep` first: from bridge 1.4.0 it replaces any
file whose first line is neither its own version nor `keep`, with its
defaults (`synth_after=0` among them), which silently broke the substitute
contract for a game without DLSS (#127). Carried over from a file the bridge
wrote itself, only when they differ from its defaults: `ofa_grid`, `ofa_perf`,
`mv_sign_x`, `mv_sign_y`, `source`, `synth`, `unwrap`, `vk_sync`,
`vk_present`.

### Translation layers and Vulkan

**DXVK.** DirectX 8, 9 and 11 are translated to Vulkan and ReShade then
reaches the game as a Vulkan layer, with nothing hooked inside the game.

| Game | DXVK | Files placed |
| --- | --- | --- |
| D3D9 (32-bit, on the feeder) | forced | `d3d9.dll` |
| D3D8 (32-bit, on the feeder) | forced | `d3d8.dll`, `d3d9.dll` (d3d8 is a front end on d3d9) |
| D3D11 on native, bridge, feeder | by the box, and by default for an executable known to close itself on a ReShade DLL | `dxgi.dll`, `d3d11.dll` |
| any other route | never | |

The DXVK archive build matches the game's bitness (`x64\` or `x32\`). A Source
engine D3D9 game loads its renderer from `bin\shaderapidx9.dll` and never looks
beside the executable, so `d3d9.dll` goes into `bin\win64` or `bin` (the one
whose `shaderapidx9.dll` matches the game's bitness). Anything already under
those names that is not DXVK is backed up first; every backup is made before
the first DLL goes in. Executables known to need DXVK: `mgsvtpp.exe`,
`mgsvmgo.exe`, `mgsgzs.exe` (they quit the moment a ReShade proxy hooks
D3D11). A hand-set Vulkan API on such a game does not turn DXVK off: the
detected API decides. Through DXVK the game is a Vulkan game for the rest of
the install: no proxy, the layer instead. Use a borderless window, not
exclusive fullscreen (a note).

**The Vulkan layer.** A Vulkan game never loads `dxgi.dll`; ReShade reaches it
as an implicit layer. The tool:

- extracts the layer files (`ReShade64.dll`, `ReShade64.json`, and for a
  32-bit game `ReShade32.dll`, `ReShade32.json`) from the ReShade setup into its
  own folder and registers the manifest under
  `HKCU\Software\Khronos\Vulkan\ImplicitLayers` with value 0 (no
  administrator rights needed);
- gives the 32-bit manifest the layer name `VK_LAYER_reshade32`. Both of
  ReShade's own manifests are named `VK_LAYER_reshade`, HKCU is not redirected
  per architecture, and the loader de-duplicates implicit layers by name:
  it keeps the 64-bit one and rejects it for a 32-bit process, so nothing
  loads and no log is written (Call of Juarez: Gunslinger #31, Bayonetta #2);
- reuses an active registration of ReShade's own (HKCU or HKLM) that this
  game's architecture can load, rather than adding a second; rewrites ours
  when it still carries the old shared name;
- registers for this user only. A game started as administrator reads only
  the machine's list and does not get the layer; the preview says so.
  Changed from the reference: no all-users registration in v1
  ([install](install.md), which owns registrations and their dependents);
- says before it registers that the layer is **global**: it loads into every
  Vulkan application of this account, and uninstalling the last Vulkan
  install removes it.

### Frame generation

Three independent options, all off by default; the standalone route adds its
own. None is an upscaler.

| Option | Routes | Where it shows | What it does | Needs |
| --- | --- | --- | --- | --- |
| Frame generation, any RTX card | optiscaler | D3D12 | OptiScaler's bundled AMD FSR 3.1 libraries, the upscaler's output as the input: one generated frame per rendered one (2x), RTX 20 to 50. The game's own frame generation must be off; latency rises; the HUD varies by game | the FSR 3.1 libraries in the OptiScaler package; otherwise the switch writes nothing and says so |
| Multi-frame generation on RTX 40 | the ReShade routes | D3D12 and Vulkan | dashdogy's MFG-Unlocker raises the multiplier of a DLSS frame generation the game already has, to 3x or 4x, placed with Ultimate ASI Loader under a proxy name the executable imports | an RTX 40 (sm 89; an RTX 50 has it natively); the game's own `nvngx_dlssg.dll` or `sl.dlss_g.dll` (not one this tool wrote); the game's executable importing one of `dinput8.dll` `version.dll` `winmm.dll` not already used by another mod or REFramework |
| Frame generation files you downloaded | optiscaler | D3D12 | places the person's own `dlssg_for_sm86` (RTX 30: `version.dll` and `dlssg_sm86.ini`, or the same proxy under `winmm.dll` `dxgi.dll` `d3d12.dll` `dbghelp.dll`) or DLSS Enabler (headless: its `version.dll` as `dlss-enabler-headless.dll`) | files the tool cannot download. See below |
| frame generation of standalone | standalone | any | `nvngx_dlssg.dll` placed; used by the add-on | RTX 40 or 50; below that it falls back to real frames |

The multi-frame option in detail: both archives are checked to hold every
expected file before the first is placed; the release is never taken on trust
if its layout changed. `RTX40MFGCore.dll` and `RTX40MFG.asi` go into `plugins\`
(where a loader already present keeps them, when that folder holds an `.asi`
and a loader under one of the three names), `RTX40MFG-UI.addon64` beside the
executable; the loader goes in under the first of the three proxy names the
executable imports and nothing else uses (an RE Engine game's `dinput8.dll` is
REFramework's, so the loader takes another). The loader's own `<proxy>.ini` is
merged with `LoadPlugins=1`, `LoadFromScriptsOnly=1`,
`LoadExtraPlugins=RTX40MFG.asi`, `DontLoadFromDllMain=0`,
`ForceEntryPointHook=0` rather than replaced. If the executable imports none
of the names, or the release changed shape, or GitHub refuses, the install
carries on **without** it and says so as a warning. The pick of the multiplier
is in ReShade's "DLSS MFG" tab: follow the game, fixed, dynamic. Always said:
the game's own DLSS Frame Generation must be on; this is research software; a
frozen picture or a crash is possible at the higher multipliers and on Vulkan.
With the box off, an unlock an earlier install of ours placed is taken out, or
it would keep hooking Streamline.

Own frame generation files: the person picks the files they downloaded; the
tool copies them into its own folder (`own-fg\<recipe>`) so that "update all"
can place them again after the downloads folder was cleaned, places them
under the right name beside the game, backs up anything it replaces, records
them, and removes them on uninstall. Nothing runs them; they are identified by
name and by strings in the DLL, and a DLL must be a 64-bit Windows binary. The
install is **refused**, before anything is written, when OptiScaler would load
under the name the files need (choose another), when the stored copy has
disappeared, or when a file under that name is in the folder and is neither the
one the tool recorded, an OptiScaler, nor byte-identical to the stored copy.
Labelled experimental everywhere it shows.

OptiScaler with the `wilsjo2-mfg` build and "frame generation" together: the
FSR 3.1 output (`FGOutput=fsrfg`) leaves the RTX 40 unlock nothing to act on.
The install warns of exactly that, once, and only when FSR frame generation
really went in.

### Pre-install checks and warnings

Everything that is known before the first write is checked then, and each
refusal says what to do:

| Check | Result |
| --- | --- |
| no executable, a read error on the game, protected executable with the architecture or API not chosen, architecture not readable, D3D8 with 64-bit | refused |
| the folder is not writable (an Xbox/Game Pass folder is a different message: that app has the switch), the game is running | refused |
| a Remix game (`.trex` found) and the route is not remix | refused: ReShade crashes it before it draws a frame and, on D3D9, would overwrite the runtime |
| remix and no `.trex` found | refused |
| remix and the runtime has no neural pass and "swap" is off | refused, naming the swap |
| the card cannot run DLSS (the no-DLSS-here rule of [gpu](gpu.md)) | refused unless "ignore" |
| the OptiScaler build `wilsjo2-mfg` on a card other than RTX 40 | refused |
| own frame generation files cannot be placed | refused (above) |
| feeder on D3D10 with a feeder build below `v0.13.1-beta.1`; on the built-in provider below `v2.0.0`; helper mode below `v1.18.0-beta.1` | refused, naming the build to pick |
| uplift on Vulkan or OpenGL and the ReShade the PC can get is below 6.8.0 | refused |
| the proxy name is taken by something that is not ReShade | refused (above) |
| the executable looks like a launcher (`GTAVLauncher.exe` was all of issue #191) | warning, naming the executable that looks like the real one |
| another DLSS hook is in the folder (a list of known hooks and any `.addon64`, `.addon32`, `.addon` that is not ours) | warning: flicker, greyed-out frame generation, nothing |
| an anti-cheat is present (marker files, whole-word match: BattlEye, Easy Anti-Cheat, Riot Vanguard's two files, GameGuard, XIGNCODE3, Denuvo Anti-Cheat, PunkBuster, ...) | warning with the file named; not refused, because the game may be single-player: "do not use this online" |
| an RE Engine game (`re_chunk_000.pak` in the folder) on any route but remix | warning, and REFramework installed first |
| the driver (see [gpu](gpu.md)) | warning per route |
| the route's reliability level | a warning when it is not stable |

Reliability levels, per route and API:

| Level | Cases |
| --- | --- |
| stable | native; D3D11 and D3D12 on the feeder |
| beta | upstream; bridge (D3D11, Vulkan); optiscaler (with or without a redirected upscaler); remix without a swap; D3D10 on the feeder; Vulkan and OpenGL on the feeder; 64-bit D3D9 on renodx |
| experimental | standalone; uplift; renodx otherwise; remix with a swap; D3D9 and D3D8 through DXVK on the feeder; 32-bit on the feeder |

Per-executable facts that the route texts cannot know, shown beside the
conflicts: `quake3.exe` loads `opengl32.dll` from System32 (use ioquake3);
`openmw.exe` (OpenGL: `renodx-dlss5` pinned to 4.60, VORT motion vectors);
`mafia ii definitive edition.exe` closes itself at start when the renodx
add-on hooks neural rendering (the install sets the hook stage to 0);
`forspoken.exe` checks the signature of `dxgi.dll` and closes on ReShade's
(set "ReShade loads as" to `d3d12.dll`, or use optiscaler loaded as
`winmm.dll`). Any OpenGL game: motion vectors from VORT, never LumeniteFX
(it reads none), and `renodx-dlss5` 4.60 unless the person chose a build of
their own (their pin wins, as in [components](components.md)).

What must not sit in the folder beside each route, and the in-game
settings to say, as shown with the route:

| Route | Not in the folder | In the game |
| --- | --- | --- |
| native | OptiScaler, a frame-gen unlocker | NVIDIA Smooth Motion off |
| upstream | `renodx-dlss5`, OptiScaler, another NGX hook (ours removed, theirs named) | with DLSS frame generation, cadence Quality in the overlay; it does not upscale |
| optiscaler | any ReShade; a frame-gen unlocker or DLSS Enabler | with "frame generation" on, the game's own frame generation off |
| bridge | the feeder, renodx | Smooth Motion off |
| feeder | the bridge, renodx | Smooth Motion off |
| renodx | `renodx-dlss5`, the feeder, the bridge | nothing to tune if it does nothing: switch route |
| uplift | any other neural add-on | tick Enable in its tab; single-player only |
| standalone | (its own `nvngx.dll` is part of the route) | the game's own DLSS, frame generation and anti-aliasing off |

**Windows' Direct3D compiler.** A `d3dcompiler_47.dll` beside the executable
that is older than Windows' own rejects the `cs_5_1` target the add-on
compiles with: frames are delivered and the neural pass silently does
nothing. On ReShade routes, [install](install.md) moves it aside after a
version check and puts it back on uninstall; this spec only says where it
applies.

**Emulators.** For a game run by a known emulator on a render backend that
never creates a DXGI swap chain, v1 tells the person which backend to set;
the automatic edit is [install](install.md)'s and arrives in M8.

## What must never happen

- **A ReShade proxy in a Remix game's folder**, under any name.
- **Two add-ons that hook NGX in the folder at once**, whatever the record
  says.
- **A second ReShade under another name**, including one left by an earlier
  install under the name that was changed.
- **A written file, of the game's or the person's, with no backup** when
  it replaces one that is not ours; **and a file of ours ever backed up as if it
  were the game's** (uninstall would restore the conflict).
- **A refusal after the previous route was removed.** Every refusal that can
  be known from metadata (the OptiScaler package check, the pin, the
  helper/D3D10 minimums) comes before the first write.
- **A route recommended that the dropdown does not offer.**
- **A route steered to because of a fault that does not apply**: the driver
  fault is the renodx-dlss5 path; OptiScaler and native on a game with
  own DLSS are not moved by it.
- **Overwriting the person's own settings that the tool does not manage**:
  `OptiScaler.ini` keys outside the table, `ReShade.ini` keys, `rtx.conf`
  lines, a deliberate `NeuralUplift=0`.
- **An all-users registration of any kind** (see the bugs below).
- **A global layer registered without saying it is global.**
- **A third-party download run, or its ini obeyed, as the tool's own
  configuration.**
- **A multi-frame or own-frame-generation file that is not placed
  completely or not recorded.**
- **A Windows graphics preference changed without a record to undo it**
  ([gpu](gpu.md)).

## Bugs and questionable choices in the reference not to port

- **Route rules scattered over code, text and UI.** The decision is in
  `_detect_routes` (`dlss.py:781`), then `detect` (`dlss.py:454`) with
  three steering stages, then `fit` (`dlss.py:634`) for the labels, then
  `plan` (`installer.py:1785`) and `preview` (`installer.py:2012`) re-derive
  what the install will do, with comments saying "keep the two together" (for
  example `vulkan.will_do`, `vulkan.py:378`; and "gate 2.0.6: the preview
  promised a Program Files copy the install skipped"). Every comment of that
  kind is a defect that happened. The rewrite computes one **plan**: a
  typed object (route, options after overrides, files to write with their
  source and destination, configuration edits, warnings and refusals), shown by the
  preview and executed by the install without re-deciding.
  - Changed from the reference: reasons, notes and warnings are keyed
    messages with parameters, not formatted English glued to the rule; the UI
    and the command line render the same plan.
- **The route list for an unknown API is assumed D3D12** (`dlss.py:968`
  "DX12 or unknown -> assume DXGI/D3D12") and a game with own DLSS then
  gets native recommended with no question asked. Rule 8a asks instead.
- **The refused pin installs the newest build** (`installer.py:4769`): a
  warning, and `renodx-dlss5` 5.2.1 goes in when the pin 4.55 is missing, "the
  kind of build that pin exists to avoid" (see [components](components.md)).
- **The refusals that come after the removal of the previous route.** The
  previous route is uninstalled (`installer.py:3848`) before the OptiScaler
  package is checked for `OptiScaler.dll` (`optiscaler.py:759`), before the
  feeder tag is resolved for the D3D10/helper/built-in checks (these are
  early, `installer.py:3759`, but only when the feeder is the route; the
  same for the pin), before the DLSS runtime is downloaded and checked
  against the card (`installer.py:4806`). A failure then rolls a switched route
  back, which is what `_roll_back` is for; the better behaviour is to resolve
  and download everything first, then remove and write.
  - Changed from the reference: resolve and verify every component of the
    plan, check the GPU build, before the previous route is touched.
- **The all-users layer is "extracted again, not copied from the per-user
  folder"** (`vulkan.py:349`) but from `setup_exe`, which is the cached
  download in the per-user cache (`installer.py:4453`): the same folder any
  process of that user can write. An elevated process placing a DLL under
  Program Files from it carries whatever was put there. The same applies to
  the per-user folder itself, in a lower degree (it loads into every Vulkan
  application of the user).
  - Changed from the reference: no all-users registration in v1
    ([install](install.md)).
- **`d3dcompiler_47.dll` is renamed unconditionally** (`installer.py:3499`),
  with a note that the game's copy "is older than Windows'", which is never
  checked. A game's current copy is moved for nothing, and the game's own
  folder is altered on a guess.
  - Changed from the reference: [install](install.md) owns the one
    mechanism: compare the file's version with System32's first, move aside
    only a copy that is older, say the versions.
- **`_shared_steer` swallows every error** (`dlss.py:520`, `except Exception:
  pass`) so a malformed community list silently changes nothing and no one can
  tell why a recommendation differs between machines. Log it.
- **The recommendation depends on the network** (the shared list). An offline
  machine and an online one get different "recommended" routes for the same
  game with the same detection. The preview must say which list (or none) the
  recommendation used and its date.
- **Content sniffing to decide whose a file is**: `_is_reshade` reads
  whole files over 1 MB for the bytes `ReShade`
  (`installer.py:611`), `is_dxvk` for `DXVK`, `is_optiscaler` for the UTF-16
  name, and `foreign_lumenite` walks `reshade-shaders` with `rglob`.
  Prefer the record for "ours", and read the PE version resource
  (`OriginalFilename`, `ProductName`) for "what is it".
- **Silent defaults that change meaning per route.** `Options.provider = 3`,
  `renodx = None`, `path = feeder` (`installer.py:359`-`371`) are defaults of
  a dataclass that every route shares; fields that mean nothing on a route are
  still carried and recorded. Options are per route in the rewrite; a route's
  options contain only what it reads.
- **The OptiScaler.ini update notice is switched off** on every install
  (`optiscaler.py:823`) without saying so. Kept off (the tool, not
  OptiScaler, decides which build is installed), and the install report now
  says "OptiScaler's own update notice is switched off: this tool keeps the
  build current".
- **`write_preset` and the technique order** are written for the provider the
  person picked, but a provider that is not installed (0 and 1 need the person's
  own shader) leaves a technique list that names nothing; a red error in
  ReShade's overlay is the only sign. Warn at install.
- **Two proxy name lists.** ReShade's six names (`installer.py:631`) and
  OptiScaler's seven (`optiscaler.py:203`) are separate and share `dxgi.dll`,
  `d3d12.dll` but not the rest; a game that imports `version.dll` can be
  served by OptiScaler but not by ReShade, and the only guidance is a help
  line per name. Name the import table the game really uses instead (the
  RTX 40 route already reads it, `mfg.py:178`) and offer the names the game
  imports first.
- **The Vulkan layer's registration** names the manifest by its full path
  in the registry. The manifest itself names its DLL relative to itself
  (`.\<dll>`: `vulkan.py:176-196` rewrites `library_path`), so the files
  stay consistent; a moved data folder leaves only the registry value
  dangling. The record carries the value's path, and uninstall removes it by
  enumerating values, not by building the name.
- **DXVK is offered as a tick on D3D11 only for native, bridge and feeder**
  but forced for D3D9 and D3D8, with a settings rule that re-reads the
  executable list in three places (`installer.py:687`, `:724`,
  `ctl_game.py:947`). One rule, one place.
- **Unknown architecture is "32-bit" in the route list** (`dlss.py:876`,
  `bitness != 64`) but refused at install (`installer.py:773`). The list
  must not offer a route the install will refuse.

## To verify against primary sources

| Claim | Source |
| --- | --- |
| ReShade's add-on build loads as the six proxy names; each name stands in for which system library; the 32-bit and 64-bit packages and the `ReShade64.json`/`ReShade32.json` layer manifests | ReShade's documentation and the installer; a trial in a clean VM |
| The Vulkan loader skips HKCU for elevated processes, de-duplicates implicit layers by name, and rejects a layer of the wrong bit-type | Khronos loader documentation (`LoaderLayerInterface`); the loader's own trace with `VK_LOADER_DEBUG=all` |
| ReShade's installer registers under HKLM when elevated; that a layer with `disable_environment` or a different name loads in both | ReShade source and the layer manifest |
| That DXVK's `d3d8.dll` imports `d3d9.dll` and DXVK ships `d3d8.dll`; archive layout; which games load a renderer by `bin\shaderapidx9.dll` | DXVK release and README; Source engine behaviour |
| OptiScaler: the proxy names it supports, `[DlssNr]`, `[Inputs]`, `[FrameGen]`, `[OptiFG]`, `[Menu]`, `[Hotfix]`, `[Log]`, `[Upscalers]`, `[DLSSG]` keys and values, which fork has which key | each fork's `OptiScaler.ini` and docs; do not infer from one fork |
| That OptiScaler's D3D11 path needs `Dx11Upscaler=fsr22_12` and the model does not run on D3D11 | the fork author's statement and a trial |
| `AdaMfgUnlock`, FSR 3.1 frame generation libraries in the package | the packages |
| The Windows `d3dcompiler_47.dll` version and the `cs_5_1` target claim | Microsoft documentation; a trial with an old copy |
| That `renodx-dlss5` loads when found beside the executable under that exact file name, that `nvngx.dll.addon64` must keep its name | the add-on READMEs |
| Streamline file names, `nvngx_dlss.dll`, `nvngx_dlssd.dll`, `nvngx_dlssg.dll` and where Unreal and CryEngine keep them | NVIDIA's DLSS and Streamline documentation; sample game folders |
| That RE Engine's marker file is `re_chunk_000.pak` and REFramework's `dinput8.dll` is what loads first | REFramework's README |
| The anti-cheat marker names | each product's install layout |
| That an elevated game reads only HKLM for implicit layers, and the machine's list location | Khronos documentation |
| That `rtx.conf` keys for each Remix runtime flavour are as recorded, and the in-game path Alt+X, Developer Settings, Post-Processing | the runtime forks' documentation; a run |
| That the MFG unlock requires the game's own DLSS frame generation and the ASI loader's behaviour (`LoadPlugins`, `LoadFromScriptsOnly`, `LoadExtraPlugins`) | MFG-Unlocker's README; Ultimate ASI Loader's ini documentation |
| Windows has no per-user setting that lets an unelevated process write to an Xbox/Game Pass game folder | Xbox app documentation |

## Fixtures

- **Detection inputs** as small folders (names only; zero-byte files are
  enough except where a bitness or an API is read): each of rows 1 to 10
  above, with and without own DLSS, nested Unreal DLSS, a swapped file with its
  backup, the three NEEDS_DXVK executables, a `.trex` folder, `re_chunk_000.pak`,
  BattlEye markers, a launcher executable beside a real one. Expected: the
  option list, the recommended route and the reason key.
- **Steering cases**: own DLSS D3D12 on sm 75, 86, 89, 120 and unknown; FSR game
  on sm 89 and 120; the shared list with enough reports for a game (a
  per-game result wins), enough for the class on two routes with overlapping
  and non-overlapping ranges, uplift as the best, a pick not on offer, a
  malformed list; drivers 616.55, 616.56, 616.64, 616.92 with and without a
  NEEDS_DXVK executable.
- **Install plans**: for each route, the exact set of files and configuration
  edits, per API and bitness (32-bit feeder, helper mode, Source D3D9 under
  `bin`, D3D11 DXVK, Vulkan, OpenGL); a golden file per case, produced from
  the plan, not by installing.
- **Config files**: a tuned `OptiScaler.ini` before and after (unrelated keys,
  comments and the ordering unchanged), a fork's shipped ini with `AdaMfgUnlock=false`,
  a `ReShade.ini` carrying `NeuralUplift=0`, an `rtx.conf` with and without
  a final newline, a `dlss5-feed.cfg` written by the add-on with comma
  decimals (#69), a `dlss5-bridge.cfg` unstamped, stamped with an older
  version, and with `keep`.
- **Registry**: the layer lists under HKCU and HKLM with ReShade's own, with
  ours, with the old shared name, a 64-bit only registration and a 32-bit
  game, run against an in-memory registry behind the platform interface.
- **The reference's own tests**: `git show d5801a3^:test_all.py` sections 6b
  and 6f (routes and detection), 8 (route rules, pins, the OptiScaler dials), 21
  (neural-upstream), 22 (FSR and XeSS through OptiScaler), 23 (standalone),
  24 (only the selected motion-vector shader is installed). Behaviour hints
  only; several encode the defects above.
- **Real reports**: `_tools/reports/` files carrying `route:`, `arch/api:`
  and `gpu:` lines; 031, 037 (Vulkan and renodx pins), 175, 225 (nested
  Unreal DLSS), 364 (the wrong OptiScaler package), 434, 562; and
  `docs/routes.svg` in the reference for the shape of the decision.

## Open questions

- **Q1.** The shared compatibility list can move the recommendation. It is
  fetched; who runs it, and may the rewrite keep reading it? (It is a
  community result list with no integrity guarantee; a poisoned list could
  steer people to a bad route, though never to an unlisted one.)
- **Q2.** The `renodx` (ShortFuse) route is listed for most games although
  "reported not working in many games". Keep it listed everywhere, or only
  where it is the sole way in (64-bit D3D9) and behind a disclosure elsewhere?
- **Q3.** The per-route "reliability" levels are measured on the author's
  games. Show them as the reference does, or drop them for the shared list's
  own numbers?
- **Q4.** The tool's own default of 75 percent for the model-resolution dial:
  is it still the right start on RTX 20 and 30 cards, where the pass is
  heaviest?
