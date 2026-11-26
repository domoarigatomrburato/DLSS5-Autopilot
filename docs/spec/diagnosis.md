# Diagnosis: did it work, and what next

The tool reads the logs and the folder a game left behind, plus what the game
had loaded while the tool watched it, and says in one sentence whether DLSS 5
ran, why not, and what to do next. It also builds the bug-report text, and
measures what a session cost. This spec covers every log the tool reads, the
verdict catalogue and its classes, the order evidence is weighed in, the "try
another route" offer, the crash record Windows keeps, the report and its
privacy, and the report corpus that becomes the regression set.

Read from the reference's `core/diagnose/*` (chain, evidence, routes, live,
helper, process, layer, body, model), `core/verdicts.py`, `core/wincrash.py`,
`core/reportout.py`, `core/autotune.py` (the measuring half), `core/community.py`
(route ranking), `core/ui/ctl_game.py` (crash corrections), `_tools/*`, the
README section "When it does not work", and `_tools/reports/` (184 reports).
The session side (watcher, autopilot, compare, session cards) is in
[watch](watch.md). Two other specs feed this one: [net](net.md) for the
network failure kinds, and the install record (the "manifest") that the
install spec will define. This spec names only the fields it reads (see
"What the diagnosis reads from the install record").

## What the user gets

### The answer

One call, `Analyse(installFolder, lastInstallError)`, returns a **report**:

| Field | Meaning |
| --- | --- |
| `Verdict` | the one sentence, with a stable **verdict id** beside it (below) |
| `Findings` | ordered list of (level, title, detail), level one of ok, warn, bad, info |
| `Ran` | the logs show the game ran with this install |
| `NeverRan` | the verdict rests on there being no log; better evidence that the game ran (a fault record, a sighting) must replace it |
| `NoRecord` | the folder has no install record; nothing may be dated "since the install" |
| `Route` | the route from the record, or read off the files when the record is gone |
| `LogTime` | the modified time of the log the verdict rests on, shown as `dd MMM HH:mm` |

The verdict is the headline; findings are the evidence under it. People act
on the verdict and not on the findings, so anything that changes what to do
(a second DLSS hook, a conflict, a person's answer) must be in the verdict.

Changed from the reference: every verdict carries a **stable id**, a class,
an actor and a next-step kind as data. The reference matched its own printed
sentences against about 120 word fragments (`verdicts.py:24-270`) to find a
verdict's class; a reworded sentence silently became "unmapped", and about 20
verdicts the tool can print were already unmapped (listed under "Bugs"). Tests
assert the id, never the text. Text is a template filled from evidence and is
free to be reworded.

### Every log and file the tool reads

Logs are read **from the end**, up to a cap, and then cut to the **last
session** in them. Logs are appended to run after run; only the last launch
describes what the person just saw. A log whose newest write is older than the
install (minus 60 s of slack, `evidence.py:1603`) describes the previous setup
and is not evidence about this one (it may still be shown as a finding).

| Source | Where | Read (tail cap) | Last session starts at | What is extracted |
| --- | --- | --- | --- | --- |
| ReShade log | `ReShade.log` beside the exe | 250 000 bytes | last line containing `Initializing crosire's ReShade` (the whole tail when it was cut inside a session) | `Registered add-on "<name>" v<ver>` lines (which add-ons loaded); `loaded from '<path>' into` (a ReShade proxy named d3d9.dll); `Direct3DCreate9` vs `CreateSwapChain`/`Presenting`/`vkCreateSwapchainKHR` (which API the app drew with); `Exiting`; `Failed to compile\|load <file>` (shader failures, essential vs not); `vtable::Hook(... EvaluateFeature ... hooked` and `Failed to find NVSDK_NGX_*_EvaluateFeature*`; `untested build`; `focus window is the desktop window`; every `\| INFO\|WARN\|ERROR \| [Name]` voice (an add-on that speaks is loaded even when its registration line fell out of the tail); `D3D12CreateDevice failed ... 0x887e0003`; add-on lines under the tags `[NRPRE]` (neural-upstream) and `[Uplift]`/`Uplift ... registered`/`NR ran on the game's frame (...)` (GITC-Uplift); timestamp of the first and last stamped line (for the session length, see [watch](watch.md)) |
| Feed log | `dlss5-feed.log` | 400 000 | last line matching `^[\d:.]+\s+dlss5-feed\S*\s...attached[.;]` | add-on name and version, `classic` engine tag; `DLSS5_Feed.fx technique found\|MISSING` (the last one counts) and `has not resolved yet` (still compiling); `DLSS5_MV_PROVIDER=n (name) -> technique (state)`; `MV probe ... N% non-zero`; `Depth probe ... sampled depth is flat`; `host spawned`/`host connected`/`speaks protocol vN, this add-on\|host vM`; `rejected late add-on registration`; `building: WxH` (every size the feature was built at); `frame N delivered\|evaluated`, `first frame fed`, `N frames: feed CPU X ms/frame ... Y fps`; `feature ready`; `CreateFeature raised exception 0x..`; `evaluate raised 0xC0000005` and the `... stack, by module (innermost first): a <- b <- c` line; `### CRASH\|EXCEPTION RECORDED ### exception 0x.. ... at <addr> in <module>; this add-on was last doing: <step>` and `crash dump written: <path>`; `stopped: <reason>` after the last frame; `an external frame pacer is presenting this swapchain: ...`; `SuperSampling.Available=0\|1`; `NVSDK_NGX ... -> 0x00000001`; `SuperSampling.FeatureInitResult: 0x.. (name)`; `would not set DLSS up inside this game's process`; `dlss5-feed.addon64 stands down`; Shader Model 5.1 compiler complaints (`is too old for Shader Model 5.1`, `rejects cs_5_1`, `unrecognized compiler target 'cs_5_1'`). NGX's own copied block (between `===== NGX's own log` and `end of NGX's own log`, or the feed's next own line) is **removed first**: its words read as the feed's. |
| Helper log | `host64\dlss5-feed-host.log` (32-bit feeder, helper mode) | 150 000 | last banner `^[\d:.]+\s+dlss5-feed-host...` | `[host] Present failed 0x887A0005` (device removed); `[host] neural consumer outcome: ...`; `NeuralUplift=0\|1`; `NVSDK_NGX_D3D12_Init -> 0x1 (Success)`; the add-on file and version it ran |
| Bridge log | `dlss5-bridge.log` | first 64 KiB and last 1 000 000 bytes | last line `<clock>  dlss5-bridge <ver> (built ...) attached.` | build; frames (`[bridge\|synth] [D3D12 \|Vulkan ]frame N delivered`, `N frames delivered so far.`, `[vkmirror] frame N recorded`); frame-rate lines and "WINDOW NOT IN FOCUS"; the stop reasons (`stopped: <why>. The game renders normally`, `evaluate raised exception 0x.. -- disabling`, `[synth] ... after N delivered frames`, optical-flow-stopped lines, `[vkmirror] <why>. The game's own DLSS was forwarded`); `it faulted in <module>`; `### CRASH RECORDED ###` block; `CreateFeature failed with result 0x.., name`; `evaluate failed with result`; `CreateFeature calls: 0, EvaluateFeature calls: 0`; `[vkmirror] ReShade created an effect runtime on D3D12`. A previous run's crash that the bridge reprints at attach (`The previous run crashed`, up to the first blank line) is dropped. |
| OptiScaler log | `OptiScaler.log` beside the exe, else the newest `Logs\*.log` | 400 000 | a clock that jumps back more than 60 s (not a midnight wrap: previous value above 86 100 s and new below 300 s) | `DLSS-NR` lines: `running at WxH`, `... cost\|elapsed: X ms total [= \| , Y ms model]`, `finished picture: N frames`, failure words (`create failed`, `unavailable`, `did not run`, `not found beside`, `would not load`, `disabling for this session`, `refused`), settings echoes `DlssNr.<key>:`; `forwarder loaded`; the input hooks (`context created`, `hk_ffxFsr2`, `hk_ffxFsr3`, `hk_ffxCreateContext`, `hk_xess`, `XeSS Version:`, `libxess.dll found` seen; `libxess.dll not found!`, `disabling FSR2 hooks!` never); `Vulkan is creating swapchain`; `Original present result: <8 hex>`; `dlfgPresent ... present failed`; `[writeMiniDump] Exception detected` and its `][<n>s:` offset; the unload markers `DLL_PROCESS_DETACH`, `Unloading OptiScaler`, `TryDestroyNGXParameters`, `ReleaseFeature` |
| Standalone log | `%LOCALAPPDATA%\RHI\Logs\standalone-dlssnr.log`, **one file for every game** | 200 000 | last ` attached; requested profile=` line | `standalone contract ready: ...`; `on-present frame N:`; `standalone pipeline FAILED at <stage>`; `required private runtime dependency missing`; `NGX core: no _nvngx.dll found`; `same-frame VORT optical flow`, `zero-motion`, `fallback guides`; `DLSS-G runtime unavailable`, `falling back to real frames`, `frame generation disabled`; `native presentation (initialization )?(failed\|rejected)`; `waiting for a valid ... shared frame`; `standalone session ending`, `clean process shutdown`. Only evidence about this folder when this is the **only** standalone install the tool made (otherwise another game wrote it) and it was written after this install. |
| Remix log | the runtime's `rtx-remix\logs\remix-dxvk.log` (found through the `.trex` runtime) | 300 000 | whole tail | `[DLSS-NR] Loaded .trex\nvngx_dlssnr.dll`, `Snippet initialized`, `Created the Neural Uplift feature (id, preset) at WxH`, evaluated-frame counts, the runtime's failure phrases and skip phrases, `NVIDIA DLSS-NR inactive:` |
| DXVK logs | `<exe-stem>_d3d9.log`, `_d3d11.log`, `_dxgi.log`, ... beside the game | existence and age | | proof the game **ran** since the install on a DXVK route (DXVK writes one the moment it loads) |
| Install record | `dlss5-autopilot.json` (older names `dlss5kur-kurulum.json`, `dlss5-installer.json`) | whole | | see next section |
| Other files | `ReShade.ini`, `OptiScaler.ini`, `dlss5-feed.cfg`, the dlss page's record | whole | | the add-on's own switch (`[<section>] <key>` in ReShade.ini: on / off / absent), OptiScaler's `[Log] LogToFile` and `[DlssNr] Enabled`, `WorkingScale`/`work_resolution` |
| Process record | the watcher's sightings, or a live process | | | see [watch](watch.md): which of our files the process had loaded, which it had from elsewhere, which graphics DLLs it held, how many modules, whether the list was refused |
| Windows' crash record | the Application log | | | see "The Windows crash record" |
| The game's own files | per-user data folders and the exe's folder tree | bounded walk | | "did the game write anything after the install" (below) |
| The Windows compatibility flags | the exe's value under `HKCU` / `HKLM` `...\AppCompatFlags\Layers` (both registry views) | | | `RUNASADMIN` means every start is elevated |

