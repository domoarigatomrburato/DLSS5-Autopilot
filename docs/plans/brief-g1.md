# Brief: M4 sub-task g1 — route plans, part 1 (plan infrastructure, the ReShade base, native)

Repository: `/Users/alessandroburato/coding/personal/invidia-claritatis`, branch `main`, work in place
(no worktree, no commits, no branch switches). Read `AGENTS.md` first: layering, the gate, the test
rules. Ask `main` with `SendMessage` whenever something here is ambiguous or contradicts the code or a
spec; never guess.

## Context

M4 (see `docs/plans/rewrite.md`, M4 and its sub-tasks) built the install machinery: the record
(`Install/InstallRecord`, `InstallRecords`), the transaction (`InstallTransaction`, `InstallRun`), the
resolver (`InstallResolver`, `FileSteps`, `ResolvedInstall`), refusals and the preview
(`InstallRefusals`, `RouteRefusals`, `FileRefusals`, `GatheredRefusals`, `InstallPreview`), uninstall,
shared layers and the graphics preference (`RegisterLayer`, `GraphicsPreferenceSteps`). What does not
exist yet is **the code that builds an `InstallPlan` for a route**: today every plan is built by hand
in tests. Sub-task g writes the plans of the ReShade base and of native, upstream, bridge and renodx;
h the rest. g is split in two; this is **g1**. Read `docs/plans/handoff.md` ("Where things are" and
"For g and h").

Specs to read (they bind; implementation never edits `docs/spec/`):

- `docs/spec/routes.md`: "What each route installs" (the "Every ReShade route" list, the native row,
  "Route-switch purging"), "Translation layers and Vulkan" (the Vulkan layer part only), the settings
  tables, "Pre-install checks and warnings", "What must never happen".
- `docs/spec/install.md`: "Order of an install", "Refusal checks" (the proxy-name row), "The 'what
  will happen?' preview", "Backups of the game's own files", "Reinstall and update", "Switching
  routes", "Moved-aside files" (the compiler, the hand-installed OptiScaler, the older bridge add-on).
- `docs/spec/components.md`: "The catalog", "How a version is resolved", "Pins", "Integrity".
- `docs/spec/gpu.md`: the per-card `nvngx_dlssnr` choice.

Existing code to build on (read it before designing anything new):

- `Install/InstallPlan.cs` (steps, `FileSource`, `GeneratedSource`, `CachedSource`,
  `GraphicsPreferenceSteps`), `Install/InstallTransaction.cs:69` (the pattern refusal).
- `Routes/RouteComponents.cs` (which ids a route installs, `UsesDxvk`, `PlacesDlss`,
  `IsReShadeRoute`), `Routes/ShownSettings.cs`, `Routes/RouteSettings.cs` (`SettingNames.FileName`).
- `Components/ComponentRun.cs`, `Components/Fetch/ComponentFiles.cs` (`FetchedComponent`),
  `Components/Sources/*` (resolution per id), `Components/Mirror/DlssnrBuilds.cs`
  (`ChooseAsync`), `Components/Sources/ReShadeSource.cs` and the appended-zip support from b1.
