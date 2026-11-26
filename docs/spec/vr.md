# VR through ReShade's OpenXR layer

A VR game on OpenXR draws the headset's two eye images through the OpenXR
runtime; the window on the desktop is a mirror, drawn after the fact. A proxy
DLL (`dxgi.dll`, `d3d11.dll`) or the Vulkan layer hooks the game's own device,
which draws the mirror: the pass runs, the desktop window changes and the
headset sees nothing (issue #33). ReShade 6 also ships an **OpenXR API layer**
(`ReShade64_XR.json`, loading the same `ReShade64.dll`) that hooks the OpenXR
swapchain, the picture the eyes get. This spec covers registering that layer
for the user, what the **vr** page shows, and what is **unproven**: nobody
working on the tool owns a headset, so whether the feeder, the bridge and the
DLSS 5 add-on behave inside an OpenXR swapchain has never been seen. Read from
`core/openxr.py`, `core/ui/vrpage.py`, `docs/vr.md`, the install and uninstall
paths in `core/installer.py` (`2429-2447`, `4462-4486`, `5789-5805`), `core/prefs.py:293-309`,
`core/diagnose/body.py:379-386` and the README's "VR". Games on
OpenVR/SteamVR are not reached by this layer at all; if a game only offers
SteamVR, nothing here applies.

## What the user gets

### The option on a game

- A checkbox **VR headset (OpenXR layer)** in the game's settings, shown only
  for **ReShade routes on a 64-bit game** (`ctl_game.py:954`). It is off by
  default and does not exist on OptiScaler or Remix, where it would do nothing;
  the install record's `vr` flag is false for those routes whatever was asked
  (`installer.py:3188`). The command line has `--vr` (see the CLI spec).
- On a 32-bit game the request is not an error: nothing is registered, with the
  warning "VR: the OpenXR layer is 64-bit only - nothing is registered for this
  32-bit game" (and "skipped for this 32-bit game" in the log).

### What an install with the box ticked does, on top of the route's files

1. Downloads ReShade (already needed by the route) and **extracts**
   `ReShade64.dll` and `ReShade64_XR.json` from the ReShade setup into the
   layer folder `%LOCALAPPDATA%\dlss5-autopilot\reshade-vulkan\`, the folder
   that also keeps the Vulkan layer's files (`openxr.py:35-38`, `vulkan.py:61-64`).
2. Rewrites the manifest's `api_layer.library_path` to `.\ReShade64.dll`, so the
   manifest finds the DLL beside it.
3. Registers the manifest **for the user**:

   ```
   HKCU\Software\Khronos\OpenXR\1\ApiLayers\Implicit
       <full path of ReShade64_XR.json>  =  (REG_DWORD) 0
   ```

   Value `0` is "enabled" for the OpenXR loader. The layer's name is
   `XR_APILAYER_reshade`.
4. Notes the registration in the install record (`vr: true` and the
   registration among its registrations). [install](install.md) owns the
   registration and derives its dependents from the install records, so the
   last uninstall is the one that removes it; no list is kept in settings.

Decision table at install time (`openxr.py:79-112`):

| An active registration exists... | Outcome |
| --- | --- |
| none | files extracted, manifest registered |
| a ReShade registration that is **not ours** (its manifest's folder is not our layer folder; for example ReShade's own installer's) | reused untouched, never rewritten; log "ReShade's OpenXR layer is already registered (<path>); reuse it"; note "reused the existing ReShade OpenXR layer (<path>)" |
| **ours** | files refreshed, the registration stands |

"Registration" means a value whose file name contains `reshade` and ends in
`.json`, under the key above in HKCU or HKLM, with value 0 and the file present;
a disabled value (non-zero) or a missing file does not count.

- **Preview ("what will happen?")** names it among the changes **outside the
  game folder**: either "reuses the existing ReShade OpenXR layer (<path>)" or
  "OpenXR layer (VR): XR_APILAYER_reshade registered for this user (files in
  <folder>) - it loads into EVERY OpenXR application until the last VR install is
  removed", plus the warning "VR through OpenXR has not been tried with a
  headset by the author; OpenXR games only".
- **After install** the user gets a note ("registered ReShade as an OpenXR layer
  for this user, so the pass reaches the headset's image; it loads into EVERY
  OpenXR application until 'uninstall' removes it") and the warning that it is
  unproven and OpenXR-only: "if the headset shows nothing new, or the game
  refuses to start, untick 'VR headset' and install again - and say what
  happened in a report".
- **The registration is per user, not per game, and global**: once present,
  ReShade loads into every OpenXR application the user starts, not only the game
  installed from. This is the one thing about the option the user must be told
  before agreeing.
- **Errors.** `ReShade64.dll` held open by a running Vulkan or OpenXR program
  (the DLL is shared with the Vulkan layer): "ReShade64.dll is in use by a
  running Vulkan or OpenXR program - close it and install again (<error>)"; the
  usual cause is SteamVR or the headset's own software. A registry failure:
  "Could not register the OpenXR layer: <error>". Both stop the install.

### Uninstall

When a game whose record lists the OpenXR registration is uninstalled,
install counts the other records that list it ([install](install.md),
"Shared registrations"). If none is left and the registration is **ours**, its
value is deleted ("removed: our ReShade OpenXR layer registration (no VR games
left)"); if others remain: "kept the OpenXR layer: N other VR install(s) still
use it"; if a record could not be read, the registration is kept and the log
says why. A registration that is not ours is never touched. The layer
files stay in the layer folder (the Vulkan layer may share the DLL).

### The vr page

Hidden until M8: nothing on it is proven in a headset, and the VR box on a
game's settings does the work. When it comes, it has three blocks and a note
(`vrpage.py`):

- **The OpenXR layer** card, read once per visit and after anything that
  changes it, never on every redraw:

  | State | Shown |
  | --- | --- |
  | none | "not registered": "no OpenXR layer is registered for this user. Open a 64-bit game's page, tick 'VR headset (OpenXR layer)' in its settings and install: the layer is registered then." |
  | ours | "registered", with the manifest path; and a **remove the layer** button |
  | not ours | "registered by ReShade itself": "<path> - not a file this tool wrote, so it is reused and never changed." No button. |

  **Remove the layer** asks first ("VR games stop loading ReShade, and a DLSS 5
  install in a game folder is left as it is"); with no registration it says there
  is nothing to take out; with one that is not ours it says "The registered layer
  is not one this tool wrote ... It came from ReShade's own installer, so it is
  left alone - remove it from there." Result: "the OpenXR layer registration was
  removed" / "not found".
- **Games that carry OpenXR's loader**: every library game with an install
  folder is searched (first match only) for `openxr_loader.dll` or
  `openxr_loader64.dll`, anywhere under it, on a worker thread with a total
  budget of 20 s. Hits are listed by name with the file found, and each links to
  the game's page, where the box is. Nothing found: "none of the games in the
  library ships openxr_loader.dll. That is not proof a game has no VR mode - some
  load the runtime from the system - but it is the only evidence read from disk,
  so nothing here is guessed." Shown as "reading the library..." meanwhile.
- **What is proven**: "nothing, in a headset": "The layer registers and the files
  land where they should - that much runs here. Whether the feeder, the bridge
  and the DLSS 5 add-on behave inside an OpenXR swapchain has never been tried on
  a headset by the author: there is none on this machine. If you have one,
  install with the VR box ticked and share the result - it is the one thing that
  would settle this, and it lands in the shared results like every other report."
- Two links: ReShade's own notes on VR
  (`https://reshade.me/forum/general-discussion/6451-reshade-in-vr`) and this
  repository's VR page.

### What is proven and what is not

| Claim | Status |
| --- | --- |
| the layer registers, the files land, uninstall removes the registration | runs on the test machine, in the suite and by hand |
| the feeder, the bridge and the DLSS 5 add-on inside an OpenXR swapchain | **never tried in a headset** |
| SteamVR-only (OpenVR) titles | out of reach of this layer |

A report from somebody with a headset (install with the box ticked, play, then
"did it work?" or share the result) is the only thing that can turn the second row
into an answer. The shared result carries no field for VR (see the community
spec); a headset report is recognised only by the free text and the route.

### Diagnosis

The report body adds the line `ReShade OpenXR layer (VR): registered` or
`NOT REGISTERED - install again` when the install record says `vr` (`body.py:379-386`).

## What must never happen

- A registration this tool did not write being modified, replaced or removed.
- Our registration removed while another VR install still relies on it, or left
  behind after the last one is gone.
- The registration written in any hive but the current user's.
- An install that ticks VR on a 32-bit game or on OptiScaler or Remix writing
  anything to the registry.
- The user not being told, before the install, that the layer loads into every
  OpenXR application.
- Claiming VR works. No text anywhere says it does; "unproven" stays until a
  headset report exists.

## Bugs and questionable choices in the reference not to port

- `prefs.py:293-309`, `installer.py:4465,5789-5805`: the count of users of the
  registration is a list in the settings file, outside the install records. A
  lost settings file, a game folder deleted by hand, an uninstall from a record
  alone, or a failed install (the entry is added before the install is known to
  have finished) leave it wrong: the registration lingers, or is removed while
  another VR game needs it. Changed from the reference: there is no list; the
  dependents are the install records that list the registration, read by
  [install](install.md), so the plan's "uninstall from the record alone" holds.
- `openxr.py:79-86`: a *disabled* registration of our own manifest (value
  non-zero, perhaps disabled by the user on purpose) is invisible to
  `existing_registration`, so an install writes the value again as 0 and
  re-enables it without saying so. Say so, or leave a disabled registration
  alone and warn that VR is off.
- `openxr.py:107-111`: failing to rewrite the manifest's `library_path` is
  swallowed (`pass`); the layer is then registered with a manifest that may not
  find the DLL. Report it and write atomically.
- `openxr.py:39-60`: the keys are read in the process's registry view. A 64-bit
  OpenXR application reads the 64-bit view; read and write that view explicitly.
- `openxr.py:93-100`: the shared `ReShade64.dll` is overwritten by whichever of
  the Vulkan layer and the OpenXR layer installs last, so one layer's ReShade
  version can change under the other.
- `vrpage.py:93`: `rglob` walks the whole game tree for each name, twice per
  game, on a 20 s budget; the partial result after the budget is shown as if
  complete. Show "searched N of M games" when the budget runs out.
- The same layer is not offered on the Vulkan-only DXVK path: not a defect, but
  the page does not say so.
- The `reshade`-in-the-name test (`openxr.py:56-58`) treats any manifest with
  that word as ReShade's, including another tool's.

## To verify against primary sources

| Claim | Source that settles it |
| --- | --- |
| the key `HKCU\Software\Khronos\OpenXR\1\ApiLayers\Implicit`, a value named by the manifest's full path, REG_DWORD, `0` = enabled; HKLM read as well; which registry view | the OpenXR loader specification, "API layer discovery" (Windows) |
| an implicit layer loads into every OpenXR application, and layers' order | the same |
| ReShade's OpenXR layer manifest name `ReShade64_XR.json`, its layer name `XR_APILAYER_reshade`, that it loads `ReShade64.dll`, and that ReShade 6.x setup contains both files | ReShade 6.x release, its installer (a zip appended to the exe) and reshade.me's VR notes |
| ReShade hooks the OpenXR swapchain and does not need the game's own device | the ReShade forum thread above; ReShade source (`runtime`, `openxr`) |
| `openxr_loader.dll` beside a game means it goes through OpenXR; some games load the runtime from the system | Khronos loader docs; a sample of VR games |
| whether the extracted `ReShade64.dll` carries an Authenticode signature we can verify before registering it for every OpenXR app | the shipped DLL's properties |
| the feeder, bridge and DLSS 5 add-on work in an OpenXR swapchain | a headset test; nothing else |

## Fixtures

- The ReShade setup: a small fixture zip with `ReShade64.dll` and
  `ReShade64_XR.json` (the real manifest's JSON, with a `library_path`).
- Registry states, behind the interface of the plan's Windows layer: none; one
  of ours enabled; one of ours with value 1; one foreign enabled with the file
  present; one foreign whose file is gone; HKLM versus HKCU.
- Two install records listing the registration, one in a folder that no
  longer exists, and one record that cannot be read.
- The diagnosis lines (`body.py:379-386`) for an install record with `vr` true
  and the registry empty.
- Library trees with `openxr_loader.dll` at different depths, and one tree too
  large to finish within the budget.

## Open questions

1. **Signer check.** The layer is a DLL loaded into *every* OpenXR application
   of the user. Verify the extracted `ReShade64.dll` (ReShade's signer, if it is
   signed; otherwise a pinned hash per ReShade release) before registering? The
   takeover's rule on hashes and signers applies.
2. **Should the registration be opt-out per run** (a "turn off ReShade for other
   OpenXR apps" switch, e.g. by registering only while the game runs)? The
   reference registers permanently; this is a larger design.
3. **How to learn the unknown.** Should "share the result" carry a `vr` flag so
   the first headset reports are countable, without waiting for free text?
4. **A disabled registration** (above): leave alone and warn, or re-enable?
