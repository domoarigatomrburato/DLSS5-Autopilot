#:property RestorePackagesWithLockFile=false
// The root entry point: dotnet run gate.cs -- <fix|check|build>
//   fix    apply every autofix, then run every check; the one command before a commit.
//   check  the same checks, read-only, with locked restore and a full-history secret scan; for CI.
//   build  Release build of the solution.
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;

internal static class Gate
{
    private const string Solution = "Autopilot.slnx";
    private const string ResultsDirectory = "TestResults";
    private const string CoverageFloorFile = "coverage-floor.txt";

    public static async Task<int> Main(string[] args)
    {
        Step[] steps = args switch
        {
            ["fix"] => FixSteps(),
            ["check"] => CheckSteps(),
            ["build"] => BuildSteps(),
            _ => [],
        };

        if (steps.Length == 0 || !File.Exists(Solution))
        {
            await Console.Error.WriteLineAsync($"usage: dotnet run gate.cs -- <fix|check|build>, from the folder holding {Solution}");
            return 2;
        }

        foreach (Step step in steps)
        {
            Console.WriteLine($"==> {step.Name}");
            if (!step.Run())
            {
                await Console.Error.WriteLineAsync($"FAILED: {step.Name}");
                return 1;
            }
        }

        Console.WriteLine("OK");
        return 0;
    }

    // Cheap checks that need no build come first, so they fail fast.
    private static Step[] FixSteps() =>
    [
        new("typos, fix", () => Run("typos", "--write-changes")),
        new("secrets, uncommitted", () => Run("gitleaks", "git", "--pre-commit", "--redact", "--no-banner")),
        new("secrets, staged", () => Run("gitleaks", "git", "--pre-commit", "--staged", "--redact", "--no-banner")),
        // gate.cs is not in the solution; its style is checked when it compiles.
        new("format gate.cs", () => Run("dotnet", "format", "whitespace", "--folder", ".", "--include", "gate.cs")),
        // Restores without locked mode, so a package change refreshes the lock files.
        new("format", () => Run("dotnet", "format", Solution)),
        new("build, warnings are errors", () => Run("dotnet", "build", Solution, "--no-restore")),
        new("tests, with coverage", RunTests),
        new("coverage floor", CheckCoverageFloor),
    ];

    private static Step[] CheckSteps() =>
    [
        new("typos", () => Run("typos")),
        new("secrets, full history", () => Run("gitleaks", "git", "--redact", "--no-banner")),
        new("build, locked restore, warnings are errors", () => Run("dotnet", "build", Solution, "-p:RestoreLockedMode=true")),
        new("format gate.cs, verify", () => Run("dotnet", "format", "whitespace", "--folder", ".", "--include", "gate.cs", "--verify-no-changes")),
        // The build already fails on style and analyzer warnings; this catches what it cannot:
        // a missing final newline, line endings and the order of usings.
        new("format, verify", () => Run("dotnet", "format", Solution, "--verify-no-changes", "--no-restore")),
        new("tests, with coverage", RunTests),
        new("coverage floor", CheckCoverageFloor),
    ];

    private static Step[] BuildSteps() =>
    [
        new("build, locked restore, Release", () => Run("dotnet", "build", Solution, "--configuration", "Release", "-p:RestoreLockedMode=true")),
    ];

    private static bool Run(string fileName, params string[] arguments)
    {
        ProcessStartInfo start = new(fileName, arguments);
        // Builds and test runs send nothing to Microsoft or Avalonia.
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["TESTINGPLATFORM_TELEMETRY_OPTOUT"] = "1";
        start.Environment["AVALONIA_TELEMETRY_OPTOUT"] = "1";

        try
        {
            using Process process = Process.Start(start) ?? throw new InvalidOperationException($"{fileName} did not start");
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            Console.Error.WriteLine($"{fileName} is not installed or not on PATH");
            return false;
        }
    }

    private static bool RunTests()
    {
        // The merged report of a previous run must not be mistaken for this one.
        if (Directory.Exists(ResultsDirectory))
        {
            Directory.Delete(ResultsDirectory, recursive: true);
        }

        return Run(
            "dotnet",
            "test",
            "--solution",
            Solution,
            "--no-build",
            "--coverage",
            "--coverage-output-format",
            "cobertura",
            "--results-directory",
            ResultsDirectory);
    }

    private static bool CheckCoverageFloor()
    {
        string merged = Path.Combine(ResultsDirectory, "merged");
        string[] reports = Directory.Exists(merged) ? Directory.GetFiles(merged, "*.cobertura.xml") : [];
        if (reports.Length != 1)
        {
            Console.Error.WriteLine($"expected one merged coverage report in {ResultsDirectory}, found {reports.Length}");
            return false;
        }

        string lineRate = XDocument.Load(reports[0]).Root?.Attribute("line-rate")?.Value
            ?? throw new InvalidDataException("the coverage report has no line-rate");
        decimal covered = decimal.Parse(lineRate, CultureInfo.InvariantCulture) * 100;
        decimal floor = decimal.Parse(File.ReadAllText(CoverageFloorFile), CultureInfo.InvariantCulture);

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"line coverage {covered:F2}%, floor {floor}%"));
        if (covered < floor)
        {
            return false;
        }

        if (decimal.Floor(covered) > floor)
        {
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"coverage rose: set {CoverageFloorFile} to {decimal.Floor(covered)}"));
        }

        return true;
    }
}

internal sealed record Step(string Name, Func<bool> Run);