All number formats in add-on logs may use a decimal comma (a C++ add-on takes
the machine's locale): read `0,75` and `7.41` alike, and refuse `1.2.3`
without raising.

Text decoding: UTF-8 with replacement; log files are written by other
programs in other code pages.

#### What the diagnosis reads from the install record

`path` (the route), `exe`, `proxy` (a file name, or the marker
`(vulkan layer)` for a registry layer with no file), `files` (every file the
install wrote, relative), `bitness`, `api`, `kind` (game or video), `dxvk`
(version, when DXVK was installed), `complete` (false while unfinished), `notes`
(a disk-full note, a rolled-back note, a stop note, an "uninstall left" note,
"replaced a previous ..."), `tool` (the version that wrote it), `components`
(e.g. renodx build, optiscaler release tag, remix runtime), `opti_build`,
`upscaler`, `provider`, `feed_helper`, `native_dlss` (false = a bridge
installed for a game with no DLSS of its own), `vr`, `remix {key, conf}`,
`own_fg`, `gpu_pref`. The install spec owns the format; the diagnosis must not
need anything else.

### Verdict classes

A verdict has a **stage** (where on the chain it stands), an **actor** (who can
act on it), and three flags. One list, read by the ranking of what to fix next,
by the "another route" offer, and by the shared-result outcome, so they cannot
disagree.

The chain: install, nothing of ours loaded, the game refused the hook, loaded
but the feed never got going, set up but not switched on, loaded and we cannot
see, the add-on crashed, the model refused, the driver's runtime faulted,
working.

| # | Stage | Actor | Route failed (another route may help) |
| --- | --- | --- | --- |
| 0 | working | none | no |
| 1 | the install stopped | us | no (install again) |
| 2 | a file went missing after the install | the person's antivirus | no |
| 3 | nothing of ours loaded | us | yes, except the `not-a-route` ones |
| 4 | the game refused the hook | upstream | yes |
| 5 | loaded, the feed never got going | upstream | yes, except `not-a-route` |
| 6 | set up, not switched on | the person | no |
| 7 | loaded, and we cannot see | us | no |
| 8 | the add-on crashed | upstream | yes, except `not-a-route` |
| 9 | the model refused | upstream | yes |
| 10 | the driver's runtime | NVIDIA | no |
| 11 | a second DLSS hook beside ours | the person | no |

(`verdicts.py:24-270`.) Stage 11 is checked last in the reference so that an
"... beside ours too" suffix does not move a verdict out of its own stage; with
ids the order is irrelevant: the suffix is a flag on the report (`ForeignHooks`),
not a change of id.

**`not-a-route`** verdicts are about how the game was set up or started, so the
next route goes in the same way and meets the same thing. They never offer
another route, and a shared result never counts them against the route
(`verdicts.py:288-314`): `install.beside-launcher`, `loaded.feed-too-early`
("did not get far enough"), `layer.runs-as-admin`, `api.not-dxgi-rescan`,
`api.renderer-unreachable`, `process.no-dxgi`, `process.only-ddraw`,
`api.d3d9-device-on-dx11-install`, `process.other-exe-ran`, `process.starter`,
`addon.third-party-asi-fault`, `opti.proxy-not-loaded` (another "loads as"
name on the same route), `reshade.ran-wrote-no-log`.

**Outcome** (`verdicts.py:410-427`): `worked` for stage 0; `failed` for stages
the logs prove; `unknown` ("only the person can say") for stages 6, 7 and 11
and for an unmapped verdict; except that the verdicts the **person's own answer**
produced ("it closed itself", "never started", below) and a ReShade d3d9.dll in
front of DXVK are `failed`.

Changed from the reference: stages 1 and 2 (the install stopped, a file went
missing) are **not an outcome of a route** at all: the reference returns
`failed` for them and a shared result would blame the route for a full disk or
a quarantined file. Their outcome is `not-a-result`: never shared, never
counted. The reference already excluded 11 and `not-a-route`; this extends it
by the same reasoning (the route was never run).

### The verdict catalogue

The table lists each verdict by id, the evidence that produces it, and the
next step the sentence must carry. Rows keep the reference's evidence rules;
the wording is the next step in the user's terms (route and setting names come
from the app, not from the reference's text). Stage in brackets, `NR` =
not-a-route, `R` = route failed.

**Folder and install (stage 1 unless noted)**

| Id | When | Next step |
| --- | --- | --- |
| `install.nothing` | no record, none of our marker files (`dlss5-feed.addon64\|32`, `dlss5-bridge.addon64`, `renodx-dlss5.addon64`, `dlss5-feed.log`, `OptiScaler.ini`, `nvngx_dlssnr.dll`; on Remix `.trex\nvngx_dlssnr.dll`), and no install traceback; `NeverRan` | install first, play once |
| `install.crashed` | folder empty of ours (no record, or a complete record and still nothing) and the tool's last error came out of the install path (any frame from a module the install reaches; a frame of the window's own does not count) | install again; per cause: `blocked` allow the tool through the firewall; `cut` another network or turn off the VPN/proxy; `dns` check the connection; `refused` another network; `timeout` try later. The cause is read from error codes first, text last ([net](net.md)) |
| `install.disk-full` | record incomplete with the disk-full note | free a few hundred MB on the game's drive and on the drive holding the cache, install again. Says whether what was written was taken back out |
| `install.stopped-by-reason` | record incomplete with a stop note | the reason in the person's terms; says whether it was rolled back |
| `install.net-stopped` | record incomplete, error names a connection fault | as the `crashed` causes |
| `install.unfinished` | record incomplete, no reason; lists files recorded and gone | install again (downloads are kept), or uninstall first |
| `uninstall.incomplete` | record incomplete with an "uninstall left" note | close the game, uninstall again |
| `install.opti-wrong-release` | OptiScaler: no proxy beside the exe, and the release tag the record names is not an OptiScaler package (or is unrecorded on an install made inside the known window when wilsjo2's page carried a non-OptiScaler release as newest) | install again (the tool now refuses such a package) |
| `install.opti-no-proxy` | OptiScaler: no log, no proxy | check the proxy is beside the exe the game launches and was not quarantined. See the LIMIT below |
| `install.beside-launcher` NR [3] | no log; the recorded exe is launcher-like (by its PE: imports/size/name rules of the game-reading spec) | pick the exe that draws (named when the folder holds a likely one), install again; `NeverRan` |

LIMIT carried from the reference (`verdicts.py:37-48`): `install.opti-no-proxy`
is also printed when a proxy was written and something removed it, which is
stage 2's shape. It is staged "us" because an install that wrote nothing looks
identical from the folder and blaming the person's antivirus on a guess is the
one answer the tool does not give. Keep it.

**A file went missing after the install (stage 2)**

| Id | When | Next step |
| --- | --- | --- |
| `missing.proxy` | route ReShade, proxy file recorded and gone | restore from quarantine, exclude the folder, install again |
| `missing.dxvk` | DXVK files recorded and gone | same |
| `missing.files` | recorded `.addon64`, `.addon32`, `.fx` or `nvngx_dlssnr.dll` gone and no DXVK log proves a run on a layer route | restore first (Windows Security > Protection history), exclude, then install again; a shader gone is more often the launcher verifying files |
| `missing.layer` | proxy is the layer marker and no ReShade layer is registered at all | install again |
| `missing.layer-bitness` | a layer is registered, but not for this game's 32/64 bits | install again (it registers the missing one) |
| `missing.opti-ini` | OptiScaler: proxy there, `OptiScaler.ini` gone | install again |
| `missing.shader` | standalone: the log says zero-motion guides and a recorded shader is gone | install again |
| `missing.runtime-standalone` [unmapped in the reference] | standalone log: `required private runtime dependency missing` | reinstall (puts every file back) |
| `missing.uplift-runtime` | GITC-Uplift: `nvngx_dlssnr.dll not found next to` | install again |
| `remix.runtime-gone` | no `.trex` any more | the mod was removed or files verified; nothing on this route works without it |

Also a **finding** (not a verdict) whenever logs exist: each recorded add-on
that is gone is named, since the log may predate the quarantine.

**Nothing of ours loaded (stage 3)** ("was it started? did it load?")

| Id | When | Next step |
| --- | --- | --- |
| `loaded.not-started` | no log, folder intact, nothing written by the game since the install, nothing running | run the game once, check again; `NeverRan` |
| `loaded.not-started-no-record` | the same without a record | install again, run once |
| `loaded.installed-after-run` | a ReShade log older than the install, nothing newer | play once, check again; `NeverRan` |
| `loaded.looks-ran-not-loaded` | no log, something in the game's files changed after the install | most likely the proxy name or the executable |
| `loaded.opti-not-run` | OptiScaler: proxy there, ini there, logging on, no log | play once; or check the proxy sits by the right exe |
| `loaded.remix-not-run` | Remix: no or stale log | play once, or Remix is not loading |
| `layer.dxvk-ran-reshade-did-not` | DXVK log newer than the install, no ReShade log | start the game normally; or install again with the tool run as administrator |
| `layer.dxvk-ran-reshade-did-not-registered-everywhere` | the same, with a layer registered in both lists an elevated game reads | report it |
| `layer.runs-as-admin` NR | the same, and the exe has `RUNASADMIN` set | untick it in the exe's Compatibility, or install again as administrator |
| `layer.name-clash-32bit` | 32-bit DXVK game; the 32-bit and 64-bit ReShade layer manifests share a layer name so the loader keeps one | install again (it gives the 32-bit layer its own name) |
| `layer.loaded-no-log` | our DXVK/other files in the process, no add-on, layer registered | install again; close overlays that bring a Vulkan layer; run the exe directly |
| `layer.not-reached` | native Vulkan install, nothing of ours in the process | same as above |
| `process.proxy-not-loaded` | process ran and held none of our files (or Windows' copy of the proxy name was loaded instead) | another "reshade loads as" name; on OptiScaler another "loads as" name |
| `process.other-exe-ran` NR | the process that ran is not the recorded exe, and not the same folder | pick that exe, install again |
| `process.other-folder` | the same exe name ran from another folder | pick that one, install again |
| `process.elsewhere` | the same DLL name loaded from a folder other than ours (not an NVIDIA runtime copy; only System32's can stand in front) | remove the other, or install under another name |
| `process.starter` NR | few modules (under 45) and no graphics DLL | install beside the exe that draws (named when the folder holds a likely one) |
| `process.not-drawing-yet` [7] | 45+ modules and no graphics DLL | press the button again while the game is drawing |
| `process.no-dxgi` NR / `process.only-ddraw` NR | a DXGI-family proxy was installed, the process held d3d9/d3d8/opengl/vulkan and no DXGI | set "graphics api" to what the process held, install again; DirectDraw is reached by no route |
| `api.not-dxgi-rescan` NR / `api.renderer-unreachable` NR | the engine beside the exe says the game does not draw with DXGI | full rescan, install again |
| `api.d3d9-device-on-dx11-install` NR | the ReShade log shows `Direct3DCreate9` and no swap chain on an install set up for DX11/12 | set the graphics API to DirectX 9 and install again (a video player: choose another output renderer) |
| `api.vulkan-on-d3d-optiscaler` | OptiScaler log: `Vulkan is creating swapchain` on a D3D install | set the game to DirectX 12, or use the feeder route |
| `reshade.d3d9-before-dxvk` [11] | a ReShade `d3d9.dll` in front of DXVK on a DX9-through-DXVK install | remove that file (or uninstall then install, when the record lists it) |
| `reshade.ran-wrote-no-log` NR | an add-on in the process, no `ReShade.log` | the folder is read-only to ReShade (store/Xbox build, or admin-only folder) |
| `process.protected` [7] | the process refused its module list | an anti-cheat, a store build or an elevated game; see anti-cheat answer below |
| `process.no-record-to-read-against` [7] | process running, install record lists nothing to look for | close it, install again |
| `process.ours-loaded-no-log` | our proxy in the process, no log | close the game, press again; report if it persists |
| `uplift.beside-other` | GITC-Uplift refuses to run beside a loaded add-on that runs NR too | move that add-on out |
| `uplift.runtime-beside-exe` | RTX 50: NVIDIA's DLSS loaded `nvngx_dlssnr.dll` from beside the exe first | install again (it moves that file aside) |

**The game refused the hook (stage 4)**

| Id | When | Next step |
| --- | --- | --- |
| `hook.opti-swapchain-refused` R | OptiScaler: last `Original present result` non-zero and no model work after it | another "loads as" name (winmm.dll), another build, or the feeder route |
| `hook.upstream-no-ngx-call` R | neural-upstream: no hook line on the game's DLSS call | the optiscaler route also works on DirectX 11 |
| `hook.d3d12-redist` | `D3D12CreateDevice failed 0x887e0003`; not on a helper-mode install | the game asks for an Agility SDK it does not ship; try the feeder's helper mode. Names the unmet requirement read from the exe's PE when it can |
| `hook.protected-by-anticheat` | person said "never started" and the process was protected, or an anti-cheat sits beside the game | uninstall; no route gets past it |

**Loaded, the feed never got going (stage 5)**

| Id | When | Next step |
| --- | --- | --- |
| `feed.no-effect-runtime` | feed attached; no log line that only exists once an effect runtime exists | tick "DLSS 5 Feed" in the effect list; send both logs |
| `feed.inconclusive-early` NR [also stage 5] | nothing decides | play longer, closed during shader compile is this shape |
| `feed.started-and-closed` | ReShade started and none of the "got somewhere" words (registered, redirecting, initialized runtime, swap chain, compiled, exiting, effect, created) | uninstall, check the game starts alone, try another name in "reshade loads as" |
| `bridge.no-frame-called` | the bridge attached, the game called DLSS (or evaluate failed), no frame | the bridge panel says why; report with the bridge log |
| `bridge.no-call` | no DLSS call counted and no frame | on a synthetic contract the feeder route draws its own frame |
| `bridge.attached-no-frame-optical-flow` | synthetic contract: no frame from the driver's optical flow | the panel says why |
| `bridge.no-sr-call` | no Super Resolution call from the game | turn DLSS on; turn Ray Reconstruction off |
| `remix.neural-never-started` | Remix log: a failure phrase, no creation | the failure's own fix text |
| `remix.neural-skipped` | Remix log: the pass skipped frames, with its reason | per the reason |
| `remix.no-neural-pass` | the runtime has no neural pass | use "swap the Remix runtime" |
| `remix.never-attempted` | runtime ran; nothing about DLSS-NR | install again, or swap |
| `addon.no-frame` | upstream: heartbeats but no valid result | switch the route to native |
| `uplift.switched-on-no-frame` | `[Uplift] Enabled=1`, no `NR ran` line | the add-on tab's card says why |

**Set up but not switched on (stage 6)**

| Id | When | Next step |
| --- | --- | --- |
| `switch.feeder-ready-not-on` | `feature ready`, no frames | switch it on in the panel; check the depth buffer |
| `switch.addon-off` | the add-on's own ReShade.ini switch is off | tick it in the overlay (installing again will not, on purpose) |
| `switch.upstream-off` | `[NRPRE] settings loaded: enabled=0` | switch on in the tab |
| `switch.upstream-no-call` R-no | enabled, hooks OK, neither a creation nor an evaluate nor a heartbeat | turn DLSS on in the game and play a minute; or use optiscaler on DX11 |
| `switch.opti-not-asked` | OptiScaler ran; neural rendering never asked for | press the overlay key and tick it |
| `switch.opti-never-drew` | every DLSS-NR line is a setting echo | the game's own upscaler (DLSS, FSR 2+, XeSS) must be on |
| `switch.remix-off` | the runtime logged the pass inactive | Alt+X > Neural Rendering |
| `switch.bridge-substitute-off` | a bridge on a no-DLSS game whose settings file lacks the install's stamp and has the substitute off | install the bridge again |
| `switch.uplift-not-ticked` | registered, no switch | tick Enable |
| `switch.no-log-from-addon` (standalone) | ReShade loaded, the standalone log has nothing | play once, check again |
| `switch.standalone-set-up-no-frame` | contract ready, no frame | play longer / F10 |

**Loaded and we cannot see (stage 7)**: the verdicts that name something the
tool cannot show; every one says what to look at, **except** after the person
said the game did not run (see "The person's answer"), when "look in the
overlay" is replaced.

| Id | When |
| --- | --- |
| `unseen.addons-loaded-confirm` | route logs no frames (native, bridge without its log, renodx, upstream before `[NRPRE]`): add-ons loaded, confirm in the panel. With a switch known on: "the switch is on, the panel is the only live picture" |
| `unseen.bridge-frames-no-proof` | the bridge delivered at least N frames; whether the neural pass drew them is in no log |
| `unseen.bridge-d3d12` | a bridge on a game that drew with D3D12: use the native route |
| `unseen.helper-only` | frames reach the 64-bit helper; only its log says what came back |
| `unseen.opti-mentioned` | OptiScaler mentions DLSS-NR and never reports it running |
| `unseen.remix-silent` | Remix log shows no neural frame (the runtime logs at info level; a higher level hides it) |
| `unseen.dlss-called-no-frame` | upstream: DLSS was called and no frame followed |
| `unseen.log-off` | OptiScaler's log is off (LogToFile not true) |
| `unseen.process-protected-ran` | a sighting says the process was protected |
| `unseen.opti-loaded-no-log` | the proxy was in the process, logging is on, no log |
| `unseen.standalone-attached-no-contract` | the add-on attached and built nothing |

**The add-on crashed (stage 8)**

| Id | When | Next step |
| --- | --- | --- |
| `crash.feed-recorded` R | feed log has a crash/exception record whose stack is the feeder's own | another feeder build; lower work resolution; report to the feeder project with the log and dump (names the dump) |
| `crash.in-game-code` NR-ish | the innermost frame is the game's exe and none of ours below it | turn off overlays (Smooth Motion, NVIDIA overlay, Discord, RivaTuner); confirm stable alone |
| `crash.third-party-asi` NR | innermost frame is an `.asi` we did not write | take it out |
| `crash.in-runtime` | the innermost frame is NVIDIA's runtime (`nvngx*.dll`, `D3D12Core.dll`) | another add-on build; the standalone route; roll the driver back to 616.56 |
| `crash.create-feature` R | `CreateFeature raised exception` | another renodx build, or an `nvngx_dlssnr` that supports the card |
| `crash.feed-stopped` R | frames delivered, then `stopped: <why>` | per why; host log for 32-bit |
| `crash.helper-device-lost` R | the helper's Present failed 0x887A0005 | another add-on build; send the host log |
| `crash.helper-no-feature` R | the helper's own check: the add-on did not intercept / feature 18 failed (not "unavailable"/"could not be opened") | another add-on build |
| `crash.helper-wait-ran-out` R | `host lost: pipe never appeared`, and the helper came up later (or never) | start again / another feeder build |
| `crash.uplift-helper` | GITC-Uplift helper did not start/stopped/did not answer; or could not start on this device | its tab has Retry |
| `crash.bridge-crashed` R | the bridge's crash handler block | per module named; report |
| `crash.bridge-stopped` R | the bridge stopped itself (reason named); with or without frames | the feeder route does not use the bridge |
| `crash.opti-after-draw` R | a Present failed after the model drew (0x887A0005/6/7 = device lost, else a window/mode change) | driver update or lower load; or frame generation off, put the game's own `nvngx_dlssg.dll` back, borderless |
| `crash.opti-streamline-dump` R | the log ends in Streamline's mini-dump line (after a dispatch that kept going it is only a warning; a dump the session did not end on is not a crash) | remove other DLSS tools, frame generation and Reflex off, next "loads as" name, another build |
| `crash.upstream-device-recreated` | upstream: the log ends on a device/swap-chain re-creation with no orderly shutdown | route native or optiscaler; or take the add-on out while loading |
| `crash.windows-recorded` | a counted Windows fault (below) over a "play once" verdict | uninstall, run once alone; see overrides |

**The model refused (stage 9)**

| Id | When | Next step |
| --- | --- | --- |
| `model.opti-refused` R | OptiScaler: a failure line (last failure after last work line) and no work, or work then failure ("stopped after it started") | driver 616.56+, a `nvngx_dlssnr` for the card; native or renodx route |
| `model.ngx-refused-feature` | upstream: NGX refused the feature (non-1 result) and no snippet fallback | native or optiscaler route |
| `model.ngx-refused` | `FeatureInitResult 0xBAD...` | another route; the reason name |
| `model.no-dlss-in-process` | the feeder says NGX would not set DLSS up inside the game's process | the standalone or optiscaler route, or the feeder's helper process |
| `model.nvidia-card-not-used` | `SuperSampling.Available=0` | **finding** only: driver too old or the wrong GPU (laptops) |

**The driver's runtime (stage 10)**

| Id | When | Next step |
| --- | --- | --- |
| `driver.616-64-fault` | `evaluate raised 0xC0000005` and the stack `D3D12Core.dll <- nvngx_dlssnr.dll <- _nvngx.dll <- renodx-dlss5` | on 616.64+: install again (the tool pins renodx 4.55 on those drivers when "auto"), or the standalone route (when the game fits it), or roll back to 616.56. If the record already carries 4.5x: the standalone route or the rollback only |
| `driver.no-entry-point` | the add-on failed to find `..._EvaluateFeature`, nothing was ever hooked, driver below 616.56 (or unreadable and the plain call was not hooked), no frames, no feature, no crash | update the driver; or check it is 616.56+ when unreadable |
| `driver.feeder-versions-mismatch` | the add-on and the helper speak different protocol versions | close the game, wait for `dlss5-feed-host64.exe` to exit, install again |
| `driver.feeder-two-addons` | the in-process feed logged "stands down" for the helper add-on | keep one of the two |

On drivers 616.64+, `Failed to find NVSDK_NGX_..._EvaluateFeature` does not
mean the driver is too old: on a driver that carries DLSS 5 it is a finding
(`WARN: the add-on could not hook ...`) and the chain goes on.

**Working (stage 0)**

| Id | When |
| --- | --- |
| `working` | frames delivered by the feeder/standalone (frames in the log, and none of the rules above fired, no stop line after the last frame); OptiScaler model work with no failure after it; Remix feature created or an evaluated frame; neural-upstream with valid heartbeats (and not ending on a device re-creation); GITC-Uplift `NR ran on the game's frame (...)` |

Working must never be said over: a stop line after the last frame, a Windows
crash of this session, a too-old `d3dcompiler_47.dll` (frames flow, the pass
does nothing: that is `feed.old-compiler`, stage 5: rename the game's compiler
aside; the next install does it), or the person saying the game closed itself.

Also: `feed.no-runtime-resource-build` ("DLSS never started": `failure: resource
build`) and `feed.old-compiler` need ids and stage 5.

### The order evidence is weighed in

**The order is the behaviour**: a rule moved ahead of another changes what
every later rule sees. The corpus below exists to catch exactly that. The
ladder, first match wins unless marked "adds a finding":

1. **Identity of the folder.**
   1. No record and nothing of ours: `install.crashed` when the install's
      traceback explains it, else `install.nothing`.
   2. No record but ours present: continue with `NoRecord`, route read off the
      files (OptiScaler markers, then Remix `.trex`, bridge add-on, upstream
      `nvngx.dll.addon64`, feeder add-ons or log, standalone add-on, GITC
      add-ons, `renodx` build, plain renodx). Say a finding that the record is
      gone. Nothing may be dated against an install time.
2. **The record says unfinished**: disk full, stop reason, an uninstall that
   left files, a network fault in the last error, else generic. The folder's
   contents mean nothing until the install finished (a cut-off download
   would otherwise read as files "missing").
3. **Complete record, empty folder, and a crash trace** (a re-install that
   crashed over an earlier record): `install.crashed`.
4. Add an info finding when another build of the tool made the folder (never a
   verdict). Said as older or newer; a newer one is a rollback and gets no
   "install again".
5. **Route readers that own their logs**: OptiScaler, then Remix (neither
   installs ReShade). A ReShade log beside the game since the install is
   somebody else's: its DLSS-hook add-ons are named, in the verdict if the
   route's verdict was not Working.
6. **ReShade routes**: pick the logs by route, not by timestamp (a feed log left
   from an earlier route is ignored with an info finding). Cut each to its
   last session. A stale ReShade log with no feed log is no evidence. Another
   NGX hook file sharing the folder is a warn finding whatever the logs say.
   - Standalone with no ReShade log but a fresh add-on log of its own: `Ran`
     with a finding that ReShade could not write its log, then the standalone
     reader.
   - **No log at all: the "no log" ladder**, in this order, because a missing
     file explains everything downstream:
     1. a Vulkan-layer route whose layer is gone (`missing.layer*`), or a
        proxy file gone (`missing.proxy`);
     2. DXVK files gone;
     3. recorded core files gone, unless a DXVK log proves a run on a layer
        route (then the layer answers are better);
     4. a stale ReShade log, same exception: `loaded.installed-after-run`;
     5. DXVK log and layer route: name clash; layer on the machine's list
        (report it); exe set to run as administrator; else "if anything
        started the game elevated" with whether this tool is elevated;
     6. did the game write anything since the install (below): picks the
        headline between "changed after the install, so it ran" and "not
        started"; with no record, says only "nothing has loaded ReShade";
     7. **the live and remembered process evidence**, which rewrites every
        guess above (and removes them): a live process first, else the last
        sighting after the install, in this order: protected; another exe
        ran; the same DLL name loaded from elsewhere; layer clash; layer route
        with our files in; our add-on in the process but no log
        (`reshade.ran-wrote-no-log`); our files loaded and no log; nothing
        recorded to look for; wrong graphics API; layer route not reached;
        starter; not drawing yet; none of our files loaded;
     8. a launcher exe (the one cause readable off the install itself);
     9. otherwise the three guesses as info findings (other exe, DLL name
        ignored, not Vulkan / not OpenGL) and `loaded.not-started` with
        `NeverRan`.
7. **Logs present**: `Ran`. Then adds findings (loaded add-ons, "did not load"
   when ReShade registered others but not ours, two DLSS add-ons fighting,
   two builds of one add-on, a second DLSS hook, an old bridge, feeder plus
   bridge, ReShade d3d9.dll before DXVK, D3D9 device on a DX11/12 install,
   game exited before a swap chain, add-on proven by its own log when ReShade's
   tail lost the line, shader failures, depth buffer, motion vectors, host
   spawned/connected, device rebuilds, resolutions rebuilt at, a bordered
   window, a minimised window).
8. **Early whole-folder verdicts**: `hook.d3d12-redist`, then the standalone
   reader, the upstream reader (only with `[NRPRE]` in the log; older builds
   fall through to "confirm in the overlay"), the GITC reader (only with
   `Uplift` in the log).
9. **The decisive part**, from the three logs joined, in this order:
   1. the driver-fault stack (`driver.616-64-fault`);
   2. a frame-pacer finding;
   3. a crash/exception record (`crash.*` by whose frame is innermost);
   4. the helper's own fault, only when its log is from the same launch
      (clocks, not file times);
   5. create-feature crash; driver availability; hook failure; stands-down;
      NGX refusal; then frames delivered (with the stop-after-last-frame and
      old-compiler checks) → `working`; `feature ready` → not switched on;
      resource build failure; old compiler; route without a frame log →
      stage 7 with the switch state and the bridge's log; the helper fed
      (`unseen.helper-only`); the feed attached and never got a runtime;
      ReShade died early; else inconclusive.
10. A D3D9-related or helper-mismatch verdict found earlier replaces
    "inconclusive"-type verdicts but never `working` and never a bridge that
    delivered frames.
11. **Post-pass, always**: name a second DLSS hook in the verdict. If the
    verdict is Working, the hook finding drops from bad to warn; if the verdict
    is a "cannot see" one it is replaced by the hook verdict; otherwise the
    hook is appended ("... beside ours too (names), test without it").
12. **Callers** then apply, in this order: the person's answer, Windows' fault
    record, and the closing-fault exemption (below).

Changed from the reference: apply steps 11 and 12 inside the one `Analyse`
entry point (taking the person's answer and the crash record as optional
inputs). The reference had three copies of the crash correction (the game page,
the watcher, the replay) that drifted (see "Bugs").

#### Did the game write anything since the install

A bounded walk (4 000 entries, 400 per folder, 1 s, `model.py:534-540`):

- per-user data under `%LOCALAPPDATA%`, `%LOCALAPPDATA%Low`, `%APPDATA%`,
  `Documents\My Games`, `Saved Games`, in folders named after the exe stem
  (minus `-Win64-Shipping` etc., minus `Client`, `Game`, `_x64`, `64`) and
  after the install folder and up to three parents while they are
  container-named (`Binaries`, `Win64`, `bin`, `x64`, `shipping`, `game`...);
  never a store or launcher folder name;
- the same sub-folders (`Saved/Logs`, `Saved/SaveGames`, `Saved/Config/...`,
  `Logs`, `Config`, `UserData`, ...) under the exe's folder;
- a file counts if modified more than 60 s after the install, and is not one
  of ours (the record's files, logs, `.ini` the tool writes, `.dlss5-*`
  sidecars, `.tmp`, backups) and not code or archive (`.dll .addon64 .addon32
  .fx .fxh .asi .json .7z .zip .pdb .exe`; an exe's time moves with store
  updates).

It is evidence, never proof; the sentence says "something in the game's own
files changed".

### The person's answer

The report asks two questions: **did the game start** (it started and ran / it
started, then closed itself / it never started) and **what happened** (at least
3 words). Both required. A report template line left untouched is no answer.

If the person says it closed itself or never started, a verdict that sends them
to look at something in a running game (the "look in the game" list:
overlay/panel asks, "Working.", add-ons loaded, inconclusive, loaded and set up,
not switched on, needs the game's own upscaler, no D3D12 DLSS call, no neural
frame followed, delivered no frame) is **wrong for them** and is replaced. Every
verdict that names something the logs show stays. Video installs are left as is.

Replacement, in order of what is known:

1. the process was protected (the sighting refused the module list with
   "protected") and the person said never started and Windows recorded no
   fault: `hook.protected-by-anticheat`;
2. an anti-cheat the install found beside the game, same answers:
   `hook.protected-by-anticheat`;
3. otherwise a numbered what-to-take-out-first list: another mod's add-on that
   ran beside ours; what the dlss page swapped (restore original); what this
   install swapped; then "uninstall and start once with nothing of ours in it;
   if it runs, install again with another route";
4. the verdict: ran + closed itself + a Windows fault → `crash.windows-recorded`
   ("it ran, and then the game crashed"); ran + closed itself → "neural
   rendering ran, then it closed itself"; ran + never started → "the logs show
   an earlier session that ran; this time it never started"; a fault but no
   run → "Windows recorded the game faulting"; closed itself → "nothing here
   recorded why"; never started → "never started with this install in".

The findings that told the person to look in the overlay are removed. The
"never ran" findings are removed whenever proof the game ran exists (a
sighting, a fault record).

### The Windows crash record

Windows writes an event when a program faults; for a game that dies before
ReShade loads it is the only record.

- **Read**: the newest Application log event whose provider is `Application
  Error`, `.NET Runtime` or `Application Hang`, within the last 14 days
  (clamped 1 to 60), naming the game's exe in its first two data fields, **since
  the install (minus 60 s)**. The module is the first of the fields 4 to 6
  ending in `.dll .exe .sys .addon64 .addon32`; an event with no module (a hang)
  is skipped, not allowed to hide the crash that explains the evening. The
  code is the 8-hex-digit field, shown `0x...`. Event time is UTC.
  A hard timeout of 8 s; on timeout or any error: no record.
- **Described** as a title and detail, in this priority:
  1. module equals the exe: the crash is in the game's own code. This does not
     clear the install; uninstall and run once to tell;
  2. module equals the install's proxy name and is **not** otherwise ours:
     ambiguous (the name is also a Windows system DLL; the event gives a
     name, not a path): uninstall and run once, report if ours;
  3. module is ours: a name in the record's file list, or one starting
     `reshade`, `dlss5`, `renodx`, `optiscaler`, `nvngx`. Names the project
     whose code it is (DLSS5-Feeder, dlss5-bridge, renodx-dlss5,
     DLSS5-ReShade-AIO, neural-upstream, ReShade, OptiScaler, RTX40MFG-Unlock,
     NVIDIA's runtime; `nvngx.dll` is the standalone route's bridge and
     `nvngx.dll_dlssnr.dll` is OptiScaler's forwarder, neither NVIDIA's);
     "report a bug"; another build or route can be tried;
  4. module is a shared Windows/VC++ runtime (`ucrtbase`, `msvcrt`,
     `vcruntime140`, `vcruntime140_1`, `msvcp140`, `ntdll`, `kernelbase`): the
     event names who was called, not the caller; the last log lines may point at
     the caller;
  5. otherwise: neither the game nor ours, an overlay, a driver component or a
     mod; close overlays (RivaTuner, Discord, GeForce Experience).
- **Belongs to this session** if its time is at or after the session start
  minus 5 s, where the session start is the ReShade log's last write minus the
  span between its first and last stamped lines (the stamps carry no date, so
  the span is taken modulo 24 h; with no stamps, or a start after the last
  write, a fault within 300 s before the log's last write). With no log
  evidence, assume yes. A fault 21 s before ReShade's first line is an
  earlier launch's.
- **A fault in a folder that is not this one** (a module given as a path
  outside the install folder) does not rewrite a "never ran" verdict.
- **The closing-fault exemption** (not counted against the result): on the
  OptiScaler and standalone routes only, if the fault is within 30 s after the
  add-on log's last write (and not more than 1 s before it, Windows gives whole
  seconds), and that log's last four lines show OptiScaler unloading
  (`DLL_PROCESS_DETACH`, `Unloading OptiScaler`) or the game destroying its NGX
  parameters after releasing the feature (`TryDestroyNGXParameters` with a
  `ReleaseFeature` in the last 40 lines), or the standalone add-on's `standalone
  session ending`: the game was on its way out. It is **shown** (an info
  finding, in the report, with the one-line reason) and not counted.
- **Overrides** (a counted fault, this session):
  - verdict `working` → `crash.windows-recorded` ("It ran, and then the game
    crashed - Windows recorded the fault in <module>"). A line says the
    install is right but the session did not end well, and the two-way test:
    OptiScaler: set `Enabled=false` under `[DlssNr]` and start again; any
    other route: uninstall and play the same spot;
  - a "play once and check again" verdict (the logs had nothing yet) →
    "ReShade loaded, and the game crashed before the add-on got going";
  - a `NeverRan` verdict and the fault is in this folder → "It started, and
    nothing here recorded the session - Windows recorded the fault", with a
    line that it WAS started, uninstall, check it starts alone, then install
    again (and try another proxy name, except on Remix where there is none; say
    to report it when no proxy name is offered); `NeverRan` and the "guess"
    findings are removed.
  Any other verdict stands.

### "Try another route"

Offered when the verdict's class is **route failed** (table above) and the game
**ran**: the report says `Ran`, or does not rest on "no log" (`NeverRan`
false), or the watcher saw it run. A game that was never seen running is not
offered a route, so `loaded.installed-after-run` ("play once") must **not** be
a route-failed verdict (the reference classes it stage 3; see "Bugs").

The offer is **the rest of one plan**, kept per game:

1. The plan: the routes the app offers for this game (the install spec's
   detection), starting with the recommended route when it is offered, then
   the rest **minus routes that are never tried automatically** (GITC-Uplift:
   offered only when the person picks it).
2. Ordered by what other people's results say, best first, each with the
   counts that put it there:
   - rank 3: a route that worked **in this game** (confidence = the lower end
     of the 95 % Wilson interval of worked/tried; ties by count);
   - rank 2: a route with at least 5 results in games like this one (the class
     is graphics API plus what the game ships: its own DLSS, FSR 2/3, XeSS, or
     nothing; when games like this have at least 5 results on any offered
     route, one table, the class's, ranks all routes; otherwise every game);
   - rank 1: nothing reported: offered order;
   - rank 0: tried in this game and never worked: last.
   It never adds or drops a route.
3. A sentence for the person ("In this game the X route is reported working by
   3 of 4" or "Nobody has reported this game yet. Across DirectX 12 games with
   their own DLSS, X worked in 15 of 21 tries, against 4 of 12 for Y") only when
   the results are enough, and only when it beats the current route.
4. The route that failed is recorded as tried; "try X" continues the plan
   from X; the next close does not offer a tried route. After at most **three**
   attempts a plan ends ("a fourth try has never rescued a game in the corpus,
   and each costs a download, an install and a launch").

The shared results come from the compatibility list, which is not built until
the app can send results (plan: "the compatibility feed comes back later").
Until it is, ranking is "the offered order" and the sentences are never
produced; the code must work on an empty feed.

One line says why per route ("nothing of the X route loaded in the game"; "the
game refused the X hook"; "the X route did not get the neural pass going"; "the
X add-on crashed"; "the model refused on the X route").

### Cost and frame-rate measurement ("it tells you what it costs")

After a session that ran, on the routes that have a **work area** dial
(OptiScaler always; the feeder only on a 64-bit D3D11 game and not through
DXVK), the tool reads from the same logs what the session cost.

- **Feeder**: the last `N frames: feed CPU X ms/frame ... Y fps` line (the settled
  rate, not the loading screen) gives the frame rate and the frame count. The
  32-bit feed's own spelling (`[feed32] N frames: feed CPU ... (Y fps) ...
  pipe write`) is a frame rate to the helper, not a measure of the neural pass.
- **OptiScaler**: every `DLSS-NR (cost|elapsed): X ms total [=|, Y ms model]`
  line of the last run; the **median** of Y (X when Y is absent) is the model
  cost; the number of readings is shown as readings, not frames. The word before
  the number is the fork author's and changes; ask for a duration beside a
  `DLSS-NR` mention, not for one word. Lines that are failures, setting echoes,
  or say `skip`/`fail`/`refus`/`abort`/`cancel`/`no motion` are not work.
- **The work area** the session ran at is **read from the add-on's own config**
  (`OptiScaler.ini` `WorkingScale` ×100; `dlss5-feed.cfg` `work_resolution`),
  clamped 25 to 100, not from the slider (the slider is what the next install
  would write). If the config was written more than 60 s **after** the log's
  last write, it is for the next session and the session is read as "unknown
  work area". With an unknown work area the number may be shown, marked "the
  work area is the slider's", and is **never published**.
- **History**: one sample per (game, route, work area), newest wins, at most
  eight per game, at most 60 games.
- **The split**: `frame_ms(r) = base + k·r²` with r the work area as a fraction.
  Two sessions at different areas (at least 5 % apart) solve both; `k <= 0.05`
  or `base <= 0` means the sessions were not comparable (another scene, another
  driver): step instead of pretending. Cost table rows for 50, 75, 100 % and the
  session's own, each `k·r²` ms and, when `base` is known, the frame rate
  `1000/(base+k·r²)`. On the OptiScaler route (no frame rate logged) `base` is
  unknown: the model's cost scales as area and only that is shown, with no fps.
- On the feeder the number is everything that grows with the work area: the
  model **and** the feed together. Every sentence that prints it says so.
- **What is published** (the "share the result" record): work area, the
  milliseconds, the frame rate; nothing else about the machine beyond the card
  and driver the record names anyway. The milliseconds are the model's own
  (OptiScaler) or the solved growth (feeder, only when the two sessions are at
  least **15 %** apart; 5 % apart solves to a model costing a fifth of a ms).
  A published number that was guessed is worse than none.

Changed from the reference: fit the two parameters by **least squares over all
the recorded points**, not through the lowest and the highest only
(`autotune.py:333-350` used `points[0]` and `points[-1]`, so a third and fourth
session improved nothing). Identical at two points. Keep the spread and
plausibility rules. "Aim for" is in [watch](watch.md).

### The bug report ("report a bug")

`issue_body` builds one Markdown text. Order (the replay reads it back by these
headings and keys, see Fixtures):

1. the two answers (did the game start; what happened);
2. a `---` line and the key list: `version`, `gpu` (name and compute
   capability) and `driver`, `game`, `exe` (file name only), `arch/api` with the
   reason in brackets, `route`, `anti-cheat` (when found), `work area` (when
   the route has the dial and the config says), `bridge contract: synthetic`
   (when so), `windows event: <exe> faulted in <module> <code> at <time> UTC`
   (only for a counted fault), `starts as administrator` or `compatibility flags`,
   `this tool: running as administrator`;
3. `**Diagnosis**: <verdict>` and one line per finding `- [level] title` (cut at
   1 200 characters), after the person's answer has been applied (a **copy**:
   the window's own diagnosis is not changed behind its back);
4. `**Files in the folder**`: one line per file that decides whether anything
   can load: the proxy (or, for a layer route, "ReShade N-bit Vulkan layer:
   registered (for this user and on the machine's list | for this user only) |
   NOT REGISTERED"), recorded add-ons, `ReShade.ini`, DXVK files with "present
   (written since the install | from before)" for DXVK's logs, `nvngx_dlssnr.dll`
   (in `host64/` on a 32-bit or helper feeder install; under `GITC-Uplift/` on
   that route), the feed's two shaders plus the motion-vector provider (and where
   the person's own copy is, relative, when the install deliberately did not
   write it), the standalone's two shaders, runtime swaps (dlssd, nvngx_dlss,
   the dlss page's record) as present/MISSING, `d3dcompiler_47.dll` present or
   renamed aside, the add-on's switch value, own frame-generation files, the
   Windows graphics setting per exe, OpenXR layer status, and for Remix the
   `.trex` contents and flavour. **"install record: MISSING"** is a line, or a
   folder never installed in looks like one with files missing;
5. `**What ran, and what it loaded**` (`seen at dd MMM HH:mm`): process name,
   `ran from` **relative to the install folder** (or "outside the game folder"),
   DLL list refused with the reason or the module count, graphics DLLs in the
   process, ours loaded, same name loaded elsewhere, ours not loaded (only the
   essential ones: `nvngx_dlssnr.dll`, add-ons, the proxy, DXVK's DLLs on a DXVK
   install), "also written, loaded only if the game asks";
6. log excerpts, each a fenced block under `**<name>**` with its own budget:
   `ReShade.log` 25 lines / 1 500 characters of the last session; the bridge log
   900; `dlss5-feed.log` 1 400; the helper log 900 (14 lines); `OptiScaler.log`
   900; `standalone-dlssnr.log` 900 (headed "last written ..., before this
   install - not this game's" when so); `remix-dxvk.log` 1 200 (only
   neural-pass lines, or "no DLSS-NR line - its last lines");
7. `**Last error**` (900 characters) of an install into this folder, or under a
   heading that says it is not from an install into this folder;
8. the tool's own log, this run only, with launcher-scan noise lines dropped
   unless they name this game or folder, last 15 lines, 900 characters.

**Excerpts are built so a replay reaches the same verdict**: they keep the
deciding lines first and drop the rest by priority, never the oldest first.
Each excerpt has a fixed list of what to keep (the newest line of each kind the
diagnosis reads: the `EvaluateFeature hooked`/`Failed to find` pair, the
`Direct3DCreate9`/`CreateSwapChain` pair, `Registered add-on` lines, the newest
`[NRPRE]` line of each kind, the standalone's `contract ready` / `on-present
frame` / `FAILED at`, OptiScaler's forwarder/failure/work/dump/present lines,
the bridge's first and last frame, stops, crash block and last three
frame-rate lines, and so on). A log with none of the wanted lines is still a
log: show its last lines, never "(none)", which reads as no log at all.
Hook addresses (`with 0x.. => 0x..`) are removed from ReShade lines.

Changed from the reference: the **keep-list is derived from the verdict
catalogue** (each rule that reads a log line registers it), not a parallel
hand list in the report code. A line the diagnosis reads but the report drops
made replays disagree with the machine (the reference reproduces the printed
verdict for 50 of 143 reports, see Fixtures), and the C# design must make that
impossible by construction.

Changed from the reference: the report is built from **sections with budgets,
whole sections or whole lines**; the reference cut the finished text at 6 000
characters (`body.py:1378`), mid-fence, so the last block lost its closing
fence and the tool's own log block could be dropped without notice.

#### Privacy: what the report may and must not include

It is posted in public. Nothing is sent by the tool: the person sees it in the
browser and decides.

**May include**: tool version; GPU name, compute capability, driver; game name
and exe **file name**; architecture, API and why; route and build; anti-cheat
name and the file names that evidenced it; the verdict and findings; the
install's file list as present/missing names (relative); what the process loaded
(file names, counts); the faulting exe, module name, code and UTC time; log
excerpts; the tool's log lines about this game.

**Must not include**: the Windows account name, the machine name, the Windows
user profile path or any absolute path under it; absolute paths of any kind
that were not made relative (the reference leaked them, see "Bugs"); the game
library's absolute path when it contains a name (use `<game>`); network share
host names; Steam account ids found in `userdata\<id>`; the clipboard or any
secret.

**Rule**: one redaction pass over the **finished text**, before it is saved,
copied, linked or zipped:
1. the install folder's absolute path, in either slash style, case-insensitive,
   becomes `<game>`;
2. the user profile directory, the account name as a path segment of any
   `\Users\<name>\` (also `/Users/`), the machine name, and the account name
   itself anywhere in the text (case-insensitive, whole word) become `<user>` /
   `<pc>`;
3. UNC prefixes `\\host\share` become `\\<host>\share`;
4. what survives of a path keeps its file name so the module names stay
   readable.
The replayer (below) must accept redacted text as input.

**"save zip"** attaches the raw logs to the report: it runs the same pass over
every text log in it, or warns that the logs are unredacted. (Changed from the
reference, which put the raw tails in a zip meant to be attached to a public
issue: `reportout.py:68-106`.)

**Delivery** (`reportout.py`): save the whole report to a file first (name
`report-<slug of the game>-<yyyyMMdd-HHmmss>.md`, slug = ASCII alphanumerics
joined with `-`, 40 characters, "game" when empty), then put it on the clipboard
and **read it back** to know whether it took, then open the browser. The link
carries the longest start of the report that fits. The window says one plain
line of what happened:

| Opened | Cut | Clipboard took it | Line |
| --- | --- | --- | --- |
| yes | no | any | "the report is open on GitHub with everything in it - read it, then submit" |
| yes | yes | yes | "report open on GitHub with its start - the whole report is on your clipboard: click the box, Ctrl+A, Ctrl+V" |
| yes | yes | no | "GitHub has only the start of the report and the clipboard did not take it - use save zip and attach it to the issue" |
| no | any | yes | "no browser opened - the report is on your clipboard, paste it into a new issue on GitHub" |
| no | any | no | "no browser opened and the clipboard did not take it - use save zip and attach it to a new issue on GitHub" |

The link: `<repo>/issues/new?title=<quoted title cut to 120 characters, whitespace
collapsed>&body=<quoted body>`, at most 7 800 characters in all. Over that, the
body is the instruction (paste first when the clipboard has it, else "attach the
zip") followed by the **longest whole-line start** that fits (binary search over
lines, then over characters when even the first line does not fit, never the
instruction alone). A title in a script that costs nine characters a letter once
quoted must not eat the room.

`save zip` holds `report.md` and, as present, the end (4 MiB) of: `ReShade.log`,
`dlss5-feed.log`, `dlss5-bridge.log`, the OptiScaler log, `dlss5-feed-host.log`,
`standalone-dlssnr.log`, and the tool's own log. Nothing leaves the machine
until the person attaches it.

**"share the result"** (the compatibility list) builds a smaller record: the
game's name (cut at 80) and exe (lowercase), route and build, the game's own API
(not the record's: a DXVK install records Vulkan), what the game ships, the card
(name cut at 40, `sm_NN`) and driver, the tool version, `worked`/`failed`, the
one-line verdict, the measurement above, and who said it (`person` when the
logs could not see the outcome and the person answered; `crash` when Windows
recorded the fault). No paths, no user name. When the outcome is `unknown` the
person is asked "did the DLSS 5 picture show in the game?"; a counted fault is
a failure without asking. A record the person did not answer and whose verdict
is unknown, or is `not-a-route`, is not counted. The community feed and its
page are out of scope until the app can send results.

## What must never happen

- **A verdict that rests on absence of a log saying the game never ran when
  there is proof it did**: a sighting, a Windows fault record this session, a
  DXVK log. Every "never ran" finding is removed when proof arrives.
- **Dating anything "since the install" when there is no install record.**
- **Reading another game's log as this one's**: the standalone log is one file
  for every game; the bridge log only when its clock ties it to the ReShade
  session (the bridge's `attached.` line within 5 s before to 120 s after
  ReShade's `Registered add-on "DLSS 5 Bridge"` line, past midnight handled); the
  helper log only within 10 s before to 180 s after the feed's `host spawned`.
  Logs carry clock times with **no date**: tie by clocks, not by file times (the
  feed log keeps growing while the person plays on).
- **Reading a previous run's crash as this one's** (the bridge reprints it).
- **Treating "unreadable" as "empty" or "none"**: a protected process refuses
  its module list; that is "unknown", never "nothing of ours is loaded". A
  config that could not be read is not a number.
- **Guessing antivirus** when a missing file may be our own failed install, and
  blaming the person for the tool's own mistake ("not started since an install
  that never happened here").
- **A path in a posted report** (privacy above), and a **truncation that cuts a
  fence or drops a section silently**.
- **Telling a person whose game closed or never started to look in a running
  game's overlay.**
- **A diagnosis that hangs**: Windows queries (event log, process list) have a
  hard timeout and failing one yields "no evidence", never an error shown as a
  verdict.
- **A crash in the diagnosis turned into a wrong answer**: an exception while
  reading a log is a finding the person sees and a bug, not "inconclusive".
- **Running a Windows tool by bare name**: use the absolute system path
  (plan decision), or the API.
- **Offering another route for a verdict about how the game was started or
  set up**, or for a game never seen running.
- **A Windows path parsed with the host's rules.** Paths in logs, records and
  sightings are Windows paths on every host; the reference's `os.path.basename`
  and `Path.name` returned the whole string for `C:\x\y.dll` off Windows. See
  Fixtures: 28 of 184 baseline answers move for that reason on macOS.

## Bugs and questionable choices in the reference not to port

- **Verdicts classed by their printed words** (`verdicts.py:24-270`,
  `:343-350`). About 20 verdicts the tool can print are **unmapped**: they have
  no stage, no actor, no outcome, and never offer a route. Found by running the
  reference's `stage()` over printed sentences: the uninstall-incomplete, the
  32-bit layer name clash (both spellings), the standalone add-on's four
  (own log has nothing yet, set up no frame yet, pipeline failed, missing a
  runtime file, no NGX core in the driver), `DLSS never started.`, both
  old-`d3dcompiler_47` verdicts, `driver has no DLSS 5 entry point` (the second
  wording), `The crash is in the game's own code` (classified by a different
  fragment), upstream "switched off in the tab", "neural rendering ran, then the
  add-on stopped at a device re-creation", "neural rendering never ran - the
  game's own upscaler has to be on", "the game is on Vulkan; the optiscaler
  route needs a Direct3D renderer", the swapped Remix runtime, OptiScaler's log
  is off, the protected-process verdicts. The corpus passes only because the
  baseline holds none of them. Use ids and make the catalogue exhaustive
  (a test: every id has a class).
- **The same mapping is used to decide "another route could help"**, so an
  unmapped verdict silently never offers one. Same fix.
- **`The install never finished` and the stage-1 verdicts count as `failed`
  outcomes** (`verdicts.py:410-427`): a full disk would blame the route in a
  shared result (see Changed above).
- **`Installed after the last run - play once` is stage 3 and route-failed**
  (`verdicts.py:63-69`): with the watcher's "it ran" default (`seen=True`,
  `ctl_watch.py:ran`) a game the person has just launched and closed in 20 s
  gets "try another route" over logs from before the install. It is
  `NeverRan`.
- **Three copies of the Windows-fault correction** (`ctl_game.py:2660`,
  `ctl_watch.py:_on_lookdiag`, `_tools/replay_report.py:analyse`) with
  different wording and conditions; one function.
- **The correction lived on the window's side** of `analyse`; the report
  printed `Working.` beside a fault record for months (#294 #295).
- **`other_ngx_hooks` named the helper's own add-on** `host64\renodx-dlss5.addon64`
  as "another DLSS hook shares this folder": 27 of the 28 report disagreements
  at the reference are this one extra warn finding. The helper process's files
  (`host64\*`) are ours and are not evidence about the game process
  (`watch.py:382`). Probably a path-parsing artefact off Windows (see
  Fixtures); unverified.
- **Free-text parsing of a Python traceback** to decide "the install crashed"
  (`evidence.py:1483-1511`, `_CRASH_CAUSES` by substrings of a Python exception
  name such as `getaddrinfo`, `gaierror`, `urlerror`; `model.py:390-436`
  reads bytecode to learn which modules the install reaches). In C# the install
  returns a **typed failure** (kind, cause, host) that is stored in the record
  and read by the diagnosis; no traceback text is parsed.
- **`_RESHADE_KEEP`, `_FEED_KINDS`, `_opti_kind`... are a second hand-written
  copy of the rules**, which is why replays differ from the machine.
- **The mod-by-mod `if`/`elif` chain is 236 branches in one function with order
  as the only structure** (the corpus header says so): make it a list of rule
  objects with a name, a precondition and an order, so a test can ask "which
  rule fired".
- **`rep.verdict = ...` assignments in 40+ places** with `return rep` after
  some and fall through after others. A rule returns a result or "no opinion".
- **Verdict text with the date/time inside** ("ran at 05 Oct 22:50", from a
  local `strftime`): the same report then differs by locale and day. Times go in
  the finding, in ISO, and the verdict says "when the tool looked".
- **`LogTime` and the sightings in local time, formatted `%d %b %H:%M`** with an
  English month: localisable text, not a key.
- **Clock-of-day arithmetic for a session start** (`ctl_game.py:150-190`): a
  16-hour session or a log with one stamp breaks it. Keep the rule above but
  prefer the process's own start time when the watcher saw the process.
- **Hard-coded dates in rules** (`routes.py:132`: a release window on
  2026-09-20, a display-filter release) are data about one incident; keep as a
  recorded finding, not a branch.
- **PowerShell for the event log** (`wincrash.py:87-114`): a process per query,
  an `~`/`|` text protocol that breaks on a module name containing either, and
  "data fields 4 to 6" positional guesses. Use the .NET event-log reader with a
  query on provider and time, read the event's named data (application,
  faulting module, exception code), under a timeout. (Changed from the
  reference.)
- **`windows_crash` run on every close and every press**: cache the answer per
  (exe, install time) for the session.
- **`print`-style reading of `LOCALAPPDATA` at import** for the standalone log
  path (`model.py:212-217`): resolve at call time (tests need it injected).

## To verify against primary sources

1. **The strings every log parser matches** are other projects' output and
   change between their releases. Each is read from a binary's format strings
   or from real logs; the catalogue above is only as true as that. Source:
   each project's own repository and release binaries. The ones that rest on a
   single report or a read of a binary are flagged here: renodx-dlss5 and
   DLSS5-Feeder log lines (`CRASH RECORDED`, `stack, by module`, `stopped:`,
   `feature ready`, `building:`), the bridge's format strings
   (`dlss5-bridge` 1.4.11 and 1.4.12), standalone-dlssnr 1.7.x, wilsjo2's
   OptiScaler 0.8.4 `DLSS-NR` lines, Kim2091 / lunks' Remix runtime
   `[DLSS-NR]` lines, GITC-Uplift 1.7.0 `[Uplift]` lines, neural-upstream
   `[NRPRE]` lines. Fixture work item: a **golden log per line kind**.
2. **ReShade's log format**: `Initializing crosire's ReShade`, `Registered
   add-on "<name>" v<ver>`, `loaded from '<path>' into`, `vtable::Hook(...)`,
   `Failed to compile`, the `\| INFO \|` voice format with `[name]`, and the
   clock format `hh:mm:ss:mmm [pid]`. Source: ReShade's `log.cpp` and add-on
   API.
3. **Application Error event layout**: event ids 1000 (Application Error),
   1002 (Application Hang), 1026 (.NET Runtime); field order for the faulting
   application, faulting module and exception code; the exception code is bare
   hex; the `TimeCreated` is UTC. Source: Windows event schema (Microsoft
   docs), tested on a real fault.
4. **Which crash events carry a module**: that a hang carries none.
5. **`RUNASADMIN` in `AppCompatFlags\Layers`**, the key's per-user and
   machine views, and that an elevated process does not read `HKCU` Vulkan
   implicit layers (the Vulkan loader skips `HKEY_CURRENT_USER` for elevated
   processes; the machine's list `HKLM` it does read). Source: Vulkan loader
   documentation (implicit layer discovery on Windows), Khronos.
6. **Two ReShade layer manifests sharing the name `VK_LAYER_reshade`** and the
   loader keeping a name once. Source: Vulkan loader docs; ReShade installer.
7. **`ERROR_ACCESS_DENIED` for a protected process, `ERROR_PARTIAL_COPY` for
   a process still starting**, and that `EnumProcessModulesEx` with
   `LIST_MODULES_ALL` lists a 32-bit process's modules from a 64-bit tool.
   Source: Microsoft docs for those functions.
8. **0x887A0005 / 6 / 7** as device removed / hung / reset (DXGI_ERROR codes)
   and **0x887E0003** as `D3D12_ERROR_INVALID_REDIST`. Source: `dxgi` and
   `d3d12` headers/docs.
9. **Driver 616.56 is the first with the neural-rendering runtime and 616.64
   changes the NGX path** (a statement by NVIDIA and by the feeder's author,
   DLSS5-Feeder #54). Source: NVIDIA release notes; the feeder issue.
10. **The startup module count for "a starter"** (45) and the one-reading
    limit: derived from two corpus cases. Measure on real games.
11. **GitHub's new-issue link length** (7 800 characters in the reference):
    GitHub does not publish a limit; the reference chose it from failures.
    Measure, and say where the value comes from.
12. **Event time zones and DST**: the reference's earlier bug (an hour out all
    winter) shows the conversion needs a test across DST.
13. **That Windows clipboard text read-back is a faithful check** that the
    paste will work in a browser (a second-order claim).
14. **Whether `OptiScaler.ini` `[Log] LogToFile` and `[DlssNr] Enabled` and
    `WorkingScale`** are the keys the shipped builds read; and the same for
    `dlss5-feed.cfg work_resolution`. Source: those projects' docs/sources.

## Fixtures

The corpus is `_tools/reports/*.txt` (184 saved GitHub issue bodies, the tool's
own log block removed and Windows account names replaced with `<user>` by hand,
which is itself evidence that reports leaked them), with
`_tools/verdict_baseline.json` (the answer for each, `route`, `ran`, `never_ran`,
`verdict`, `findings`), replayed by `_tools/replay_report.py` and compared by
`_tools/verdict_check.py`. Numbers measured on macOS with Python 3.12 while
writing this spec: 184 reports (143 print a verdict the machine made), 0
crashes, route counts feeder 71, optiscaler 44, upstream 13, bridge 12,
standalone 11, renodx 6, remix 3, unknown 17; baseline verdicts by stage:
nothing of ours loaded 44, cannot see 41, install stopped 39, add-on crashed
16, set up not switched on 13, feed never got going 11, driver's runtime 7,
working 5, refused the hook 5, second hook 2, missing file 1; outcomes
failed 148, unknown 31, worked 5.

### How a report is turned into a replayable folder (what the replay does)

- **Blocks**: every fenced block under `**ReShade.log**`, `**dlss5-feed.log**`,
  `**OptiScaler.log**`, `**standalone-dlssnr.log**`, `**remix-dxvk.log**`,
  `**dlss5-feed-host.log**`, `**dlss5-bridge.log**` (ignored when `(none)` or
  empty).
- **Header keys** read: `version`, `gpu` (and the driver inside it), `game`,
  `exe`, `arch/api` (API by regex DX8..DX12 / Vulkan / OpenGL, 32-bit),
  `route`, `work area`, `bridge contract`, `starts as administrator`,
  `compatibility flags`, `this tool`.
- **The folder is rebuilt as the report describes it**, not as a perfect install
  would leave it: the `Files in the folder` list says which files exist, and
  "install record: MISSING" removes the record (without that the biggest class
  in the corpus, "nothing we wrote ever loaded", cannot reproduce: #43, #194).
  A report without the list gets everything in place.
- **Whether the install finished** is not in a report: it is inferred from the
  printed verdict (in the four install-stopped verdicts and the network ones =
  unfinished), else from a `**Last error**` block naming the installer.
- **Machine facts come from the report**: the driver from the header, the
  Vulkan-layer registration from the file list, the standalone log from its
  block, the watcher's record from the `What ran, and what it loaded` block (the
  sighting is dated two minutes after the record), elevation from the header,
  the DXVK logs from the list. Never the replaying PC.
- The person's answer, the printed `windows event:` line, the anti-cheat line
  (or the exe's name matched against known anti-cheat exes) and the refused
  DLL-list reason are applied as the report body applies them.

### The language-neutral fixture

Two layers, both data, both checked in:

1. **Cases** `fixtures/diagnosis/cases/<id>.json`, one per report, the
   **inputs the diagnosis takes** and the expected result. This is what a C#
   test reads; no Markdown parsing at test time.
   ```
   { "id": 63, "source": "report-063.txt",
     "route": "feeder", "exe": "DetroitBecomeHuman.exe",
     "api": "Vulkan", "bitness": 64,
     "record": { ...fields from "What the diagnosis reads..."...,
                 "complete": true, "files": [..] } | null,
     "folder": { "present": ["dlss5-feed.addon64", ...], "absent": [..],
                 "dxvkLogsFresh": [..], "gameWroteSinceInstall": false },
     "logs": { "reshade": "...", "feed": "...", "host": null, "opti": null,
               "standalone": null, "remix": null, "bridge": null },
     "machine": { "driver": "616.86", "layerRegistered": true,
                  "tooElevated": false, "exeFlags": "" },
     "sighting": { ... } | null,
     "answers": { "started": "it started and ran", "event": null,
                  "anticheat": "", "protected": "" },
     "lastInstallError": "",
     "expected": { "verdictId": "crash.feed-recorded", "stage": 8, "ran": true,
                   "neverRan": false, "route": "feeder",
                   "findings": [ {"level":"bad","id":"..."} ] } }
   ```
   File times are **relative**: the install time, the log write times and the
   sighting time are given as offsets to the install time, so the case means the
   same on any day.
2. **The reports themselves** `fixtures/diagnosis/reports/*.md`, unchanged
   apart from the redaction, plus **the parser** (`ReportReader`, in the test
   or tool project) that makes a case from a report. Every case is
   regenerated from its report by a test (`case == Parse(report)`), so adding a
   report is: drop the file, run the generator, review the expected id.

**The expected verdict is an id, not a sentence.** One-off migration: each
baseline sentence is mapped to its id by the catalogue; a sentence that maps to
no id is a hole in the catalogue and is reported, not defaulted. Findings are
compared by id and level (the bad/warn/ok/info and the rule that fired), not by
text.

### The 28 reports that already disagree with the baseline

Running the reference's `verdict_check.py` at the reference (macOS, Python
3.12): **28 of 184** answers differ from `verdict_baseline.json`:

- **27 differ by one extra finding only**, the same one: `warn: Another DLSS
  hook shares this folder: host64\renodx-dlss5.addon64` (reports 11, 44, 82, 95,
  101, 145, 156, 164, 190, 215, 238, 252, 322, 348, 377, 400, 403, 467, 479, 480,
  482, 511, 562, 669, 739, 917, 987). The verdict, `ran` and `never_ran` do not
  move.
- **1 differs in the verdict**: #328. Baseline: "The game loads d3d9.dll from
  another folder - ours is never reached." Now: "mm.exe ran at 19 Sep 18:22,
  and ReShade's Vulkan layer did not reach it - nothing of this install was
  loaded." The "same name loaded from elsewhere" evidence stopped winning.
- The reference's own reproduction measure (`--reproduce`: the replay against
  the verdict the reporter's machine printed) is **50 of 143** (floor 31). 93
  reports replay to another answer than the machine gave; some of those are
  rules that were wrong then and are fixed now, the rest is a report that does
  not carry the deciding line.

Probable cause of all 28 (not verified; run the reference on Windows to
settle): the replay runs on a Mac, where Python's `os.path.basename` and
`Path.name` do not split on `\`, so `host64\renodx-dlss5.addon64` is one file
name and a path filter that works on Windows lets it through, and a loaded
`d3d9.dll` path compares unequal to ours. The baseline was recorded on
Windows. This is the argument for the "never parse a Windows path with host
rules" invariant, and the reason the C# test suite must run on macOS and
Linux: the 28 are the first cross-platform regression test.

**How they enter the fixture**: for the 27, the expected result has **no**
`Another DLSS hook` finding (the baseline; `host64\` files are ours). For #328
the expected verdict is decided by hand from the report: **open question 1**.
Do not copy the current drifted answers as truths; the stop rule applies.

### Other fixtures to build

- **A golden log per line kind** for every parser in the table: real excerpts
  (from the corpus) with the id of the rule each must fire, one test per rule
  (rule name -> fixture), plus one negative per rule (the line that looks the
  same and is not: the `NR diagnostic` and `falling back` OptiScaler lines are
  not failures; `DlssNr.DispatchInterval: 16 ms` is not work; one failed Present
  after the model kept drawing is not a refusal; the feed's NGX block is not the
  feed's).
- **The ordering tests**: pairs of cases where moving a rule changes the answer
  (the corpus itself, plus the named incidents in the reference's comments:
  #43, #194, #171, #182, #213, #232, #238, #250, #252, #289, #311, #348, #364, #390,
  #400, #412, #420, #434, #458, #479, #577, #637, #682, #740, #744, #748, #750,
  #787, #862, #944, #975, #987, #992, #1014, #1035, #1047). Many are in the
  corpus; the rest are one case each.
- **Windows crash**: fixtures of `Application Error` / hang / .NET event
  XML (real ones from the corpus's `windows event:` lines and a redacted
  export), `describe` per module class, the DST boundary, the session
  attribution matrix (fault before ReShade's first line, within it, after the
  log's last write, with and without stamps), the closing-fault matrix.
- **Report building**: golden reports for an OptiScaler case, a standalone, a
  bridge and a 32-bit feeder case with a redaction check (a profile path, the
  account name as a word, a UNC host, a `userdata\<id>`), the URL cut (a body
  just under, just over, one whose first line alone is too long, a title in
  Cyrillic), the `line_for` table, and a check that no absolute path survives.
- **Cost**: the two log shapes (`feed CPU` line with decimal comma; OptiScaler
  `cost` and `elapsed` with and without `model`); sessions at two and three
  areas for the fit; a config newer than its log; the unknown-area rules.
- **The folder walk** (`game wrote since the install`) with a fake file system
  and a clock: user-data folders, a container climb that stops at the game's
  root, the entry and time caps.

## Open questions

1. **#328**: which is the right verdict, the baseline's "loads d3d9.dll from
   another folder" or the replay's "layer did not reach it"? Decide from the
   report (a DXVK Vulkan-layer install where d3d9.dll came from elsewhere):
   Ale or the orchestrator reads the report.
2. **Run the reference on Windows** to confirm the 28 are path-parsing
   artefacts and not drift. Needs a Windows machine (or the work PC).
3. **Verdict granularity**: about 150 ids above. Merge the families that
   differ only in what they name (the `crash.*` helper ones, the `process.*`
   ones), or keep them? Ids are cheap, text variants are not; the proposal
   keeps one id per distinct next step.
4. **The classes of the currently unmapped verdicts** are proposed in the
   catalogue by their evidence; confirm (in particular `crash.in-game-code`,
   proposed route-failed no, and `uplift.*`).
5. **Should "install stopped"/"file missing" be `not-a-result`** (proposed) or
   keep failing the route as the reference did?
6. **Redaction strength**: replace the install folder with `<game>`
   (proposed) or only the user and machine names (keeps the library path,
   which helps triage)? Should the zip be redacted or only warned?
7. **How many sightings feed the diagnosis**: the reference trusts one reading
   8 s after the start. The watcher spec proposes more; the diagnosis then
   needs a rule for conflicting readings (see [watch](watch.md)).
8. **Is `Working` allowed for a route that logs frames but where "the picture
   changed" is in no log?** The reference says Working for the feeder on frames
   delivered, but the "bordered window" case (frames delivered, nothing on
   screen) is a warn under a `Working.` verdict; should that be stage 7?
9. **A localisable verdict**: the text is English in the reference. The product
   language is English until said otherwise (the rewrite plan says nothing);
   confirm the verdict text lives in a resource file keyed by id.
10. **The "report a bug" two questions**: keep exactly two required answers?
    (They were added because four of nine first-day reports arrived blank.)
11. **Anti-cheat answer wording**: it says "no route gets past it" and "do not
    use this in an online game". Is that the tone Ale wants in a published
    tool?
12. **Where the repository for reports points**: `<repo>/issues/new`; the
    repository name moves with the rewrite.
