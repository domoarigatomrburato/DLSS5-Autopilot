# DLSS5 Autopilot

A Windows tool, written in C# on .NET 11 with Avalonia. The plan is in
[docs/plans/rewrite.md](docs/plans/rewrite.md) (its "Decisions" bind every
change); the behaviour of each area is in `docs/spec/`, which implementation
work reads and does not edit.

## Layout

| Path | Holds |
| --- | --- |
| `src/Autopilot.Core` | the engine; references nothing of ours |
| `src/Autopilot.Windows` | Windows implementations of Core's interfaces (`net11.0-windows`) |
| `src/Autopilot.ViewModels` | screens as view models; never references Avalonia |
| `src/Autopilot.App` | Avalonia views and the composition root; references Windows only on its `net11.0-windows` target |
| `tests/Autopilot.<Project>.Tests` | one test project per `src` project; `Autopilot.Windows.Tests` builds everywhere and runs only on Windows |
| `gate.cs` | the gate |

Layering is enforced by project references, so keep them as the table says:
Windows and ViewModels reference Core; App references all three. Windows APIs
(registry, Win32, notifications) stay in `Autopilot.Windows`, behind
interfaces in Core, and the composition root (`CompositionRoot`) picks them
per target. Tests never open a window; launch the app
(`dotnet run --project src/Autopilot.App`) only for a manual check someone
asked for.

## Gate

Run from the repository root:

| Command | What it does | When |
| --- | --- | --- |
| `dotnet run gate.cs -- fix` | applies every autofix (`typos`, `dotnet format`, lock files), then runs every check: secrets in uncommitted and staged changes, build with warnings as errors, tests with the coverage floor | before every commit, and whenever you want to know the tree is clean |
| `dotnet run gate.cs -- check` | the same checks without changing anything, with a locked restore and a secret scan of the whole history | in CI |
| `dotnet run gate.cs -- build` | Release build | when a build is the point |

Use `fix`, not `check`, locally: it fixes what can be fixed instead of
reporting it. Do not re-run it on a tree that has not changed since it passed.
It needs `typos` and `gitleaks` on the PATH.

The gate turns off .NET, test-platform and Avalonia build telemetry for its
own runs. For builds outside it, set `DOTNET_CLI_TELEMETRY_OPTOUT`,
`TESTINGPLATFORM_TELEMETRY_OPTOUT` and `AVALONIA_TELEMETRY_OPTOUT` to `1` in
your shell.

Everything blocks: warnings are errors, nullable is on, every analyzer is on
(`AnalysisLevel` `latest-all`) and every rule left out is named in
`.editorconfig` with its reason, complexity limits live in
`CodeMetricsConfig.txt`, and the package lock files are enforced (`fix`
refreshes them after a package change; commit them). Production code takes its
clock, delays and HTTP handler from the caller: `src/BannedSymbols.txt` fails
the build on the APIs that bypass them. JSON in production code goes through a
source-generated context in a `*JsonContext.cs` file (reflection is off in
`src/`), the one place CA1506 is waived. Packages come from nuget.org only
(`nuget.config`). A false positive of the secret scan goes in
`.gitleaksignore` by its fingerprint. Never lower a limit, a severity or the coverage floor to make
a gate pass; the floor in `coverage-floor.txt` only rises.

## Tests

MSTest on Microsoft.Testing.Platform; run one project with
`dotnet test --project tests/<name>`.

- Tests describe what the tool does for the user, at a project's public seam,
  not how the code is arranged.
- A step with no public seam of its own (the PE reader, the graphics API
  decision, the executable ranking) is tested as an internal through
  `InternalsVisibleTo`; nothing is made public for tests.
- Coverage counts our own code: `coverage.config` leaves out sources
  generated under `obj/`.
- Every test has 5 seconds by default (`tests/testconfig.json`); raise it on
  one test with `[Timeout]` and the reason beside it.
- Unit tests are offline: each test project routes `HttpClient` to a dead
  proxy (`tests/Shared/NoNetwork.cs`), so a stray request fails instead of
  reaching a server. It is a backstop: code that talks to the network takes
  its `HttpMessageHandler` from the caller, and tests pass a fake one.
- UI tests run headless (Avalonia.Headless) and open no window.
- `Autopilot.Windows.Tests` holds the checks of the Windows implementations,
  on real Windows APIs and files. It builds on every host, so a compile error
  fails the gate anywhere, but runs only on Windows: on other hosts the gate
  builds it without running it (its project file says how).
