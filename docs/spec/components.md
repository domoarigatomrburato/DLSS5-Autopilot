# Third-party components

Neural rendering is assembled from parts other people publish. The tool
bundles none of them: each is resolved from its publisher on the day of the
install, downloaded into the per-user cache, checked, and only then placed in
a game folder. This spec lists every component, where it comes from, how its
version is picked, what is cached, what is verified, and how a game set up
earlier is told that a part has moved on. The download path itself (retries,
TLS, size limits, archive extraction) is [net](net.md); which route uses
which part and where it is written is [routes](routes.md); the per-card choice
of the `nvngx_dlssnr.dll` build is [gpu](gpu.md). Read from the reference's
`core/sources.py`, `core/components.py`, `core/optiscaler.py`, `core/dxvk.py`,
`core/vulkan.py`, `core/refw.py`, `core/mfg.py`, `core/remix.py` and the
README sections "Where the files come from" and "Is it safe".

## What the user gets

### The catalog

Every component, with its publisher and what is taken from it. "Mirror" is
`RankFTW/rhi-repo`, a community repository whose releases carry the
`nvngx_dlssnr.dll` builds and the RenoDX add-on builds; it is the one place
those come from.

| Component | Publisher and source | What is taken | Used by |
| --- | --- | --- | --- |
| ReShade (add-on version) | reshade.me | `ReShade_Setup_<v>_Addon.exe`; from its appended zip `ReShade64.dll`, `ReShade32.dll`, and the Vulkan layer manifests `ReShade64.json`, `ReShade32.json` | every ReShade route; the Vulkan layer |
| ReShade shader headers | `crosire/reshade-shaders`, branch `slim`, raw files | `ReShade.fxh`, `ReShadeUI.fxh`, `DrawText.fxh` | feeder, standalone |
| `renodx-dlss5` (Krish) | the mirror, releases tagged `renodx-dlss5-<v>` | the `.addon64` inside the zip | native, bridge, feeder |
| `renodx-dlss` "SF" (ShortFuse) | the mirror, releases tagged `renodx-dlss-SF-<v>` | the `.addon64` inside the zip | renodx-dlss |
| `nvngx_dlssnr.dll` | the mirror, releases tagged `dlssnr-<label>` | `nvngx_dlssnr.dll` inside the zip, per card | every route |
| `nvngx_dlss.dll`, `nvngx_dlssd.dll`, `nvngx_dlssg.dll` | NVIDIA, `NVIDIA/DLSS` repository, tree of the newest release tag, `lib/Windows_x86_64/rel/`; mirror releases `dlss-<v>` and `dlssg-<v>` behind it (no `dlssd` on the mirror) | the DLL itself | super resolution, ray reconstruction, frame generation |
| DLSS5-Feeder | `jlrouzies-fr/DLSS5-Feeder` | `dlss5-feed.addon64` or `.addon32` or `dlss5-feed-helper.addon64`, `dlss5-feed-host64.exe`, `DLSS5_Feed.fx`, loose or in one zip | feeder |
| LumeniteFX | `umar-afzaal/LumeniteFX`, branch zip | one provider `.fx` (`lumenite_Kernel.fx` or `lumenite_QuantMotion.fx`), the four `.fxh` includes, `lumenite_bluenoise256.png` | feeder, provider 3 or 4 |
| VORT shaders | `vortigern11/vort_Shaders`, branch zip | `vort_Motion.fx`, its includes under `Includes`, `vort_BlueNoise.png` | feeder provider 2, standalone |
| dlss5-bridge | `NIGos/dlss5-bridge`, newest release | the `.addon64` asset | bridge |
| neural-upstream | `matiasLombo/neural-upstream`, newest release | the asset `nvngx.dll.addon64` | neural-upstream |
| standalone-dlssnr | `kibblerz/DLSS5-Reshade-AIO`, newest release | `standalone-dlssnr.addon64`, `nvngx.dll`, `DLSS5_AIO_Feed.fx` (and `StandaloneBoundary.fx` when present), loose or in the `-64-bit.zip` | standalone |
| GITC-Uplift | `ghostinthecamera/GITC-Uplift`, newest non-pre-release carrying `GITC-Uplift-<v>.zip` | `gitc-uplift.addon64`, `gitc-uplift.addon32`, `gitc-uplift-helper64.exe`, `Uplift.fx`, `UpliftMask.fx` | gitc-uplift |
| OptiScaler (six builds) | see "OptiScaler builds" | the whole package | optiscaler |
| DXVK | `doitsujin/dxvk`, newest release | `dxvk-<v>.tar.gz` (never the `native` archive): `x64/` or `x32/` `dxgi.dll`, `d3d11.dll`, `d3d9.dll`, `d3d8.dll` | DirectX 8, 9 and 11 on the ReShade routes |
| REFramework | `praydog/REFramework-nightly`, newest release | `REFramework.zip` -> `dinput8.dll` | any ReShade route or optiscaler on a Capcom RE Engine game |
| RTX40MFG-Unlock | `dashdogy/MFG-Unlocker`, tag `v1.2.1` | `Universal-RTX-40-MFG-Unlock-*.zip`: `RTX40MFGCore.dll`, `RTX40MFG.asi`, `RTX40MFG-UI.addon64` | RTX 40 multi-frame generation |
| Ultimate ASI Loader | `ThirteenAG/Ultimate-ASI-Loader`, tag `v9.7.4` | `Ultimate-ASI-Loader_x64.zip` -> `dinput8.dll` | RTX 40 multi-frame generation |
| Remix runtime | `lunks/dxvk-remix-plus-dlssnr`, newest release | `d3d9.dll`, `remix_nvngx.dll` | remix, only with "swap the Remix runtime" |
| Frame generation files you downloaded | the person (see [routes](routes.md)) | files they pick | optiscaler |

