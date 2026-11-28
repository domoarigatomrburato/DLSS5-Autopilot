# Brief: apply the /simplify fixes to M4 g1 (uncommitted)

Repository `/Users/alessandroburato/coding/personal/invidia-claritatis`, branch `main`, work in place (no worktree,
no commits, no branch switches). Read `AGENTS.md`. The tree holds g1 uncommitted (route plans: the plan
infrastructure, the ReShade base, native), gate green, fingerprint 2f32aa3b8e8dd7e37adafe56bca8433a596dae80.
g1's original brief is `brief-g1.md` beside this file: read it for the design. New code lives in
`src/Autopilot.Core/Install/Plans/` and `tests/Autopilot.Core.Tests/Install/Plans/`; see `git diff HEAD` and
`git ls-files -o --exclude-standard`. Ask `main` with `SendMessage` on any ambiguity; never guess.

Apply the items below. Behaviour stays as g1's brief says unless an item says otherwise. LSP answers in this
checkout (findReferences, goToDefinition from a use site); grep for project-unique names; ast-grep for shapes.

## Items

Survey (`FolderSurvey.cs`)
1. Drop `SurveyRequest.Runtimes`, `WithRuntimes`, `Evidence`, `SamePath`: derive the DLSS locations (beside,
   nested, dlssd) purely from `request.Facts.Runtimes` in `BaseSteps` or a small static helper, with
   `Facts.DlssBesideExecutable` for "beside". The survey keeps only disk reads. Risks: `GameRuntimesReader`
   leaves out files the record lists as ours, so the facts must be read with the record (as
   `PlanBed.SurveyAsync` does) — say so in the doc comment of whatever takes the facts; `PlanBed` must set
   `DlssBesideExecutable` from what the reader finds, or the keep/swap tests flip.
2. Build the survey in one object initializer from locals in `ReadAsync` (no placeholder record, no `with` passes).
3. Key it by relative path: the names it looks at come from a list (subfolders allowed later); config-file
   bytes live in one path→bytes map (`survey.Content(path)`), so `ReShade.ini` has no dedicated field and is
   no longer hashed into `Present`.
4. Remove `ProxyContent.Ours` (`Content` null when the file is ours). Touch points: `FileRefusals.ProxyTaken`,
   `ReShadeBase.CleanUp`, `KeptAside`.
5. Hash a present file only when the previous record lists it (`OwnedFiles.IsOursAsync` semantics: listing,
   then size, then hash); presence otherwise by `File.Exists`.
6. `ContentMarks`: one single-pass entry point returning every mark found (ASCII ReShade, DXVK, REFramework
   plus the UTF-16 OptiScaler name); remove `IsOptiScalerAsync`; priority OptiScaler > ReShade > DXVK >
   REFramework (an OptiScaler DLL may contain the text "ReShade"; ReShade never names OptiScaler.dll in
   UTF-16). New test: a file carrying both marks is OptiScaler (a new `Markers` helper; `Markers.OptiScalerDll`
   and `Markers.ReShadeDll` are pinned by `GameTreeTests`, keep them).

Dead code and shapes
7. Remove `GatheredFiles.PathOf`, `PlanDraft.Warnings`, the unused default on `GatheredFiles.Add`,
   `IniText.Get` (test the editor through its bytes).
8. `PlanFiles`: `Versions`/`Notes`/`Warnings`/`Skipped` and their writers concrete in the base class; only
   `Of`/`Content`/`Failed` abstract; one merge order in `PlanDraft.Plan` (the files' lines first, then the
   draft's) for every list.
9. `BuildFetch`: add `FetchAsync(ComponentRelease)` and drop the `Progress`/`Cancellation` pass-throughs;
   `MirrorGather`'s `paths` dictionary becomes a local; memoize the mirror catalog task once per gather in
   `BuildFetch`. CA1506 is tight (`MirrorGather.DlssnrAsync`, `PlanGather.GatherAsync` about 1 under 20).
10. `NeedsGathering` as a virtual on `PlanStep` (false by default; `PlacePattern` true; `PlaceFile` and
    `RegisterLayer` when their source is `PendingSource`).
11. `RefusalRequest.Survey` becomes a required constructor parameter (mechanical change at every
    construction site, `RefusalBed.Request` and others).
12. `RoutePlans`: add the ReShade base once when `RouteComponents.IsReShadeRoute`, then the route's own part;
    expose `PlanNames.AddOnsOf(route)` so placing and purging read the same table.

Names, one spelling each
13. The layer step reads `LayerKind.Of(layer).Library`/`.Manifest` (`Install/SharedLayers.cs:48-57`) instead of
    re-deriving the names; ReShade's four setup names defined once (on `ReShadeInstaller`) and referenced by
    `PlanNames` and `PlanBed`.
14. Names `KnownFiles` already spells (compiler, the renodx-dlss5 add-on, the old bridge add-on, OptiScaler's
    companions, optiscaler.dll) become consts there, referenced by both its sets and the plans; the purge table
    references the component sources' file-name constants where they exist (`FeederSource`,
    `ListedComponents`, `StandaloneComponent`, `ReleaseComponents`), made internal if needed, keeping the
    rows exactly as routes.md's purge table lists them.
