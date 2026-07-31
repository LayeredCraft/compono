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

        // Always "-f net10.0" above regardless of whether *this* test is itself running under the
        // net10.0 or net11.0 host - CI runs both concurrently, so two RealRunnerTests instances can
        // each spawn this exact nested command at the same moment (PR #26 review, second/third
        // rounds). The sample project's own RestorePackagesPath isolates each restore by this
        // environment variable rather than $(TargetFramework) - the latter is the same "net10.0" for
        // both processes here, so it never actually distinguished them, unlike a value computed once
        // in this C# process and inherited stably by the whole child process tree (including every
        // internal MSBuild restore/build re-evaluation the nested `dotnet test` performs).
        startInfo.Environment["Compono_LocalPackagesId"] = Guid.NewGuid().ToString("N");

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        var exited = process.WaitForExit(TimeSpan.FromMinutes(5));

        exited.Should().BeTrue("the sample project's own dotnet test run should complete well within this timeout");

        var combinedOutput = output + error;

        // FailingCompositionTests.DeliberatelyFailingComposition_NoProviderCanSatisfyTheNestedInterfaceDependency
        // is the one deliberately-failing theory in the sample project (a genuine, pipeline-propagated
        // composition failure, not one of Compono.XunitV3's own pre-composition validation failures) -
        // everything else there is shaped to pass. Its explicit Seed = 24601 is what makes this
        // assertion deterministic rather than needing to parse an auto-generated seed out of the
        // subprocess's own console output.
        combinedOutput.Should().Contain("total: 7");
        combinedOutput.Should().Contain("failed: 1");
        combinedOutput.Should().Contain("succeeded: 6");
        combinedOutput.Should().Contain("Seed: 24601");
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
