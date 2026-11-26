# GPU, driver and graphics preference

DLSS 5 neural rendering runs only inside NVIDIA's NGX runtime, so every
route depends on which card the PC has, which driver it runs and which GPU
Windows lets the game draw on. This spec covers: finding the card and its
architecture; finding the driver; refusing a PC nothing can run on; choosing
the `nvngx_dlssnr.dll` build that matches the card and checking it before it
is used; the driver rules and warnings; and Windows' per-program graphics
preference on PCs with two GPUs. Read from the reference's `core/gpu.py`,
`core/gpupref.py` and their callers in `core/installer.py`, `core/dlss.py`
and `core/optiscaler.py`, from the README sections "Your graphics card" and
"Settings worth knowing", and from the GPU names in `_tools/reports`.

The routes that consume these answers are in [routes](routes.md); where the
runtime comes from is in [components](components.md).

## What the user gets

### The card

- **Adapters come from Windows' present, enabled hardware adapters** (DXGI,
  skipping software adapters: flag `DXGI_ADAPTER_FLAG_SOFTWARE`), each with
  its PCI vendor id and its description. Vendor `0x10DE` is NVIDIA; `0x8086`
  Intel; `0x1002` and `0x1022` AMD.
  - Changed from the reference: one source for everything. The reference
    read the card name from the registry's display class
    (`HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\NNNN`,
    value `DriverDesc`, `gpu.py:46`) and only the graphics-preference code
    used DXGI (`gpupref.py:96`). The registry keeps an entry for every card
    the PC ever had (`gpu.py:144`, `gpu.py:254`), so a removed GTX 1660 made
    the tool refuse a PC that now holds an RTX 4070. Registry reading is only
    the fallback when DXGI cannot be created.
- **The card name maps to a CUDA architecture** ("sm") by these rules, in
  this order, the first that matches wins (`gpu.py:73`):

  | # | The name (upper-cased) | sm | Family |
  | --- | --- | --- | --- |
  | 0 | contains none of NVIDIA, GEFORCE, RTX, QUADRO | unknown | not an NVIDIA name |
  | 1 | contains the word ADA | 89 | Ada (workstation "RTX 2000/4000/5000/6000 Ada": their numbers are not GeForce series) |
  | 2 | contains BLACKWELL | 120 | Blackwell (workstation "RTX PRO ... Blackwell") |
  | 3 | `RTX A` + 3-4 digits | 86 | Ampere workstation |
  | 4 | QUADRO RTX or TITAN RTX | 75 | Turing workstation |
  | 5 | RTX or GTX + a 3-4 digit number: 5000-5999 | 120 | RTX 50 |
  | | 4000-4999 | 89 | RTX 40 |
  | | 3000-3999 | 86 | RTX 30 |
  | | 2000-2999 or 1600-1699 | 75 | RTX 20 / GTX 16 |
  | | 1000-1099 | 61 | Pascal (cannot run) |
  | 6 | anything else | unknown | |

  Display names: 75 "RTX 20 / GTX 16 (Turing)", 86 "RTX 30 (Ampere)", 89
  "RTX 40 (Ada Lovelace)", 120 "RTX 50 (Blackwell)" (`gpu.py:27`). The
  numbers 80, 87, 90, 100 and 121 are data-centre parts named in the table
  but not reachable by name.
- **With several adapters** the newest architecture wins (a laptop lists an
  integrated GPU and a discrete one), and of two of the same sm the one with
  tensor cores wins (`gpu.py:110`).
- **A Turing card without tensor cores** is told apart from an RTX 20: GTX
  16-series, MX 450/550, and the workstation T400, T500, T550, T600, T1000,
  T1200, T2000 (`gpu.py:131`). They share sm 75, which the runtime check
  needs, but the driver offers no DLSS on them.
- **A PC nothing can run on is refused before any write**, on every route
  (`installer.py:3601`):
  - a card without tensor cores: "<name> has no tensor cores - NVIDIA's
    driver does not offer DLSS on it, so DLSS 5 cannot run on it with any
    route. It needs an RTX card (RTX 20 or newer)";
  - a card with sm below 75: "<name> is older than RTX 20 and has no tensor
    cores - DLSS 5 cannot run on it with any route";
  - the user can override this (a setting called "ignore GPU mismatch" in the
    reference; `Options.ignore_gpu_mismatch`).