Nothing else is fetched for the routes. Remix mod downloads and the
self-update are other areas; video and webcam are dropped from scope.

### Hosts

Every URL stays on the hosts this area passes as its allowlist, redirects
included; the list is in [net](net.md), with every other area's. No private
server is ever contacted. A redirect to any other host is refused.

- Changed from the reference: the reference promised these hosts
  (`sources.py:1`) but enforced nothing; any redirect was followed. The
  allowlist is data, so a component can not move off the list without a code
  review.

### How a version is resolved

Every install resolves each component it needs at that moment; nothing is
frozen except the pins below.

**GitHub release data**

1. `api.github.com/repos/<repo>/releases[/latest|/tags/<tag>]` is asked first.
2. Answers are kept in the [net](net.md) cache, one entry per URL, and
   **reused without a request for 6 hours** (`sources.py:435`).
3. When the live request fails (rate limit, no connection, 5xx), a cached
   answer of **any age** is used and the install says so: "GitHub could not
   be reached (rate limit or no connection); using the version list cached <n>h
   ago."
4. When there is no cached answer either, the same release data is **read from
   github.com's own pages** and put into the API's shape (`tag_name`,
   `prerelease`, `draft`, `assets[].name`, `assets[].browser_download_url`):
   the release list from `github.com/<repo>/releases?page=<n>` (tags only;
   up to 4 pages, a list of at most 12 releases), one release's assets from
   `github.com/<repo>/releases/expanded_assets/<tag>`, and "latest" from the
   redirect `github.com/<repo>/releases/latest` (a `HEAD`, which spends no API
   allowance). The install says "GitHub's API could not be reached; this
   release was read from github.com's release pages instead."
5. Components that take one asset by name and have a fixed download URL
   pattern have a last resort: the plain redirect
   `github.com/<repo>/releases/latest/download/<asset>`. It records the
   version as `latest`, which is not comparable, and the install says the
   exact version is whatever that redirect points at and may be a test
   build.

Which rule applies to which component:

