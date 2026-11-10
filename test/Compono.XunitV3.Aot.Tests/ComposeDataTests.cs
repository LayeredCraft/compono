using System;
using System.Threading.Tasks;
using Compono;
using Compono.XunitV3.Aot;
using Compono.XunitV3.Aot.Tests.Support;
using Xunit;
using Xunit.Sdk;

namespace Compono.XunitV3.Aot.Tests;

// Reduced real-runner coverage for [ComposeData] under the real xUnit Native AOT runner -
// ADR-0070 / PLAN-0071 Pass 1, B6.2 and the single added B6.3 Explicit theory. These are ordinary
// [Theory]/[Fact] methods the runner executes directly (unlike ComposeDataEmissionTests, whose
// failure/metadata targets are plain [ComposeData] members driven through the harness). Print
// fixture infrastructure (RequireFixtureIdFixture / FixtureId / IdFixtureProfile) is reused from
// RequireFixtureTests.cs - no new fixture or profile types are introduced here.
//
// Assembly fixtures are deliberately omitted: xUnit's own AOT source generator emits invalid code
// (CS1593) for [assembly: AssemblyFixture(...)] as of xUnit 4.0.1 (upstream xunit/xunit#3664, fixed
// in 4.0.3-pre.19), which is the version pinned by xunit.v3.aot.mtp-v2. That is an xUnit generator
// defect, not Compono - class and collection fixtures are fully supported and covered below, and
// assembly-fixture coverage lives in the JIT project.

// Class A: a class fixture, plus the mixed [ComposeData] + [InlineData] theory. The row-source
// member reads the class fixture and records the pipeline stage it observes, proving fixtures exist
// and the member is read at TestMethodExecution, not discovery.
public sealed class ComposeDataClassFixtureTests : IClassFixture<RequireFixtureIdFixture>
{
    private readonly RequireFixtureIdFixture _fixture;

    public ComposeDataClassFixtureTests(RequireFixtureIdFixture fixture) => _fixture = fixture;

    public static TestPipelineStage ObservedStage { get; private set; }

    public static object?[][] Rows()
    {
        ObservedStage = TestContext.Current.PipelineStage;
        var id = TestContext.Current.RequireFixture<RequireFixtureIdFixture>().Id;
        return [[id, "first"], [id, "second"]];
    }

    [Theory]
    [ComposeData<IdFixtureProfile>(nameof(Rows))]
    public void RowValuesAndComposedValueReadTheClassFixture(int rowId, string label, FixtureId composed)
    {
        _ = label;
        Assert.Equal(_fixture.Id, rowId);
        Assert.Equal(_fixture.Id, composed.Value);
        Assert.Equal(TestPipelineStage.TestMethodExecution, ObservedStage);
    }

    public static readonly object?[][] MixedRows = [[1, "one"], [2, "two"]];

    [Theory]
    [ComposeData(nameof(MixedRows))]
    [InlineData(99, "inline")]
    public void ComposeDataRowsAndInlineDataRowsBothFeedTheTheory(int value, string name)
    {
        var expected = value switch
        {
            1 => "one",
            2 => "two",
            99 => "inline",
            _ => throw new InvalidOperationException($"Unexpected value {value}."),
        };

        Assert.Equal(expected, name);
    }
}

// Class B: a collection fixture of the same type as Class A's class fixture. It declares a different
// fixture scope, so it receives its own distinct instance of RequireFixtureIdFixture (each instance is
// assigned a unique Id). Each class asserting row-supplied id == its own injected fixture id == the
// composed FixtureId is the isolation evidence: a shared or leaked fixture instance would make one of
// those assertions fail.
[CollectionDefinition("ComposeDataIdFixtureCollection")]
public sealed class ComposeDataIdFixtureCollectionDefinition : ICollectionFixture<RequireFixtureIdFixture>
{
}

[Collection("ComposeDataIdFixtureCollection")]
public sealed class ComposeDataCollectionFixtureTests
{
    public static TestPipelineStage ObservedStage { get; private set; }

    public static object?[][] Rows()
    {
        ObservedStage = TestContext.Current.PipelineStage;
        var id = TestContext.Current.RequireFixture<RequireFixtureIdFixture>().Id;
        return [[id, "first"], [id, "second"]];
    }

    [Theory]
    [ComposeData<IdFixtureProfile>(nameof(Rows))]
    public void RowValuesAndComposedValueReadTheCollectionFixture(int rowId, string label, FixtureId composed)
    {
        _ = label;
        var fixture = TestContext.Current.RequireFixture<RequireFixtureIdFixture>();
        Assert.Equal(fixture.Id, rowId);
        Assert.Equal(fixture.Id, composed.Value);
        Assert.Equal(TestPipelineStage.TestMethodExecution, ObservedStage);
    }
}

// B6.3: the real runner must honor a row's Explicit flag. The body fails if it ever runs, so a green
// run with the case reported as not-run is the proof.
public sealed class ComposeDataExplicitTests
{
    public static readonly object?[][] Rows = [[1], [2]];

    [Theory]
    [ComposeData(nameof(Rows), Explicit = true)]
    public void ExplicitRowsAreNotRunByDefault(int value) =>
        Assert.Fail($"Explicit row ran with {value}.");
}

// Disposal ownership boundary: the generated AOT factory ignores the DisposalTracker it is handed
// (the registration's `factory: static _ => ...`), so neither row-supplied nor Compono-composed row
// values are registered for disposal. A caller-owned tracker must therefore come back empty and
// dispose nothing.
public sealed class ComposeDataTrackedDisposable : IDisposable
{
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}

public sealed class ComposeDataDisposalProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new ComposeDataTrackedDisposable());
}

public sealed class ComposeDataDisposalTargets
{
    public static object?[][] DisposalRows() => [[new ComposeDataTrackedDisposable()]];

    [ComposeData<ComposeDataDisposalProfile>(nameof(DisposalRows))]
    public void Disposal_target(ComposeDataTrackedDisposable rowSupplied, ComposeDataTrackedDisposable composed)
    {
        _ = rowSupplied;
        _ = composed;
    }
}

public sealed class ComposeDataDisposalBoundaryTests
{
    private const string DisposalTargetsIndex =
        "global::Compono.XunitV3.Aot.Tests.ComposeDataDisposalTargets";

    [Fact]
    public async Task RowAndComposedValuesAreNotRegisteredWithTheCallersDisposalTracker()
    {
        var tracker = new DisposalTracker();

        var rows = await RegisteredComposeDataFactory.InvokeAsync(
            DisposalTargetsIndex,
            nameof(ComposeDataDisposalTargets.Disposal_target),
            tracker);

        Assert.Empty(tracker.TrackedObjects);

        var data = Assert.Single(rows).GetData();
        var rowSupplied = Assert.IsType<ComposeDataTrackedDisposable>(data[0]);
        var composed = Assert.IsType<ComposeDataTrackedDisposable>(data[1]);

        await tracker.DisposeAsync();

        Assert.False(rowSupplied.Disposed);
        Assert.False(composed.Disposed);
    }
}