- **An unnameable card is never refused** (`gpu.py:144`). While any NVIDIA
  adapter has a name the table cannot read, nothing is refused, so a wrong
  table never blocks someone whose card is newer than the table. With no
  NVIDIA adapter at all nothing is refused either (the install then proceeds
  and warns, see Open questions Q3).
- **No NVIDIA card, AMD or Intel present:** the tool names the vendor and
  says why nothing is installed from here. The AMD text (`gpu.py:210`) says:
  neural rendering runs inside NVIDIA's `nvngx_dlssnr.dll`, so there is no
  runtime for a Radeon; two community projects run the network on Radeon
  through HIP (RDNA 3 / RDNA 4 with HIP 7, Direct3D 12, a reported 80 fps
  dropping to 12); neither can be installed from here because the open one
  keeps its runtime DLL and weights in a Discord channel and the other is
  closed source; the route becomes real the day either publishes the whole
  thing in a release under a licence that allows fetching. An Intel card gets
  the generic "no NVIDIA card detected - DLSS 5 will not run".

### The driver

- **The version is the one people know** ("616.92"), derived from the
  Windows file version of the driver: `32.0.16.1692` becomes the last digit
  of the third part joined with the fourth part (`61692`), split before its
  last two digits (`gpu.py:234`). A version that is not four dotted parts is
  returned as it is.