| Component | Resolution |
| --- | --- |
| ReShade | `reshade.me` home page: the first link matching `/downloads/ReShade_Setup_<v>_Addon.exe`. Three attempts with a growing pause; a 5xx whose body still contains the link counts; a bare 5xx is retried. Then the newest tag of `crosire/reshade` matching `v<n>(.<n>)+` (the setup exe is not a release asset: its URL is built from the version). Then the newest `ReShade_Setup_*_Addon.exe` already in the cache. Then an error listing every reason |
| The mirror catalog | `RankFTW/rhi-repo` release list (100 per page) grouped by tag prefix into families (below). Fallback: the github.com pages, at most 3 builds per family, always keeping the builds the tool pins; that shortened list is never cached for the session |
| NVIDIA runtimes | tag from the `NVIDIA/DLSS` "latest" redirect (no API call). The DLL URL is `raw.githubusercontent.com/NVIDIA/DLSS/<tag>/lib/Windows_x86_64/rel/<name>`. If the redirect fails nothing is added and the mirror alone serves the family; that failure is not remembered |
| DLSS5-Feeder | the release list (15 newest). See "Feeder builds" |
| dlss5-bridge | `/latest`; the first asset ending `.addon64`; the API-or-pages rule |
| neural-upstream | `/latest`; asset named `nvngx.dll.addon64`; with the API unreachable and nothing cached: the `latest/download` redirect, version `latest` |
| standalone-dlssnr | `/latest`; the three loose assets; when any is missing, the asset ending `-64-bit.zip`; with the API unreachable: the tag from the redirect and the archive name `DLSS5-ReShade-AIO-<tag>-64-bit.zip` (asked with a `HEAD` first), else the three loose assets by the `latest/download` redirect |
| GITC-Uplift | the newest 10 releases, drafts and pre-releases dropped, newest `published_at` first, the first that carries an asset matching `GITC-Uplift-v?<version>.zip`, with `SHA256SUMS.txt` beside it when present; with the API unreachable, the newest such zip in the cache (no sums known) |
| OptiScaler | see "OptiScaler builds" |
| DXVK | `/latest`; the first asset named `dxvk-*.tar.gz` without `native` |
| REFramework | `/latest` of the nightly repository; the asset `REFramework.zip` |
| RTX40MFG-Unlock | the pinned tag `v1.2.1` asked by tag first (the list shows only 12 on the pages fallback and the 15th entry today); then the newest non-draft non-pre-release whose asset starts `universal-rtx-40-mfg-unlock` and ends `.zip`; a release of another shape is skipped and "a newer layout is not taken on trust" |
| Ultimate ASI Loader | the pinned tag `v9.7.4`, asset `Ultimate-ASI-Loader_x64.zip`. v10.0.0 (2026-10-21) exists and nobody has run it with this route: the pin moves when someone has |
| Remix runtime | `/latest`; assets `d3d9.dll` and `remix_nvngx.dll` both required; with the API unreachable the `latest/download` redirect |
| LumeniteFX, VORT | codeload branch zips (`refs/heads/mainline`, `refs/heads/main`); no API; no version |
| Shader headers | raw files from the `slim` branch, each install |

**The mirror's families.** A release belongs to the first family whose
prefix its tag starts with, longest prefix first (`dlssnr` and `dlssg` both
start with `dlss`, so the bare `dlss-` family is last): `renodx-dlss5` ->
`renodx`; `renodx-dlss-SF` -> `renodx_sf`; `dlssnr` -> `dlssnr`; `dlssg` ->
`dlssg`; `dlss-` -> `dlss`. The label is the tag minus the prefix and a
leading `-` (`4.55`, `310.8.0-RTX40`, `310.8.SF-v2`). An entry is the
release's `.zip` asset. Entries are sorted newest first by the integers in
the tag (`310.8.0` is `(310, 8, 0)`). NVIDIA's entries are put in front of
the mirror's in the `dlss`, `dlssd` and `dlssg` families, with the label
`<sdk tag> (NVIDIA SDK)` (one SDK ships all three, and NVIDIA numbers each
runtime separately, so the label names the SDK). The catalog is remembered
for the process, except the shortened pages version.

**Choosing an entry** (`pick`): the entry whose label or tag equals the wanted
text, else the newest. An empty family is "a build list that could not be
read", with a message, never an index error. "Wanted text not found, newest
taken" is silent in the picker; the caller says it (see "renodx pins").

**Placing a runtime from a family** tries the chosen entry and then the next
one in the family; a failure on the first (a blocked download host answering
200 with an HTML page) is logged and the second is used. Only when both fail
is an error raised, naming the first source's reason too.

### Pins

A pin keeps an install on a known-good build when the newest is known to
break. Pins are data.