- `Install/ContentMarks.cs` (ReShade, DXVK, REFramework by content), `Install/OwnedFiles.cs`,
  `Install/PriorInstall.cs`, `Routes/GameRuntimes.cs` and `RuntimeWalk.cs` (the game's own runtimes),
  `Executables/VersionResource.cs`, `Executables/Executable.cs:57` (how a System32 folder is passed in).
- Test beds: `tests/Autopilot.Core.Tests/Install/TransactionBed.cs`, `RefusalBed.cs`,
  `PreviewRuns.cs`, b2's game folders from a description and tree snapshots, the component tests'
  fake HTTP handlers.

Code navigation: LSP answers in this checkout (`findReferences`, `goToDefinition` from a use site);
grep for project-unique names; ast-grep for structural shapes.

## Settled design

1. **Where.** New folder `src/Autopilot.Core/Install/Plans/`, namespace `Autopilot.Core.Install.Plans`,
   everything `internal` unless a public seam already requires otherwise. Tests in
   `tests/Autopilot.Core.Tests/Install/Plans/`.
2. **Three pieces, kept apart:**
   - **`FolderSurvey`**: one read-only async pass over the game folder that collects everything the
     planner decides on, so the planner itself is a pure function. It reads: which of the names the
     plan may touch exist (every ReShade proxy name, every route's add-ons from the purge table, the
     older bridge add-on, `d3dcompiler_47.dll`, the hand-installed OptiScaler's files); for a file
     under the chosen proxy name, whether it is ReShade, ours on the previous record
     (`PriorInstall`/`OwnedFiles`), a hand-installed OptiScaler, or something else (`ContentMarks`);
     the game's `d3dcompiler_47.dll` file version and Windows' (System32's) version, with the
     System32 folder passed in as `Executable` does; the current text of `ReShade.ini`; where the
     game's own `nvngx_dlss.dll` and `nvngx_dlssd.dll` sit (beside the executable, nested Unreal
     folders), reusing `GameRuntimes`/`RuntimeWalk` rather than walking again. Writes nothing,
     requests nothing.
   - **`PlanGather`**: resolves and fetches the components `RouteComponents.For` names, through
     `ComponentRun` and `ComponentFiles`, into the download cache only (install.md phase 2). g1
     handles the ids its routes need (`reshade`, `renodx`, `dlssnr`, `dlss`, `dlssd`); an id it does
     not handle yet throws `NotSupportedException` naming it (h and g2 add theirs). The person's
     labels pick builds (`RenodxLabel`, `DlssnrLabel` through `DlssnrBuilds.ChooseAsync` with the
     card, `DlssLabel` or NVIDIA's newest, `RayReconstructionLabel`). A `dlssd` that fails to
     download is a skipped line in the plan's header, not a failure (routes.md). An id with several
     catalog rows carries its row (handoff, "g/h"). Its result gives each placed file's cache path
     and `FileFacts` (hashed once here) and each component's version label for the header.
   - **`RoutePlans`**: `For(...)` builds the `InstallPlan` from the route, `RouteFacts`, the settings
     read through `ShownSettings.Reset`, the survey and a **files source**. Two files sources:
     the gathered one (cache paths and facts from `PlanGather`) and the **preview** one, which knows
     names only. The preview must stay offline and write nothing (install.md, the preview), so its
     `PlaceFile` steps carry a new `PendingSource` (a `FileSource` that throws
     `InvalidOperationException` if opened) and unknown facts. Generalise `InstallPlan.HasPatterns`
     into one property that is true when any step is a pattern **or** a pending file, and make the
     transaction refuse such a plan exactly as it refuses patterns today; keep the preview's pattern
     lines unchanged. The free-space check counts only known sizes in the preview; it runs again on
     the gathered plan before phase 3 (wiring that re-run is h's, with patterns).
3. **Config files** (`ReShade.ini` now; `dlss5-bridge.cfg`, `OptiScaler.ini`, `dlss5-feed.cfg` in
   later sub-tasks): generated as `GeneratedSource` with `EntryClass.Config`, from the current text in
   the survey, changing only the keys the spec lists and keeping every other line, comment, section
   and key order byte for byte. No .NET or NuGet package edits INI text while preserving it
   (`Microsoft.Extensions.Configuration.Ini` only reads), so write one small line-based editor in
   `Plans/` (set a key; set a key only when absent; keep everything else), shared by every config
   file; say in its summary why it is hand-written. Line endings: keep the file's own; CRLF for a new
   file.
4. **The ReShade base** (every ReShade route, routes.md "Every ReShade route"): proxy name from the
   setting else the spec's default by API; for a Vulkan game (including one going through DXVK, which
   g2 wires) the `RegisterLayer` step instead of a proxy; any other ReShade proxy moved out of
   ReShade's reach (ours held by the transaction's own rules since the plan no longer places it; one
   not on the record `MoveAside` with `BackupKind.Backup`); the purge table's add-ons of the other
   routes (and `renodx-dlss5.addon64` where the spec says); the older bridge add-on moved aside; the
   game's `d3dcompiler_47.dll` moved aside with `BackupKind.Dlss5Off` only when older than Windows'
   (both versions go to the preview/report notes); `ReShade.ini` (`[ADDON] AddonPath=.\`, the add-on
   switch only when absent, the overlay key); `nvngx_dlssnr.dll` beside the executable;
   `nvngx_dlss.dll` per the spec's rules (keep the game's own, nested Unreal copy swapped too, not on
   routes where `PlacesDlss` is false); `nvngx_dlssd.dll` swap when chosen and the game has one; the
   graphics preference steps through `GraphicsPreferenceSteps.Of`. The VR headset (OpenXR) option is
   out of scope (M8): read it nowhere.
5. **Native** (routes.md native row): the base plus `renodx-dlss5.addon64` (or the person's local
   add-on file, `RenodxFile`, already refused by `FileRefusals.LocalAddOnAsync` when unusable). No
   DXVK in g1: a native plan whose game `UsesDxvk` throws `NotSupportedException` until g2.
6. **Groups and the header.** One `PlanGroup` per component or route-specific step, in a fixed order
   the tests pin; names, notes, warnings and skipped lines from `Resources/Strings.resx` through
   `Texts`, in the style of the existing entries. The header's `Components` maps id to version label;
   `Choices` is the settings as read (reset).
7. **Reinstall and switching.** A plan names with `KeepEntry` every previous entry it keeps as it is
   (install.md "Reinstall and update"); a re-placed file is a `PlaceFile` (the resolver decides
   replace-ours vs back-up). Do not reimplement hold-until-commit or backups: the resolver and the
   transaction own them.
8. **Refusal: the proxy name is taken** (install.md refusal table; handoff "Refusals still to
   write"): a file under the chosen proxy name that is neither ReShade, nor ours from the previous
   route, nor a hand-installed OptiScaler refuses the install with the spec's wording, naming what
   `ContentMarks` recognised (DXVK, REFramework) when it did. A hand-installed OptiScaler is moved
   aside with its ini and forwarder (`MoveAside`, `BackupKind.Backup`) with a note. The check reads
   the survey and runs in phase 1 through `RouteRefusals`/`FileRefusals`, with a new `BlockerKind`.
9. **Overlay key.** Stays per game in `RouteSettings.OverlayKey`: the install reads and records the
   value it gets; the app keeps one global value and fills it in (M7). Nothing to change beyond
   reading it.

## Tests first

Write the tests before the code; they describe what the install does for the person, at the plan's
seam (`RoutePlans`, the survey, the gather) through `InternalsVisibleTo`. At least:

- every ReShade-route base rule above on game folders described by the b2 beds: proxy name per API
  and per setting; Vulkan game gets the layer and no proxy; a second ReShade under another name
  moved aside (recorded: held; unrecorded: backed up); other routes' add-ons purged; the older
  bridge add-on moved aside; the compiler moved aside only when older, kept when as new or newer;
  `ReShade.ini` merge keeps the person's lines and a deliberate `NeuralUplift=0`; `nvngx_dlss` keep
  vs swap vs nested; `dlssd` swapped, absent, download failed (skipped line);
- native: the add-on placed, the local add-on file placed instead when set;
- the proxy-name refusal: DXVK, REFramework, an unknown DLL refuse; ReShade, ours, a hand-installed
  OptiScaler do not (the last moved aside with its files);
- the preview plan: offline (the fake handler fails any request), names the same paths as the
  gathered plan, and the transaction refuses it;
- one gathered native plan run end to end through the transaction bed on a described folder, then
  uninstalled, leaving the folder byte for byte as before (tree snapshots);
- the gather against the fake handler: the right builds per label and card; a missing required file
  refused before anything is placed.

## Stop sentence

If an existing test or digest contradicts a rule of this spec, stop that item and report the conflict
instead of adjusting the fixture, the tolerance or the pin. Exempt: the tests that assert
`InstallPlan.HasPatterns` or the transaction's pattern refusal may move to the generalised property
and its message, keeping what they assert.

## Checks

At most one heavy build runs on this machine at a time: prefix every `dotnet` command with
`lockf -k /private/tmp/claude-501/-Users-alessandroburato-coding-personal/b2fc9f2a-eb89-4cde-bc03-7fea57d0dcf4/scratchpad/dotnet.lock`.

- While iterating: `dotnet test --project tests/Autopilot.Core.Tests -- --filter "FullyQualifiedName~Plans"`
  (and the other classes you touch).
- Once, at the end: `dotnet run gate.cs -- fix`. Never lower a limit, a severity or the coverage floor
  (CA1502 10, CA1506 40 per type and 20 per method, applied to tests too; floor 98).

Keep the diff to a few thousand lines at most; if g1 grows past that, stop at a coherent point and
report what is left.

## Report

Reply to `main` with: what was built (files, types), each settled point above and how it landed,
anything you decided that the brief did not settle (with the reason), the gate's result (tests,
coverage), and the tree fingerprint:
`{ git diff HEAD; git ls-files -o --exclude-standard | xargs cat; } | shasum`.
