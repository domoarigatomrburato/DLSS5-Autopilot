# Rewrite in C# and Avalonia

The tool is rewritten from scratch. The Python app at the tag
`python-reference` says **what** the tool must do for the person using it;
nothing in it says **how**. Its structure, names, workarounds and comments
are not carried over. It was written without engineering review, so every
behaviour taken from it is checked against primary sources (the publishers'
docs, the file formats, Windows' documented behaviour) before it is
specified.

## Decisions

- **Stack.** C# on .NET 11 and Avalonia for the UI, with MVVM through
  CommunityToolkit.Mvvm. .NET 11 is supported until November 2028, as long
  as .NET 10. Every component stays on its current release: SDK, packages
  and actions are bumped when a new version ships.
- **Cross-platform development, Windows product.** The engine, the view
  models and the UI build, run and are tested on macOS and Linux. Windows
  APIs (registry, Win32, Authenticode, notifications, tray) sit behind
  interfaces, with the Windows implementations in their own project; the
  platform analyzer (CA1416) keeps Windows calls inside it. Real games are
  checked on Windows only.
- **Layers are projects.** Project references enforce the layering, so no
  import-linter is needed:

  | Project | Holds | May reference |
  | --- | --- | --- |
  | `Autopilot.Core` | the engine: detection, routes, install and uninstall, downloads, diagnosis | nothing of ours |
  | `Autopilot.Windows` | the Windows implementations of Core's interfaces | Core |
  | `Autopilot.ViewModels` | screens as view models, no UI types | Core |
  | `Autopilot.App` | Avalonia views and the composition root | all of the above |

  The view models never reference Avalonia, so a later move to WinUI costs
  only the views.
- **Tests describe what the tool does for the user**, at the public seam of
  each project: what files an install writes, backs up and restores; the
  route a game gets; the verdict a log yields. Unit tests run offline and
  open no window. Every test has a timeout, 5 seconds by default, raised per
  test with the reason beside it. UI tests run headless.
- **Everything blocks from the first commit.** Nothing is grandfathered,
  because there is nothing to grandfather:
  - warnings are errors;
  - nullable reference types are on;
  - every analyzer is on (`AnalysisLevel` `latest-all`), with each rule
    left out named in `.editorconfig` with its reason;
  - complexity limits apply;
  - formatting is checked;
  - the package lock files are enforced;
  - the tests run with a coverage floor that only rises.
- **Idiomatic tooling only:**
  - the `dotnet` CLI and a `.slnx` solution;
  - `global.json` pinning the SDK;
  - central package management (`Directory.Packages.props`) and shared
    build settings (`Directory.Build.props`);
  - lock files restored in locked mode;
  - `dotnet format`;
  - MSTest on Microsoft.Testing.Platform, with its code coverage.

  One root entry point runs `fix`, `check` and `build`.
- **The specs come before the code.** Before an area is written, its
  behaviour is read out of the reference and written down in
  `docs/spec/<area>.md`, as what the user gets and what must never happen,
  with the reference's bugs listed so they are not ported. Fixtures that are
  data (the bug-report corpus and its verdicts, PE files, release answers)
  are stored once, in a form any test can read.
- **Security and correctness are requirements, not phases.** What the
  Python takeover found goes into the specs:
  - no https to http downgrade;
  - size limits on every download;
  - verified hashes or signers where publishers provide them;
  - absolute system paths for the system tools it runs;
  - an install recorded before it writes;
  - rollback of a partial install;
  - restores that keep their backup when they fail;
  - atomic writes that `fsync`;
  - an all-users Vulkan layer never built from a per-user cache;
  - a self-update that refuses a release without verified sums.
- **CI later.** The gate runs locally until the first milestone works end
  to end; then GitHub Actions runs it on Windows. Releases are built,
  hashed and attested in CI.
- **The compatibility feed and its page** come back in M8, served by a
  Cloudflare Worker that receives results from the app with no GitHub
  account ([community](../spec/community.md)). Until then the last published
  list stays online and nothing updates it.

## Milestones

- [x] **M0. Scaffold.**
  - [x] solution, projects, shared build settings, `.editorconfig`;
  - [x] the gate (`fix`, `check`, `build`);
  - [x] test projects with the timeout default and coverage;
  - [x] a window, shown by a headless test on macOS;
  - [x] `AGENTS.md`.
- [ ] **M1. Network and downloads** (spec: [net](../spec/net.md)): one HTTP
      path with retries, TLS, size limits, no downgrade, a download cache,
      and safe archive extraction.
- [ ] **M2. Reading games.** PE files: architecture, imports and version
      resources, which decide the graphics API. The game library: Steam,
      Epic, GOG, EA, Ubisoft, Battle.net, Rockstar, Xbox, Amazon, Heroic,
      folders added by hand, emulators (detection only). itch waits for M8.
- [ ] **M3. Routes.** Which route a game gets and why, from what was read;
      the components each route needs, and where they come from.
- [ ] **M4. Install and uninstall.** The record written first; backups;
      rollback; the preview ("what will happen?") that writes nothing;
      uninstall from the record alone. Emulator config edits wait for M8.
- [ ] **M5. DLSS files kept current.** Every game's DLSS runtimes against
      NVIDIA's newest; update and restore original.
- [ ] **M6. Diagnosis.** Logs to a verdict and a next step, driven by the
      report corpus: only the verdicts the corpus exercises; the bug-report
      text.
- [ ] **M7. The app and autopilot.** Library, game page, DLSS page,
      settings; the game watcher, the "did it work?" answer after a
      session, "try another route", and autopilot
      ([watch](../spec/watch.md)); the Windows implementations; the first
      release built in CI.
- [ ] **M8. Beyond the core.** "Aim for" and compare; RTX Remix; VR; the
      tray; the shared compatibility results (the Cloudflare Worker);
      emulator config edits; itch; verdicts outside the report corpus, as
      data; the log drawer's pop-out.
- [ ] **M9. Parity.** Every behaviour of the reference is either done or
      dropped on purpose and listed. The README is written for users.

## Dropped

Out of scope: the tool is about games.

- Video: DLSS 5 on video playback ([video](../spec/video.md) keeps the
  reference's behaviour as a record).
- YouTube.
- Webcam.
- Screen capture.