| Pin | Applies | Rule | Evidence |
| --- | --- | --- | --- |
| `renodx-dlss5` 4.55 | driver 616.64 or newer, routes native, bridge, feeder | the add-on is 4.55 | 4.6/4.7 fault inside `D3D12Core.dll` on every evaluate (feeder issue #54: 4.55 passes 300/300, 4.7 passes 0/300) (`sources.py:146`) |
| `renodx-dlss5` 4.60 | OpenGL games | the add-on is 4.60 | 4.70's workset pool never recycles under OpenGL ("NR workset pool exhausted" after four frames), verified on six OpenGL games (`sources.py:140`) |
| `renodx-dlss5` 4.55 | feeder releases below `v0.8.0-beta.3` | the add-on is 4.55 | newer builds overlap that feeder and `CreateFeature` dies with 0xC0000005 |
| feeder `v0.13.1-beta.1` or newer | Direct3D 10 games | older builds refuse D3D10: stop before writing and name the build to pick | feeder release notes |
| feeder `v1.18.0-beta.1` or newer | the 64-bit helper-process mode | older builds have no helper mode: stop before writing | feeder release notes |
| feeder `v2.0.0` or newer | the built-in motion-vector provider | older builds have none: stop before writing | feeder release notes |
| feeder `v0.15.1` or newer | HDR displays | older builds blow out HDR highlights: warn | the feeder's 0.15.1 notes |
| GITC-Uplift ReShade 6.8.0 or newer | Vulkan and OpenGL games | refuse when the ReShade the PC can get is older | GITC-Uplift README |
| RTX40MFG-Unlock `v1.2.1`, ASI Loader `v9.7.4` | the multi-frame-generation option | exact tags | see the catalog |

The precedence among `renodx-dlss5` pins: the person's explicit choice (a
label or a file of their own) first; then 616.64+ -> 4.55; then OpenGL ->
4.60; then a feeder below `v0.8.0-beta.3` -> 4.55; otherwise the newest.
The 4.55 label satisfies all three pins.

- **A pin that is not on the list stops the install before anything is
  written**, with: "renodx-dlss5 <pin> is not on the build list; the newest
  build is the kind of build this pin exists to avoid. Pick the build by hand
  under 'dlss5 add-on' to go ahead."
  - Changed from the reference: the reference logged a warning and installed
    the newest build, "which is the kind of build that pin exists to avoid"
    (`installer.py:4766`). It knowingly installs the faulting build.

### Feeder builds

- "newest release" means the newest release whose tag is not a test build.
  A tag is a test build when GitHub flags it a pre-release **or** the tag
  contains `beta`, `alpha`, `-rc`, `preview` or `-pre` (`sources.py:253`):
  the feeder's author publishes betas with the flag off, so `/latest` returned
  `1.16.0-beta.x` while v0.15.1 was the release line (#325, #348).
- "newest pre-release" means the newest build of any kind that is not a
  draft.
- An exact tag is any of the 15 newest releases; one that is not there:
  "DLSS5-Feeder release <tag> is not on GitHub's release list (the newest 15
  are checked)."
- With only test builds among the 15, the github.com pages (two pages) are
  walked for the newest non-test tag; if every source answers a test build, it
  is installed and the report says "Every recent DLSS5-Feeder release is a
  test build; <tag> was installed. Pick an exact version under 'feeder build'
  to change that."
- Versions compare by their integers with a beta below its release
  (`v0.9.0-beta.1` < `v0.9.0`).
- The assets are `dlss5-feed.addon64` (or `.addon32`, or the helper add-on),
  `dlss5-feed-host64.exe` and `DLSS5_Feed.fx`; from 0.10.0 they come in one
  zip whose name contains "feeder". The `.fx` is placed in the shader folder,
  the host in `host64`.

### OptiScaler builds

Six selectable builds; each is the newest release of its page that carries an
OptiScaler package. One is the default.

| Key (CLI) | Publisher and page | What it is |
| --- | --- | --- |
| (default) | `Dagherbou/OptiScaler_DLSSNR`, `/latest` then the 10-release list | the neural pass after the upscaler |
| `y4my4my4m` | `y4my4my4m/OptiScaler_DLSSNR_Multipass_MFG` | development builds in `.7z`; RTX 40 multi-frame generation; archives named `with_dlss` are skipped |
| `wilsjo2` | `wilsjo2/OptiScaler-DLSSNR-PreSR-Multipass` | the pass runs before super resolution, 1 to 3 passes; archives with `rtx40-mfg` are skipped |
| `wilsjo2-mfg` | the same page | the `rtx40-mfg` package only; refused on any card but RTX 40 (sm 89) |
| `janblade` | `Janblade/OptiScaler-F5-DLSSNR-Multipass` | a fork of wilsjo2's; archives with `-test-` are skipped |
| `jlrouzies` | `jlrouzies-fr/OptiScaler-DLSSNR-PreSR-Multipass` | wilsjo2's, with `[DlssNr]` keys re-read while the game runs; archives with `rtx40-mfg` are skipped |

