using System.Diagnostics;

namespace Compono.XunitV3.Tests;

// The milestone's required "at least one test suite must prove behavior through the real xUnit v3
// discovery and execution pipeline" coverage (ADR-0022's Testing Strategy) - every other test in this
// project calls GetData directly, which never exercises SupportsDiscoveryEnumeration()'s actual
// effect on discovery/execution sequencing, real theory-row rendering, or a real MTP process exit
// code. This shells out `dotnet test` against test/Compono.XunitV3.SampleTests - a genuinely separate
// project consuming Compono.XunitV3 as a packaged dependency (never a ProjectReference) - and asserts
// on that real process's captured output.
public sealed class RealRunnerTests
{
    [Fact]
    public void DotnetTest_OnTheSampleProject_ReportsTheExpectedPassFailSplit_AndSurfacesTheFailingSeed()
    {
        var sampleProjectDirectory = FindSampleProjectDirectory();

        var startInfo = new ProcessStartInfo("dotnet", "test -f net10.0")
        {
            WorkingDirectory = sampleProjectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        var exited = process.WaitForExit(TimeSpan.FromMinutes(5));

        exited.Should().BeTrue("the sample project's own dotnet test run should complete well within this timeout");

        var combinedOutput = output + error;

        // FailingCompositionTests.DeliberatelyFailingComposition_NegativeSeedIsRejected is the one
        // deliberately-failing theory in the sample project - everything else there is shaped to pass.
        combinedOutput.Should().Contain("total: 6");
        combinedOutput.Should().Contain("failed: 1");
        combinedOutput.Should().Contain("succeeded: 5");
        combinedOutput.Should().Contain("Seed: -1");
        process.ExitCode.Should().NotBe(0, "the sample project's own deliberately-failing theory makes its dotnet test process report failure");
    }

    // Walks up from this test assembly's own output directory to the repo root (identified by
    // Compono.slnx, which only exists there) rather than a relative "../../.." path from bin/<config>/
    // <tfm>/ - robust to which TFM/configuration this test itself happens to be running under.
    private static string FindSampleProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Compono.slnx")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Could not locate the repository root (Compono.slnx) above " + AppContext.BaseDirectory);

        return Path.Combine(directory.FullName, "test", "Compono.XunitV3.SampleTests");
    }
}
