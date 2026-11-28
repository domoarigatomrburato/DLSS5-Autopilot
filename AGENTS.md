# Invidia Claritatis

A Windows tool in C# on .NET 11 with Avalonia. The plan is
[docs/plans/rewrite.md](docs/plans/rewrite.md), whose "Decisions" bind every
change; `docs/spec/` describes each area's behaviour and implementation work
reads it without editing it.

## Layout

The projects keep their `Autopilot` names until a single mechanical rename.

| Path | Holds |
| --- | --- |
| `src/Autopilot.Core` | the engine; references nothing of ours |
| `src/Autopilot.Windows` | Windows implementations of Core's interfaces (`net11.0-windows`) |
| `src/Autopilot.ViewModels` | screens as view models; never references Avalonia |
| `src/Autopilot.App` | Avalonia views and the composition root; references Windows only on its `net11.0-windows` target |
| `tests/Autopilot.<Project>.Tests` | one test project per `src` project |
| `gate.cs` | the gate |

Project references enforce the layering: Windows and ViewModels reference
Core, App references all three. Windows APIs stay in `Autopilot.Windows`
behind Core interfaces, and `CompositionRoot` picks the implementations per
target. Launch the app (`dotnet run --project src/Autopilot.App`) only for a
manual check someone asked for.

## Gate

From the repository root, with `typos` and `gitleaks` on the PATH:

- `dotnet run gate.cs -- fix`, before every commit: applies the autofixes
  (`typos`, `dotnet format`, lock files), then scans uncommitted and staged
  changes for secrets, builds with warnings as errors and runs the tests with
  the coverage floor. Never re-run it on an unchanged tree that passed.
- `dotnet run gate.cs -- check`, in CI: the same checks without fixing, with a
  locked restore and a secret scan of the whole history.
- `dotnet run gate.cs -- build`: a Release build.

The gate turns telemetry off for its own runs; for builds outside it, set
`DOTNET_CLI_TELEMETRY_OPTOUT`, `TESTINGPLATFORM_TELEMETRY_OPTOUT` and
`AVALONIA_TELEMETRY_OPTOUT` to `1`.

What blocks:

- Warnings are errors, nullable is on, `AnalysisLevel` is `latest-all`; every
  rule left out is in `.editorconfig` with its reason, and complexity limits
  are in `CodeMetricsConfig.txt`.
- Production code takes its clock, delays and HTTP handler from the caller;
  `src/BannedSymbols.txt` bans the APIs that bypass them.
- JSON in `src/` goes through a source-generated context in a
  `*JsonContext.cs` file (reflection is off), the one place CA1506 is waived.
- Packages come from nuget.org only; commit the lock files `fix` refreshes.
- A false positive of the secret scan goes in `.gitleaksignore` by its
  fingerprint.
- Never lower a limit, a severity or the coverage floor to pass the gate;
  `coverage-floor.txt` only rises.

## Tests

MSTest on Microsoft.Testing.Platform; run one project with
`dotnet test --project tests/<name>`.

- Tests describe what the tool does for the user, at a project's public seam.
  A step with no seam of its own (the PE reader, the graphics API decision,
  the executable ranking) is tested as an internal through
  `InternalsVisibleTo`; nothing becomes public for tests.
- Each test has 5 seconds (`tests/testconfig.json`); raise one with
  `[Timeout]` and the reason beside it.
- Tests are offline: code that talks to the network takes its
  `HttpMessageHandler` from the caller and tests pass a fake one;
  `tests/Shared/NoNetwork.cs` routes any stray `HttpClient` to a dead proxy.
- UI tests run headless (Avalonia.Headless); no test opens a window.
- `Autopilot.Windows.Tests` checks the Windows implementations on real APIs
  and files: it builds on every host, so compile errors fail the gate
  anywhere, but runs only on Windows.
- Coverage leaves out sources generated under `obj/` (`coverage.config`).