- **Source:** the file version of `nvapi64.dll` in System32 (the driver that
  is installed now). Fallback: the highest `DriverVersion` among the display
  class entries whose `DriverDesc` names NVIDIA. Read once per run
  (`gpu.py:254`). The fallback exists because the registry keeps old
  entries: one report had 616.92 installed and the tool read a stale 581.80
  entry and told the person to update (#242).
- **A comparison "is the driver at least X"** compares dotted integers and
  answers unknown when the driver or the text is unreadable (`gpu.py:431`).
  Unknown never triggers a warning or a refusal.
- **Thresholds** (named once, each with its reason):
  - **616.56**: the first driver documented to carry the neural-rendering
    runtime. The reference names it twice (`optiscaler.py:1218` and
    `dlss.py:1151`): one constant.
  - **616.64**: the first driver on which `renodx-dlss5` 4.6/4.7 fault inside
    `D3D12Core.dll` on every evaluate (`sources.py:153`); 616.86 and the later
    builds in the reports behave the same. The tool pins the add-on to 4.55
    there. The rules that act on it are in [routes](routes.md) ("Driver
    rules") and [components](components.md) ("renodx pins").
- **Driver warnings before an install** (`dlss.py:1189`) apply to every route
  that ends in NVIDIA's runtime (all but remix). Wording varies by route; the
  decision table is:

  | Driver | Route | Message |
  | --- | --- | --- |
  | unknown | any | none |
  | below 616.56 | any NGX route | "older than DLSS 5 itself: the neural-rendering runtime first appears in 616.56: the install will finish and the model will not run. Update the driver before trying anything else here" |
  | 616.56 to 616.63 | any | none |
  | 616.64 or newer | native, bridge, feeder | the 4.6/4.7 add-on builds fault on these drivers; the tool pins 4.55, which is a way round and not a fix; where standalone is offered, name it as the route to try first, then roll back to 616.56 |
  | 616.64 or newer | renodx-dlss | this route loads ShortFuse's add-on, to which the pin does not apply and about which nothing is known on these drivers; roll back to 616.56 if it crashes the moment neural rendering comes on |
  | 616.64 or newer | standalone-dlssnr | the fault is reached through `renodx-dlss5`, which this route does not load, so it is the route to try when another faults; it still runs NVIDIA's runtime |
  | 616.64 or newer | optiscaler, neural-upstream, gitc-uplift | the generic text: the driver faults in a good number of games and the pin does not apply to this route |

  The optiscaler route also returns its own card-and-driver note
  (`optiscaler.py:1221`): card unknown: "The OptiScaler author states an RTX
  50 series card is required. Your card could not be detected."; sm below
  120: "tested RTX 50 only; this tool installs the community build compiled for
  your card; if the overlay says the model refused, the native route is one
  click away"; sm 120 and driver below 616.56: "needs driver 616.56 or newer;
  you have <v>".
- **HDR** (`gpu.py:319`): whether any active display is in HDR (advanced
  colour enabled, not merely capable) is read through the Windows display
  configuration API; unknown when it cannot be told. The feeder route uses it
  for one warning: an HDR10 swap chain (R10G10B10A2 carrying PQ BT.2020) comes
  out blown or flat on feeder builds older than v0.15.1, so the person is told
  to take the newest release or turn HDR off while playing.

### The `nvngx_dlssnr.dll` build for the card

`nvngx_dlssnr.dll` is compiled per architecture. The build is chosen per card,
downloaded, and its contents are checked against the card before it is used.

| Card | Preferred build (label) | What the tool says (`gpu.py:483`) |
| --- | --- | --- |
| sm 120 or newer | `310.8.0` (NVIDIA's own, FP8) | "full speed ... a game still needs the add-on to ask for neural rendering - swapping DLSS DLLs alone does nothing" |
| sm 89 | `310.8.0-RTX40` (community, sm_89 kernels) | "works; expect a moderate frame-time cost" |
| sm 75 or 86 | `310.8.SF-v2`, then `310.8.SF` (community FP16) | "heavy: roughly half your fps at full model resolution. Lower the work area / model resolution" |
| any other sm | none preferred: the mirror's order | "This card is below RTX 20; DLSS 5 will not run on it." |
| unknown card | none preferred | no sentence |

- **The order** (`gpu.py:466`): the preferred labels first, in the order
  above, then every other build in the mirror's order (newest first). The
  preference is by label because a patched build can also list sm_120, and an
  RTX 50 must get NVIDIA's original.
- **A pinned build** (the user chose a label in "nvngx_dlssnr") is the only
  candidate. If it does not match the card the install stops: "Build <label>
  will not run on <card>. <reason>. Leave the version on Auto and the tool
  picks the newest build that supports your card."
- **With Auto**, candidates are tried in order: a candidate that does not
  match the card is skipped (named in the log and in the notes: "skipped as
  incompatible: ..."); the first that matches is used. If none matches:
  "No suitable nvngx_dlssnr build found for <card>. Tried: <labels>. DLSS 5
  currently runs on NVIDIA RTX 20 series and newer."
- **The check reads the file, not the label** (`gpu.py:511`): it scans the
  DLL for CUDA fatbin records and collects every sm they carry code for, so a
  release the table has never heard of is judged correctly. Result:
  - compatible: "compatible with your card (file supports: <list>)";
  - incompatible: "THIS BUILD WILL NOT RUN ON YOUR CARD. It contains no code
    for <card family>. File supports: <list>";
  - cannot tell (no architectures found, or the card is unknown): installed
    with the warning "could not verify GPU compatibility (<why>)". A
    mismatch the user overrode is installed with "dlssnr <label> does not
    match your card - installed anyway".
- **Record format being read:** a fatbin starts with the magic `0xBA55ED50`;
  the u16 at +6 is its header size and the u64 at +8 its payload size; inside
  it, entries have a header size (u32 at +4, 24 to 4096) and a payload size
  (u64 at +8); the sm number is the u32 at offset 24, 28 or 20 of the entry
  when it is a known sm. Known sm values: 50, 52, 53, 60, 61, 62, 70, 72, 75,
  80, 86, 87, 89, 90, 100, 101, 120, 121. The file is memory-mapped, never
  read whole (the file is about 165 MB).
- **The recorded version** of the runtime is the label ("310.8.0-RTX40").
  The staleness check never calls it outdated: the build is per card, so a
  newer label is not "behind" (see [components](components.md)).

### Two GPUs: Windows' graphics preference

A laptop (or a desktop with the processor's graphics on) has an NVIDIA GPU
and another one; Windows decides per program which draws. NGX exists only on
the NVIDIA card, so a game or helper that lands on the other GPU runs without
neural rendering. Settings > System > Display > Graphics is where a person
sets this by hand; the tool sets it for them, only when it makes sense.

- **When it applies** (`gpupref.py:188`): an NVIDIA adapter and an Intel or
  AMD adapter are both present (DXGI, hardware only; registry-name fallback).
  The setting is not offered and not written on a one-GPU PC. The answer is
  computed once per run and names the other vendor.
- **What is written:** under `HKCU\Software\Microsoft\DirectX\UserGpuPreferences`,
  a `REG_SZ` named by the program's **full path**, with the token
  `GpuPreference=2;` ("High performance"). The string is shared with other
  per-program switches Windows keeps there (`SwapEffectUpgradeEnable=1;`,
  Auto HDR), so the string is parsed into `key=value;` tokens, our token is
  added or removed, and every other token is left exactly as found.
- **For which programs** (`installer.py:3345`): the game's executable; on
  the feeder route with the helper process (every 32-bit game, or the
  64-bit "run DLSS in a helper process" setting) also
  `host64\dlss5-feed-host64.exe`; on the gitc-uplift route for a game that is
  not 64-bit also `gitc-uplift-helper64.exe`. On the Remix route too.
- **A choice the person already made is kept**: any existing `GpuPreference`
  value (0, 1 or 2) is theirs and is never recorded as ours. The install says
  "<exe>: your own Windows graphics setting is kept" and writes the note
  "<exe> already had a Windows graphics setting (Settings > System > Display
  > Graphics); it was left as it is".
  - Changed from the reference: when the kept value is not 2, also say that
    the game may draw on the other GPU and name the setting to change. The
    reference said only that it was kept, so a person who set "Power saving"
    once got a silent non-working install.
- **Recorded in the install record**, per program: `{exe, was, wrote}`, where
  `was` is the full string before (absent when there was no value) and
  `wrote` the string written. A reinstall that finds our own `2` and a record
  for that path keeps the first record rather than reading our value as the
  person's.
- **Taken back out** on uninstall, or on an install made with the setting off
  (`gpupref.py:277`): only while the token still says 2. Untouched since:
  the old string goes back byte for byte (or the value is deleted when there
  was none). Changed since by Windows (the string differs from `wrote`):
  only our token is removed, the rest stays. Our token changed or gone: the
  person's now, left alone.
- **Never stops an install** (`installer.py:3376`): a failure to write (or
  read) the registry is logged and reported as a warning ("could not set the
  Windows graphics setting to High performance (<why>); set it by hand in
  Settings > System > Display > Graphics if the game runs on the other GPU"),
  and the record of an earlier install is carried forward.
- **Where it shows:** a "use the NVIDIA card (Windows setting)" toggle on the
  game's settings, on by default, shown only on a two-GPU PC. The preview
  lists it among the writes outside the game folder: "Windows graphics
  setting: <names> set to 'High performance' (the NVIDIA card, not the <other>
  one) for this user. A choice you already made there is kept. 'uninstall'
  takes it back out." Command line: `--no-gpu-pref`.

## What must never happen

- **Refusing a PC on a guess.** An unnameable card, an unreadable driver or
  an unreadable HDR state never blocks an install and never warns.
- **Installing a runtime into the game before it is checked.** The match
  between a build and the card is decided before the file touches the game
  folder.
- **Overwriting a person's Windows graphics choice**, or deleting a token or
  a string we did not write. A removal touches only our own `GpuPreference=2`
  and only while it is still ours.
- **Losing the person's other graphics tokens** (Auto HDR, windowed-game
  optimisations) when the preference is added or removed.
- **Writing the preference outside HKCU** (it is per user, no elevation).
- **Failing an install because the graphics preference could not be
  written.**
- **A ghost adapter deciding anything**: a card that is no longer present
  does not choose the architecture, the driver or the refusal.

## Bugs and questionable choices in the reference not to port

- **The runtime is extracted into the game folder before it is checked**
  (`installer.py:4804` ReShade routes, `installer.py:4096` Remix, `installer.py:4287` OptiScaler): a build
  that does not match is written over the game's folder, then replaced by the
  next candidate; a failure between leaves a wrong DLL in place. Check in the
  download cache or a temp file, then place.
- **Two different candidate policies for one decision.** The ReShade routes
  and the Remix route loop through the ordered candidates and skip
  mismatches; the OptiScaler route takes only the first candidate and fails
  if it mismatches (`installer.py:4287`). One policy (the loop).
- **The preferred build is chosen by label text** (`gpu.py:466`): a new
  release labelled differently is not preferred, so an RTX 50 may get a
  patched build. Keep the label order as data, and also prefer, among
  compatible builds, the one that carries code for the card natively over one
  that merely lists it only through other architectures (to verify, see
  below).
- **Ghost adapters**: the registry display class (see above).
- **`no_dlss` misses pre-Turing GTX cards with a three-digit number**
  (GTX 980, GTX 750 Ti): their name does not match any range, so the card is
  "unknown" and nothing is refused (`gpu.py:73`).
  - Changed from the reference: a GTX or GT card numbered below 1600 is older
    than Turing and is refused like the 10 series.
- **The same constant twice** (616.56) and the stale "RTX 50 only" wording on
  the optiscaler route.
- **The driver is read from System32's `nvapi64.dll`**, which is the driver
  installed, not necessarily the one of the adapter the game will use on a PC
  with several NVIDIA parts. Acceptable; see Q2.
- **`gpu.py:367` HDR structs and `gpupref.py:141` DXGI vtable calls** are
  hand-written COM and P/Invoke in the reference (offsets such as vtable
  index 12 and 10). In C# use `CreateDXGIFactory1` through a maintained
  binding (for example `Vortice.DXGI` or CsWin32-generated code) and
  `QueryDisplayConfig` through CsWin32: do not hand-lay structs.
- **`gpu.py:173` and `installer.py:4827`** wording for a card under RTX 20 says "RTX 20 series and
  newer" while the table also treats GTX 16 as no-tensor: one sentence for
  the one rule.
- **The AMD text embeds a research date and two project names** that will go
  stale ("Checked 2026-09-09", `gpu.py:208`): keep it as data a release can
  change, with its date.

## To verify against primary sources

| Claim | Source |
| --- | --- |
| A CUDA fatbin's header and entry layout (magic `0xBA55ED50`, offsets 6 and 8, entry fields, sm at 20/24/28) and that the cubins inside a compressed fatbin still have a readable sm field | NVIDIA CUDA Binary Utilities docs; `cuobjdump --list-elf` and `--list-ptx` on the real `nvngx_dlssnr.dll` builds; LLVM's fatbin readers |
| Whether an entry carrying only PTX for a lower architecture (JIT-compilable on a newer card) counts as "will run": the reference says no and may refuse a build that would run | `cuobjdump -lptx`/`-lelf` on the four known builds; NVIDIA "CUDA Compatibility" guide, "PTX JIT" |
| The architecture and DLSS-capability of each name pattern: workstation "RTX A", "RTX 2000 Ada", "RTX PRO ... Blackwell", MX 450/550, T-series; GTX 16-series having no tensor cores | NVIDIA product pages and the NVIDIA DLSS-supported-GPU list |
| That `nvapi64.dll`'s file version maps to the marketing driver number by "last digit of the third part + fourth part" for every driver branch (including Studio and DCH) | NVIDIA driver release notes against `FileVersion` of real installs |
| That 616.56 is the first driver carrying the neural-rendering runtime, and 616.64 / 616.86 / later fault with `renodx-dlss5` 4.6/4.7 | the DLSS5-Feeder issue #54 and 0.14.0-beta.1 notes (cited in `sources.py:146`); NVIDIA release notes; rerun on a current driver, since the pin may now be obsolete |
| That `HKCU\Software\Microsoft\DirectX\UserGpuPreferences` with `GpuPreference=2;` is what Settings > Graphics writes for "High performance", and that other tokens live in the same string | Microsoft docs on per-app GPU selection; observe Settings writing the value |
| That "High performance" means the NVIDIA GPU on every hybrid layout (NVIDIA + Intel, NVIDIA + AMD iGPU, an AMD dGPU beside an NVIDIA one) | Microsoft docs; test on an AMD-iGPU + NVIDIA desktop |
| That a program can be pinned to one named GPU (a value other than 0/1/2 or a luid form) on a PC with two NVIDIA GPUs | Microsoft docs on `GpuPreference` and DXGI `EnumAdapterByGpuPreference` |
| That the community Radeon projects are still unpublishable from a release page | the two repositories' release pages |

## Fixtures

- **Card names:** the real names in `_tools/reports/*.txt` (the `gpu:` line,
  184 reports: RTX 5090, 5080, 5070 Ti, 5060 Ti, 4090, 4070 Ti, 4060 Ti, 3060,
  2070 SUPER, laptop variants such as "RTX 4060 Laptop GPU" and "RTX 3050 6GB
  Laptop GPU") with the architecture each report prints (`sm_120`, `sm_89`).
  Add by hand: "NVIDIA RTX 2000 Ada Generation", "NVIDIA RTX 5000 Ada
  Generation", "NVIDIA RTX A4000", "Quadro RTX 4000", "TITAN RTX", "NVIDIA
  GeForce GTX 1650", "GTX 1660 SUPER", "GeForce MX450", "NVIDIA T1000",
  "NVIDIA GeForce GTX 1080 Ti", "NVIDIA GeForce GTX 980", "AMD Radeon RX 7600",
  "Intel(R) Arc(TM) A770", "Microsoft Basic Display Adapter".
- **Driver versions:** file versions `32.0.15.6094`, `32.0.16.1664`,
  `32.0.16.1692`, `32.0.15.5612` and their marketing forms (616.64, 616.92,
  561.12); an unparsable string; a registry list with an old 581.80 entry and
  a current 616.92.
- **Fatbin files:** synthetic buffers with one fatbin carrying sm 75/86/89/120
  in each offset variant (20, 24, 28), a corrupt size, a buffer with no
  magic, and a magic inside a larger file. The four real build labels
  (`310.8.0`, `310.8.0-RTX40`, `310.8.SF`, `310.8.SF-v2`) with the sm sets the
  module comment lists: RTX 50 only; RTX 40 and 50; RTX 20, 30, 40 and 50 for
  both SF builds (`gpu.py:7`). Do not commit the 165 MB DLLs; commit the
  derived sm sets and a trimmed synthetic fatbin.
- **Graphics preference:** registry values as dictionaries: none; `GpuPreference=2;`;
  `GpuPreference=1;`; `SwapEffectUpgradeEnable=1;GpuPreference=2;`; a value
  changed after our write (Auto HDR toggled). The reference's own tests swap
  the registry for a dictionary (`gpupref.Memory`): the C# seam is a registry
  interface, not the registry.
- **Adapter lists:** NVIDIA only; NVIDIA + Intel iGPU; NVIDIA + AMD iGPU;
  AMD only; a software adapter plus NVIDIA; an NVIDIA name the table cannot
  read beside a known one.
- **Old tests:** `git show d5801a3^:test_all.py` sections "8. route rules,
  version pins and the OptiScaler dials" (the Pascal case, line 1881) and
  "22. FSR and XeSS games through OptiScaler".

## Open questions

- **Q1.** Is the label-ordered preference to stay (an RTX 50 always gets
  `310.8.0` even when a newer NVIDIA-native build is published), or should it
  become "the newest build whose fatbin carries native SASS for the card,
  preferring the publisher's own"? The second needs the verification above.
- **Q2.** Two NVIDIA GPUs in one PC (a desktop RTX 4090 beside an RTX 3060):
  the reference picks the newest architecture and the driver of System32's
  `nvapi64.dll`. Should the tool pick the adapter the game's preference
  points at, or let the person choose in settings?
- **Q3.** A PC with no NVIDIA adapter at all (AMD or Intel only) is not
  refused: the install proceeds and the runtime check cannot decide, and the
  optiscaler steer treats an unknown card as RTX-capable (`dlss.py:480`).
  Refuse with a way past it ("install anyway"), or keep warning only?
- **Q4.** Should the graphics preference also offer pinning to a named GPU
  on a PC with two NVIDIA GPUs, or stay "High performance" only?
- **Q5.** Pre-Turing GTX refusal (changed above): accept?
