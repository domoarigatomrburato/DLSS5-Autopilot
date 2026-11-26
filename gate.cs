#:property RestorePackagesWithLockFile=false
// The root entry point: dotnet run gate.cs -- <fix|check|build>
//   fix    format the solution.
//   check  everything that must pass before a commit; changes nothing.
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

    private static Step[] FixSteps() =>
    [
        new("format", () => Run("dotnet", "format", Solution)),
    ];

    private static Step[] CheckSteps() =>
    [
        new("restore, locked", () => Run("dotnet", "restore", Solution, "--locked-mode")),
        new("build, warnings are errors", () => Run("dotnet", "build", Solution, "--no-restore")),
        new("format, verify", () => Run("dotnet", "format", Solution, "--verify-no-changes", "--no-restore")),
        new("tests, with coverage", RunTests),
        new("coverage floor", CheckCoverageFloor),
        new("typos", () => Run("typos")),
    ];

    private static Step[] BuildSteps() =>
    [
        new("restore, locked", () => Run("dotnet", "restore", Solution, "--locked-mode")),
        new("build, Release", () => Run("dotnet", "build", Solution, "--configuration", "Release", "--no-restore")),
    ];

    private static bool Run(string fileName, params string[] arguments)
    {
        ProcessStartInfo start = new(fileName, arguments);
        // Test runs send nothing to Microsoft.
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["TESTINGPLATFORM_TELEMETRY_OPTOUT"] = "1";

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
