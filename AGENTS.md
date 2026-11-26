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
| `tests/Autopilot.<Project>.Tests` | one test project per `src` project except Windows |
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
| `dotnet run gate.cs -- fix` | formats the solution (`dotnet format`) | after edits |
| `dotnet run gate.cs -- check` | locked restore, build with warnings as errors, format verify, tests with the coverage floor, `typos`; changes nothing | before a commit |
| `dotnet run gate.cs -- build` | Release build | when a build is the point |

Do not re-run `check` on a tree that has not changed since it passed.

Everything blocks: warnings are errors, nullable is on, every analyzer is on
(`AnalysisLevel` `latest-all`) and every rule left out is named in
`.editorconfig` with its reason, complexity limits live in
`CodeMetricsConfig.txt`, and the package lock files are enforced. After a
package change, run `dotnet restore Autopilot.slnx --force-evaluate` and commit
the lock files. Never lower a limit, a severity or the coverage floor to make
a gate pass; the floor in `coverage-floor.txt` only rises.

## Tests

MSTest on Microsoft.Testing.Platform; run one project with
`dotnet test --project tests/<name>`.

- Tests describe what the tool does for the user, at a project's public seam,
  not how the code is arranged.
- Every test has 5 seconds by default (`tests/testconfig.json`); raise it on
  one test with `[Timeout]` and the reason beside it.
- Unit tests are offline: each test project routes `HttpClient` to a dead
  proxy (`tests/Shared/NoNetwork.cs`), so a stray request fails instead of
  reaching a server. It is a backstop: code that talks to the network takes
  its `HttpMessageHandler` from the caller, and tests pass a fake one.
- UI tests run headless (Avalonia.Headless) and open no window.