15. `BaseSteps.IsOlder` through `ComponentVersion.CompareTo(...) == VersionOrder.Older` if `ComponentVersion`
    orders four-part file versions (10.0.17763.1 < 10.0.26100.1) correctly; check it with a test first, and if
    it does not, keep `System.Version` and say why.

Components layer
16. Move the mirror-build fetch (the raw-DLL check with its cache delete, now `BuildFetch.CheckRawAsync`, and
    the contents check) into `Components/Fetch` as `ComponentFiles.FetchBuildAsync(BuildEntry, ...)`;
    `DownloadCache.CheckContentsAsync` becomes internal. Keep the NotARuntime test
    (`PlanGatherTests.A_file_from_NVIDIA_that_is_not_a_runtime_is_deleted_from_the_cache_and_the_mirrors_copy_taken`).
17. `ComponentRelease` takes its `CatalogEntry` as a required constructor parameter (`Id` from it), no fallback
    lookup; update every builder (`ReShadeInstaller.Release`, `BuildEntry.ReleaseOf`, the sources, test
    `FetchFixtures.Release`; grep `new ComponentRelease(` and `new(id, new(`); one helper holds the
    mirror/NVIDIA row lookups (today duplicated with `NvidiaRuntimes.cs:86`).
18. `PlanGather`: a generic default for ids `ComponentSources.ResolveAsync` knows (resolve, fetch, add each
    `ExpectedFile`, record the version label and `release.Notice`), plus a small id→handler map for the
    special ones (reshade, renodx, dlssnr, dlss, dlssd). Supported ids = the map's keys plus the generic ids
    g1's routes need (none today beyond the map: keep `NotSupportedException` for the others until g2/h,
    but the generic path must be there, with one test on a fake component it resolves).
19. `dlssnr` on Auto: skip pinned builds whose `DlssnrPin.Sm` excludes the card before downloading them
    (unless `PlaceMismatch`), still reported as skipped (handoff.md, "h, wiring DlssnrBuilds.ChooseAsync" —
    it lands now). For a pinned build, take its facts from the pin's digest plus the file length instead of
    hashing again (the contents check already proved the digest). Tests: the bed's mirror list
    (`GatherBed.UsualMirror`) needs a pinned tag (e.g. `dlssnr-310.8.0-RTX40`, sm [89, 120]) and a card the pin
    excludes; for the facts path, use an internal seam or a pin the test controls, since no test bytes hash
    to a real pin.
20. Kept small content (the layer manifests): facts from the bytes already in memory, one read.

Tests
21. `GatherBed`: `NvidiaFiles.MinRawSize` instead of `200_000`; `NvidiaRuntimes.Folder` (make it internal)
    instead of the literal path; `UninstallWithoutRecordTests.Big` → `Markers.Carrying`.

## Stop sentence

If an existing test or digest contradicts a rule of this brief, stop that item and report the conflict instead
of adjusting the fixture, the tolerance or the pin. Exempt: tests whose only change is a renamed or moved
member, a constructor parameter added, or the bytes of `Markers` helpers, as the items above describe.

## Checks

Prefix every `dotnet` command with
`lockf -k /private/tmp/claude-501/-Users-alessandroburato-coding-personal/b2fc9f2a-eb89-4cde-bc03-7fea57d0dcf4/scratchpad/dotnet.lock`.
While iterating: `dotnet test --project tests/Autopilot.Core.Tests -- --filter "FullyQualifiedName~<Class>"`.
Once at the end: `dotnet run gate.cs -- fix`. Never lower a limit, a severity or the coverage floor (CA1502 10,
CA1506 40 per type and 20 per method, tests included; floor 98; coverage is 98.02% now, so removed code
must not strand untested lines). Budget your tool calls: if you approach your cap, stop at a coherent
point (focused tests green) and report what is left rather than leaving a broken tree.

## Report

Reply to `main`: per item, done (how) or skipped (why); anything you decided; the gate's result (tests,
coverage); the fingerprint `{ git diff HEAD; git ls-files -o --exclude-standard | xargs cat; } | shasum`.