Package choice (`optiscaler.py:272`):

- Releases are taken newest `published_at` first; drafts and the tag
  `nightly` are dropped (a fixed tag is cached once and never refreshed).
  The pre-release flag is ignored: the forks flag their newest release that
  way and it is the one wanted.
- Only archives ending `.zip` or `.7z` count, and only those whose name
  contains `optiscaler` (a release page can carry a different program of the
  author's: wilsjo2's page carried `display-filter-v0.2.0-preview`, 31 MB, no
  `OptiScaler.dll`, and "the newest release that carries an archive"
  installed it into people's folders, #364). A release with no such archive is
  passed over, with a log line, and the release before it is tried.
- When the release has several package archives, the skip words name the
  variants. If every archive matches a skip word (names moved upstream) all of
  them are kept. A build that exists for one variant (`wilsjo2-mfg`) takes
  only an archive carrying its word, or the release is passed over: never the
  standard zip under the variant's name.
- With several left, the shortest name wins when it is the stem of every
  other (a variant is the standard name with something added); otherwise the
  release's own order. What the release carried and what was chosen are
  logged, so an upstream rename reads as a log line.
- The default build's "latest" release that holds no package falls back to
  the release list, newest first.
- After unpacking, a package without a root-level `OptiScaler.dll` is
  refused before anything is written: "The release <tag> carries no
  OptiScaler.dll - it holds <names>, so it is not an OptiScaler build. Pick
  another 'optiscaler build' under settings, or try again later."
- The cache name carries the tag and the asset's own name
  (`OptiScaler-DLSSNR-<tag>-<stem>.zip`), so the two zips of one release do
  not share an entry (#196).
- A `.7z` is unpacked by [net](net.md)'s one extractor (SharpCompress), with
  no program started. Unpacking goes into a `.part` folder and is renamed
  whole. Changed from the reference: it ran an installed 7-Zip found on the
  PATH, else Windows' `tar.exe`, else a downloaded `7zr.exe`.
- The wilsjo2 package picked by builds before 1.9.1 was the RTX 40 MFG one;
  the staleness check says so (see below).

### The staleness check ("check versions")

For a game set up earlier the tool compares the versions in its install
record with what the sources offer now, and marks the game **update** with
the number of parts that need installing again. It only reports; installing
again already takes the newest of everything.

- What is compared: `reshade`, `renodx`, `renodx_sf`, `dlssnr`, `dlss`,
  `dlssd`, `dlssg`, `bridge`, `upstream`, `standalone`, `uplift`, `feeder`,
  `optiscaler`, `remix_runtime`. A game with no recorded component versions
  falls back to the older "<name> version: <label>" lines in the record's
  notes.
- Each component is resolved at most once per run, however many games are
  checked.
- A component that cannot be resolved is left out (a network failure is
  neither "current" nor "behind").
- A version `latest` (recorded when the API was out of reach) on either side is
  not compared.
- Outdated means the number goes up. Versions compare by the first four
  integers in the label; labels that are not numbers compare as different,
  never as older.
- Exceptions:
  - `dlssnr` is never outdated (the build is per card).
  - the pinned `renodx` is compared to its pin, not to the newest: 4.55 on
    driver 616.64+, 4.60 for an OpenGL game.
  - a feeder recorded as "newest release" (no tag, no pre-release flag) that
    is in fact a test build is marked: "this is a test build - 'newest
    release' no longer picks one; install again", unless the newest release
    resolves to the very same tag.
  - an `optiscaler` whose recorded proxy file is missing is marked: "the proxy
    this install wrote is not in the folder - install again" (#364).
  - an `optiscaler` recorded with a fork is never compared to Dagherbou's
    numbers; the one exception is wilsjo2's standard build recorded by a tool
    older than 1.9.1, which took the RTX 40 MFG package: "this is the RTX 40
    MFG package - install again for the standard one".
- Files the record lists that are no longer in the folder (a game update or
  "verify files" removed them) are one more line: "files in the game folder:
  <n> missing - install again", naming up to three. Not counted: a record that
  is not complete, a record marked rolled back, backups of the game's own
  files, files that are written again anyway (`.log .ini .cfg .txt .json .md`),
  files the tool switched off, and anything ending in a backup suffix.
- Summary text: "nothing recorded to check"; "all <n> components are current";
  "<k> of <n> components have a newer version"; "<k> of <n> components need
  installing again" when any stale item carries a note.

### What is cached

The cache's layout, keys and locking are [net](net.md)'s. This area caches:

| What | Lifetime |
| --- | --- |
| downloads | until the file fails its checks, or is cleared |
| unpacked OptiScaler archives | with the archive |
| GitHub API answers | fresh 6 h; used at any age when the live call fails |
| the mirror catalog, the NVIDIA runtime list, each component's latest | memory, per process, for the run |
| the person's own frame generation files, in `%LOCALAPPDATA%\dlss5-autopilot\own-fg\<recipe>` (not the cache: the person's files) | until "forget" |

The readable part of each cached file's name is the asset's own name
(`ReShade_Setup_<v>_Addon.exe`, `dxvk-<v>.tar.gz`, the mirror's zip); the
identity is net's key, never a label.

The preview ("what will happen?") promises not to make a single request. It
reads the API cache of any age and the download cache, and knows less when
nothing is cached.

### Integrity

What the reference verifies, and what it does not:

| Component | Check |
| --- | --- |
| any download | magic bytes for the extension (`.zip` also as a whole archive, `.7z`, `.exe`/`.dll`/`.addon64`/`.addon32` start with `MZ`); size against the declared length |
| NVIDIA DLLs (raw) | 200,000 bytes or more, `MZ`, a `PE\0\0` signature at the offset in the header, machine `0x8664`; a failing file is deleted from the cache and not written |
| GITC-Uplift | each extracted file against `SHA256SUMS.txt` of the same release; a mismatch deletes the file and the cached zip and refuses; sums that cannot be read, or name none of the files, install unchecked with a warning |
| the runtime DLL | the architectures inside it, against the card ([gpu](gpu.md)) |
| OptiScaler | a root-level `OptiScaler.dll` in the package |
| MFG unlock, ASI Loader | each zip holds every expected file, checked before the first file is placed |
| everything else | nothing beyond the magic bytes |

- Changed from the reference: verify more, where a publisher gives a way.
  1. Every check is one of [net](net.md)'s verifier kinds, named per
     component. Where the release has a `SHA256SUMS` file or the API answers
     an asset `digest`, the downloaded file must match it, and a mismatch
     refuses.
     When the release is known to publish sums (GITC-Uplift always has;
     jlrouzies' OptiScaler fork "a SHA256SUMS file per release",
     `optiscaler.py:108`) and they cannot be read, the install **stops**,
     with a message, rather than installing unchecked: a network that blocks
     one small file must not turn the check off.
  2. Where a publisher signs the file (NVIDIA signs its runtimes), the
     Authenticode signer must be that publisher. This runs behind a platform
     interface.
  3. An `.addon64` or `.addon32` is checked to be a 64-bit or 32-bit PE
     respectively before it is placed (the bridge and upstream add-ons were
     only checked for `MZ`).
  4. A component recorded with the digest or signer it was installed with, so
     the staleness check can say "the file in the folder is not the one this
     tool wrote".
- Changed from the reference: the cache key is [net](net.md)'s (the URL plus
  the expected digest), not a label. The reference cached
  `dlssnr-<label>.zip` and similar by label: a publisher that replaces an
  asset under the same tag is served the old one forever.
- **The mirror is accepted** as the single source of the neural-rendering
  runtime and the RenoDX add-ons, because no publisher offers them elsewhere.
  A `nvngx_dlssnr.dll` from it is placed only when it carries NVIDIA's
  Authenticode signature (net's signer kind): the mirror republishes
  NVIDIA's file, and the signature proves it was not changed on the way.
- **Pins are data shipped with the release.** A pin changes with a release,
  so it is reviewed like code; no pins document is fetched at run time,
  which would add a trust root.
- **The frame-generation files the person downloaded** are shown with their
  SHA-256, so the person can compare it with the publisher's.

## What must never happen

- **A component placed in a game folder before its checks pass.**
- **Contacting a host off the list, or following a redirect off it**; an
  https to http downgrade (see [net](net.md)).
- **An all-users registration or any elevated write built from a file in the
  per-user cache** (see Vulkan in [routes](routes.md)): the per-user cache is
  writable by any process of that user.
- **Installing a known-faulting build silently** (the pins above).
- **A moving branch installed with no record of what it was.** The reference
  installs LumeniteFX and VORT from the head of a branch (below).
- **Running a downloaded program to unpack an archive.**
- **Treating the API's silence as "up to date".**
- **Writing a recorded version of `latest` as if it were a version** and
  then comparing it.
- **Installing a pre-release or a "test" package as the default** (the
  feeder's betas, the forks' pre-release flags, `nightly`).

## Bugs and questionable choices in the reference not to port

- **LumeniteFX and VORT are branch snapshots cached by a fixed name**
  (`LumeniteFX-mainline.zip`, `vort_Shaders-main.zip`; `sources.py:42`,
  `sources.py:101`). The first download is served forever: the shaders never
  update, whatever the day, and the version recorded is nothing.
  - Changed from the reference: resolve the branch to a commit, download
    that commit's archive, cache by commit, record the commit as the version.
    The shader headers (`slim` branch raw files, `sources.py:30`) are fetched
    on every install with no cache and no pin: same fix.
- **Newest-by-default for every add-on with no compatibility floor** (bridge,
  upstream, standalone, uplift): a new major release that changes layout is
  installed on trust. The MFG route's "shape changed -> skip with a warning"
  (`mfg.py:253`) is the right pattern; apply it to every component with a
  known file list.
- **Four fetch layers, two caching policies.** `sources._json` caches API
  answers; `net.json_get` (used by `mfg.py` and others) does not, so the MFG
  route spends two API calls per install against a 60-an-hour allowance. One
  fetcher.
- **`nightly` and "latest" special cases** spread over three modules: the tag
  `nightly` dropped for forks, the version `latest` skipped in the check, the
  name `nightly` allowed in `is_package_tag`. One notion of "not a comparable
  version".
- **Content sniffing to recognise our own files** (`is_dxvk`: the bytes
  `DXVK` in a file over 1 MB; `is_optiscaler`: `OptiScaler.dll` in UTF-16 in a
  file over 1 MB; `is_loader`: `Ultimate ASI Loader`; `is_reframework`;
  `_is_reshade`: `ReShade` in a file over 1 MB). Each reads whole files of
  tens of megabytes into memory. Prefer the install record for "ours"; read a
  PE version resource (`OriginalFilename`) rather than scanning bytes where
  content must be sniffed.
- **The preview's `_cached_zip_members` and `archive_name`** exist because the
  version is not known without a request; with a resolver that has a typed
  result (tag, asset, url, digest) and a cache the preview can read, they go
  away.
- **The user agent says `dlss5-autopilot/1.3.1`** (`sources.py:25`) whatever
  the version. Send the real one.
- **The VORT bundle is installed in full on the feeder route** while the
  LumeniteFX one is trimmed to the provider the feed reads, "because ReShade
  compiled all eight heavy effects at start-up and a 32-bit game crashed from
  the stall" (`installer.py:2889`). The same stall risk applies to VORT's
  other effects; check the file list (see Q1).
- **`OptiScaler.ini`'s own update notice** was switched off in the install
  without saying so. It stays off, because this tool keeps the build current,
  and the install report now says so ([routes](routes.md)).

## To verify against primary sources

| Claim | Source |
| --- | --- |
| The reshade.me home page link pattern, and that the add-on installer is not a release asset on GitHub | reshade.me; `crosire/reshade` releases; whether the installer is Authenticode-signed and by whom |
| That GitHub's release API exposes a per-asset `digest` (SHA-256) and that `github.com` release downloads redirect only to GitHub-owned hosts | GitHub REST docs for releases; observe a redirect chain |
| Anonymous API limit of 60 requests an hour per address, and `403` or `429` as the rate-limit answer | GitHub rate-limit docs |
| That `releases/expanded_assets/<tag>` and the releases list page stay stable enough as a fallback (HTML scraping) | observe them; GitHub offers no contract. A pinned fixture catches a change |
| That `github.com/<repo>/releases/latest` redirects to `/tag/<tag>` and a `HEAD` is enough | observe |
| That NVIDIA's SDK repository keeps the three runtimes at `lib/Windows_x86_64/rel/` for every tag, and that the files are signed by NVIDIA | `NVIDIA/DLSS` tree and `signtool`/`Get-AuthenticodeSignature` on the real files |
| That the mirror's tag prefixes (`renodx-dlss5-`, `renodx-dlss-SF-`, `dlssnr-`, `dlssg-`, `dlss-`) and one `.zip` per release are stable | `RankFTW/rhi-repo` releases |
| That each fork still publishes releases in the shape the skip lists assume (package word `optiscaler`, `rtx40-mfg`, `-test-`, `with_dlss`) and which carry a SHA256SUMS | each fork's release page and licence (OptiScaler is GPL-3.0; the tool never bundles it) |
| That `doitsujin/dxvk` assets are `dxvk-<v>.tar.gz` and `dxvk-native-<v>-*.tar.gz` and the archive layout `x64/`, `x32/`; that DXVK ships `d3d8.dll` | DXVK releases and README |
| That `REFramework.zip` of the nightly repository carries one universal `dinput8.dll` | `praydog/REFramework-nightly` |
| That SharpCompress opens the real OptiScaler `.7z` archives (BCJ2 filters) | see [net](net.md) |
| That the mirror's `nvngx_dlssnr.dll` builds carry NVIDIA's Authenticode signature | `Get-AuthenticodeSignature` on each build |
| Licence of each component, and that none is redistributed | each repository's licence; the tool's NOTICE |
| That GITC-Uplift's SHA256SUMS.txt format is `<hash>  <name>` and names every file | a release |
| Whether `RTX40MFG` / ASI Loader v10.x can replace the pins | the two projects' notes |

## Fixtures

- **Release answers**, as the API shapes: one JSON file per repository with
  the cases the rules above handle. From the reference tests and bug reports:
  - the feeder list with a stable `v0.15.1` below six `1.16.0-beta.x` tags
    whose `prerelease` flag is false (#325, #348), and a list of only betas;
  - the OptiScaler fork pages, including wilsjo2's release holding
    `display-filter-v0.2.0-preview` (`_tools/reports/364.txt`), a release with
    both `OptiScaler-NR-v<x>.zip` and `...-rtx40-mfg.zip` (#196, #231), y4my4my4m's
    `.7z` with a `with_dlss` sibling and a `nightly` tag, Janblade's `-test-`
    zip, and Dagherbou's `/latest` with no package;
  - the mirror's release list with tags `renodx-dlss5-4.55`, `-4.60`,
    `-5.2.1`, `renodx-dlss-SF-...`, `dlssnr-310.8.0`, `-310.8.0-RTX40`,
    `-310.8.SF`, `-310.8.SF-v2`, `dlssg-...`, `dlss-...`, with and without a
    pinned build present;
  - GITC-Uplift with a draft, a pre-release, a release without the zip, and a
    `SHA256SUMS.txt`;
  - the standalone release in its three shapes (loose assets, `-64-bit.zip`,
    neither) and with `StandaloneBoundary.fx`;
  - a rate-limited answer (403 and 429 with GitHub's body), a 5xx, an HTML
    captive-portal page for a `.zip` (#140).
- **github.com pages**: a releases list page (about 400 KB) trimmed to the tag
  anchors, an `expanded_assets` fragment, a `releases/latest` redirect.
- **The reshade.me page**: normal, a 500 with the link in its body, a 500 with
  none, and a version bump.
- **Package archives**: a tiny zip with `OptiScaler.dll`, one without it
  (display-filter), one wrapped in a single folder; a `.7z` for the extraction
  path; a trimmed Uplift zip with its sums; a DXVK `tar.gz` with `x64/` and
  `x32/`; PE stubs (`tests/fake_pe.py` in the reference tree) for the
  raw-DLL checks.
- **Install records** for the staleness check: components with each label
  shape (`4.55`, `310.8.0-RTX40`, `latest`, `v0.15.1`, `nightly`), a record
  from before components were recorded (notes only), a rolled-back one, one
  with a missing proxy, a recorded tool version older than 1.9.1.
- **The reference's own tests:** `git show d5801a3^:test_all.py` sections
  "8. route rules, version pins and the OptiScaler dials", "21. the
  neural-upstream route", "22. FSR and XeSS games through OptiScaler", "23. the
  standalone-dlssnr route", "24. only the selected motion-vector shader is
  installed". Hints at behaviour only.
- **Real reports:** `_tools/reports/` 175 (the API answered with a certificate
  for another name), 364 (the wrong OptiScaler package), 434 (a blocked
  socket), 140-era "not a zip" cases, 031 and 037 (Vulkan and renodx pins).

## Open questions

- **Q1.** VORT full install versus a trimmed one (see above): is the
  compile-stall risk real for the standalone route's 64-bit games, and is the
  trim safe?
