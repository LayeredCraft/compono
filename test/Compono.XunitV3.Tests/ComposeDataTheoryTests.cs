namespace Compono.XunitV3.Tests;

// Real xUnit v3 runner coverage for [ComposeData] (ADR-0070, PLAN-0071 B3). Every case here runs
// through the actual xUnit pipeline, so what's proven is the real delayed-enumeration wiring - rows
// read and composed inside CreateTests() at TestMethodExecution, after fixtures exist - not an
// in-process fake. GetData-level behavior (metadata merge, seed matrix, exact failure text) lives in
// ComposeDataGetDataTests.cs.

public sealed class ComposeDataClassFixture : IAsyncLifetime
{
    public string Value { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await Task.Yield();
        Value = "composedata-class-fixture";
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed record ComposeDataFixtureValue(string Value);

public sealed class ComposeDataFixtureProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new ComposeDataFixtureValue(
            TestContext.Current.RequireFixture<ComposeDataClassFixture>().Value));
}

public sealed class ComposeDataFixtureTests : IClassFixture<ComposeDataClassFixture>
{
    private readonly ComposeDataClassFixture _fixture;

    public ComposeDataFixtureTests(ComposeDataClassFixture fixture) => _fixture = fixture;

    public static readonly object?[][] FixtureRows = [[1, "one"], [2, "two"]];

    [Theory]
    [ComposeData<ComposeDataFixtureProfile>(nameof(FixtureRows))]
    public void EachRowComposesAndReadsTheClassFixture(int value, string name, ComposeDataFixtureValue composed)
    {
        value.Should().BeOneOf(1, 2);
        name.Should().Be(value == 1 ? "one" : "two");
        composed.Value.Should().Be(_fixture.Value);
        composed.Value.Should().Be("composedata-class-fixture");
    }
}

public sealed class ComposeDataMemberFixtureTests : IClassFixture<ComposeDataClassFixture>
{
    private readonly ComposeDataClassFixture _fixture;

    public ComposeDataMemberFixtureTests(ComposeDataClassFixture fixture) => _fixture = fixture;

    // The member itself reads the fixture - proving fixtures are available at the execution-time point
    // where the row source is read, not only inside a profile registration factory.
    public static object?[][] FixtureDependentRows() =>
        [[TestContext.Current.RequireFixture<ComposeDataClassFixture>().Value]];

    [Theory]
    [ComposeData(nameof(FixtureDependentRows))]
    public void RowSourceMemberCanReadAFixtureAtExecution(string fromFixture) =>
        fromFixture.Should().Be(_fixture.Value);
}

public sealed class ComposeDataMixedSourceTests
{
    public static readonly object?[][] MixedRows = [[1, "one"], [2, "two"]];

    [Theory]
    [ComposeData(nameof(MixedRows))]
    [InlineData(99, "inline")]
    public void ComposeDataRowsAndInlineDataRowsBothFeedTheTheory(int value, string name)
    {
        switch (value)
        {
            case 1:
                name.Should().Be("one");
                break;
            case 2:
                name.Should().Be("two");
                break;
            case 99:
                // The [InlineData] row is an independent, ordinary data source - it is passed through
                // uncomposed, exactly as it was written.
                name.Should().Be("inline");
                break;
            default:
                Assert.Fail($"Unexpected value '{value}'.");
                break;
        }
    }
}

public sealed class ComposeDataExecutionProbeTests
{
    private static int _reads;

    public static int Reads => Volatile.Read(ref _reads);

    public static object?[][] ProbeRows()
    {
        Interlocked.Increment(ref _reads);
        return [[7]];
    }

    [Theory]
    [ComposeData(nameof(ProbeRows))]
    public void MemberIsReadDuringExecution(int value)
    {
        value.Should().Be(7);
        Reads.Should().BeGreaterThan(0);
    }
}

// Parallel isolation: two classes each declare their own IClassFixture<ComposeDataIsolationFixture>,
// and each fixture assigns a unique Id at construction (static Interlocked counter). Parallelization is
// left at xUnit's default (separate implicit collections run concurrently). Each test asserts the
// composed Id equals its own class's ctor-injected fixture Id - which can only hold if the per-row
// composition resolves against the running test's context rather than a process-global lookup.
public sealed class ComposeDataIsolationFixture
{
    private static int _next;

    public ComposeDataIsolationFixture() => Id = Interlocked.Increment(ref _next);

    public int Id { get; }
}

public sealed record ComposeDataIsolationValue(int Value);

public sealed class ComposeDataIsolationProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new ComposeDataIsolationValue(
            TestContext.Current.RequireFixture<ComposeDataIsolationFixture>().Id));
}

public sealed class ComposeDataIsolationTests1 : IClassFixture<ComposeDataIsolationFixture>
{
    private readonly ComposeDataIsolationFixture _fixture;

    public ComposeDataIsolationTests1(ComposeDataIsolationFixture fixture) => _fixture = fixture;

    public static readonly object?[][] Rows = [[1], [2]];

    [Theory]
    [ComposeData<ComposeDataIsolationProfile>(nameof(Rows))]
    public void ComposedValueMatchesThisClassesOwnFixture(int value, ComposeDataIsolationValue composed)
    {
        value.Should().BeOneOf(1, 2);
        composed.Value.Should().Be(_fixture.Id);
    }
}

public sealed class ComposeDataIsolationTests2 : IClassFixture<ComposeDataIsolationFixture>
{
    private readonly ComposeDataIsolationFixture _fixture;

    public ComposeDataIsolationTests2(ComposeDataIsolationFixture fixture) => _fixture = fixture;

    public static readonly object?[][] Rows = [[3], [4]];

    [Theory]
    [ComposeData<ComposeDataIsolationProfile>(nameof(Rows))]
    public void ComposedValueMatchesThisClassesOwnFixture(int value, ComposeDataIsolationValue composed)
    {
        value.Should().BeOneOf(3, 4);
        composed.Value.Should().Be(_fixture.Id);
    }
}

public sealed class ComposeDataCollectionFixture : IAsyncLifetime
{
    public string Value { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await Task.Yield();
        Value = "composedata-collection-fixture";
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

[CollectionDefinition("ComposeDataCollectionFixture")]
public sealed class ComposeDataCollectionFixtureDefinition : ICollectionFixture<ComposeDataCollectionFixture>
{
}

public sealed record ComposeDataCollectionFixtureValue(string Value);

public sealed class ComposeDataCollectionFixtureProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new ComposeDataCollectionFixtureValue(
            TestContext.Current.RequireFixture<ComposeDataCollectionFixture>().Value));
}

[Collection("ComposeDataCollectionFixture")]
public sealed class ComposeDataCollectionFixtureTests
{
    public static readonly object?[][] Rows = [[1, "one"], [2, "two"]];

    [Theory]
    [ComposeData<ComposeDataCollectionFixtureProfile>(nameof(Rows))]
    public void EachRowReadsTheCollectionFixture(int value, string name, ComposeDataCollectionFixtureValue composed)
    {
        value.Should().BeOneOf(1, 2);
        name.Should().Be(value == 1 ? "one" : "two");
        composed.Value.Should().Be("composedata-collection-fixture");
    }
}

// F3: the row-source member is read inside CreateTests() at TestMethodExecution, not at discovery. The
// member records the pipeline stage it observes; the theory asserts it, so a regression that reads the
// member earlier (discovery) fails loudly.
public sealed class ComposeDataExecutionStageTests
{
    public static TestPipelineStage ObservedStage { get; private set; }

    public static object?[][] StageRows()
    {
        ObservedStage = TestContext.Current.PipelineStage;
        return [[1]];
    }

    [Theory]
    [ComposeData(nameof(StageRows))]
    public void MemberIsReadAtTestMethodExecution(int value)
    {
        value.Should().Be(1);
        ObservedStage.Should().Be(TestPipelineStage.TestMethodExecution);
    }
}

// F4 (real runner): the runner must honor the merged row metadata. The bodies below throw if executed,
// so a green run proves the runner never invoked them. Skipped/explicit rows are expected to be reported
// as skipped, not failed.
public sealed class ComposeDataSkipAndExplicitTests
{
    public static readonly object?[][] Rows = [[1], [2]];

    public static object?[] SkippedRows() => [new TheoryDataRow(1) { Skip = "row-level skip" }];

    public static bool AlwaysFalse => false;

    public static bool AlwaysTrue => true;

    [Theory]
    [ComposeData(nameof(Rows), Skip = "attribute-level skip")]
    public void AttributeSkipIsHonored(int value) => Assert.Fail($"Should be skipped, ran with {value}.");

    [Theory]
    [ComposeData(nameof(SkippedRows))]
    public void RowSkipIsHonored(int value) => Assert.Fail($"Should be skipped, ran with {value}.");

    [Theory]
    [ComposeData(nameof(Rows), Skip = "conditional", SkipUnless = nameof(AlwaysFalse))]
    public void SkipUnlessWithFalseConditionIsHonored(int value) =>
        Assert.Fail($"Should be skipped, ran with {value}.");

    [Theory]
    [ComposeData(nameof(Rows), Skip = "conditional", SkipWhen = nameof(AlwaysTrue))]
    public void SkipWhenWithTrueConditionIsHonored(int value) =>
        Assert.Fail($"Should be skipped, ran with {value}.");

    [Theory]
    [ComposeData(nameof(Rows), Skip = "conditional", SkipUnless = nameof(AlwaysTrue))]
    public void SkipUnlessWithTrueConditionRuns(int value)
    {
        // Negative-space companion to the two conditional cases above: when the condition does not
        // trigger a skip, the theory genuinely executes.
        value.Should().BeOneOf(1, 2);
    }

    [Theory]
    [ComposeData(nameof(Rows), Explicit = true)]
    public void ExplicitRowsAreNotRunByDefault(int value) =>
        Assert.Fail($"Should be explicit-only, ran with {value}.");
}

// F4 (real runner): display name and traits that the runner actually attaches to the running test.
public sealed class ComposeDataDisplayAndTraitTests
{
    public static object?[] LabelledRows() => [new TheoryDataRow(1) { Label = "labelled-row" }];

    [Theory]
    [ComposeData(nameof(LabelledRows))]
    public void RowLabelReachesTheRunningTest(int value)
    {
        value.Should().Be(1);
        var test = TestContext.Current.Test!;
        test.TestDisplayName.Should().Contain("labelled-row");
        test.Traits.Should().ContainKey(ComposeDataRows.SeedTraitName);
    }
}

// F7: assembly-level fixture integration (the assembly fixture declared in AssemblyFixtures.cs).
public sealed record ComposeDataAssemblyFixtureValue(string Value);

public sealed class ComposeDataAssemblyFixtureProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new ComposeDataAssemblyFixtureValue(
            TestContext.Current.RequireFixture<RequireFixtureAssemblyFixture>().Value));
}

public sealed class ComposeDataAssemblyFixtureTests
{
    public static readonly object?[][] Rows = [[1], [2]];

    [Theory]
    [ComposeData<ComposeDataAssemblyFixtureProfile>(nameof(Rows))]
    public void EachRowReadsTheAssemblyFixture(int value, ComposeDataAssemblyFixtureValue composed)
    {
        value.Should().BeOneOf(1, 2);
        composed.Value.Should().Be("assembly-fixture-value");
    }
}

// F7: extend parallel isolation to four concurrent classes (the pattern established for Tests1/Tests2).
public sealed class ComposeDataIsolationTests3 : IClassFixture<ComposeDataIsolationFixture>
{
    private readonly ComposeDataIsolationFixture _fixture;

    public ComposeDataIsolationTests3(ComposeDataIsolationFixture fixture) => _fixture = fixture;

    public static readonly object?[][] Rows = [[5], [6]];

    [Theory]
    [ComposeData<ComposeDataIsolationProfile>(nameof(Rows))]
    public void ComposedValueMatchesThisClassesOwnFixture(int value, ComposeDataIsolationValue composed)
    {
        value.Should().BeOneOf(5, 6);
        composed.Value.Should().Be(_fixture.Id);
    }
}

public sealed class ComposeDataIsolationTests4 : IClassFixture<ComposeDataIsolationFixture>
{
    private readonly ComposeDataIsolationFixture _fixture;

    public ComposeDataIsolationTests4(ComposeDataIsolationFixture fixture) => _fixture = fixture;

    public static readonly object?[][] Rows = [[7], [8]];

    [Theory]
    [ComposeData<ComposeDataIsolationProfile>(nameof(Rows))]
    public void ComposedValueMatchesThisClassesOwnFixture(int value, ComposeDataIsolationValue composed)
    {
        value.Should().BeOneOf(7, 8);
        composed.Value.Should().Be(_fixture.Id);
    }
}

// A condition holder on a *different* class than any test class below, referenced via SkipType - the
// runner must resolve the named boolean property on the referenced type, not the test class.
public static class ComposeDataSkipConditions
{
    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;
}

// F4 (real runner): row-level conditional skip with SkipType + string condition values. Bodies throw if
// executed, so a green run (with these cases reported skipped) proves the runner honored the condition;
// the two companion cases run, so the class cannot pass merely because everything was skipped.
public sealed class ComposeDataRowSkipConditionTests
{
    private const string RowCondition = "row conditional";

    public static object?[] SkipUnlessFalseRows() =>
        [new TheoryDataRow(1)
        {
            Skip = RowCondition,
            SkipUnless = nameof(ComposeDataSkipConditions.AlwaysFalse),
            SkipType = typeof(ComposeDataSkipConditions),
        }];

    public static object?[] SkipWhenTrueRows() =>
        [new TheoryDataRow(1)
        {
            Skip = RowCondition,
            SkipWhen = nameof(ComposeDataSkipConditions.AlwaysTrue),
            SkipType = typeof(ComposeDataSkipConditions),
        }];

    public static object?[] SkipUnlessTrueRows() =>
        [new TheoryDataRow(1)
        {
            Skip = RowCondition,
            SkipUnless = nameof(ComposeDataSkipConditions.AlwaysTrue),
            SkipType = typeof(ComposeDataSkipConditions),
        }];

    public static object?[] SkipWhenFalseRows() =>
        [new TheoryDataRow(1)
        {
            Skip = RowCondition,
            SkipWhen = nameof(ComposeDataSkipConditions.AlwaysFalse),
            SkipType = typeof(ComposeDataSkipConditions),
        }];

    [Theory]
    [ComposeData(nameof(SkipUnlessFalseRows))]
    public void RowSkipUnlessWithFalseConditionIsHonored(int value) =>
        Assert.Fail($"Should be skipped, ran with {value}.");

    [Theory]
    [ComposeData(nameof(SkipWhenTrueRows))]
    public void RowSkipWhenWithTrueConditionIsHonored(int value) =>
        Assert.Fail($"Should be skipped, ran with {value}.");

    [Theory]
    [ComposeData(nameof(SkipUnlessTrueRows))]
    public void RowSkipUnlessWithTrueConditionRuns(int value) => value.Should().Be(1);

    [Theory]
    [ComposeData(nameof(SkipWhenFalseRows))]
    public void RowSkipWhenWithFalseConditionRuns(int value) => value.Should().Be(1);
}

// F4 (real runner): the attribute-level skip group resolving its condition through SkipType on a
// different class.
public sealed class ComposeDataAttributeSkipTypeTests
{
    public static readonly object?[][] Rows = [[1], [2]];

    [Theory]
    [ComposeData(nameof(Rows), Skip = "attribute conditional",
        SkipUnless = nameof(ComposeDataSkipConditions.AlwaysFalse), SkipType = typeof(ComposeDataSkipConditions))]
    public void AttributeSkipUnlessResolvesTheConditionOnTheReferencedClass(int value) =>
        Assert.Fail($"Should be skipped, ran with {value}.");

    [Theory]
    [ComposeData(nameof(Rows), Skip = "attribute conditional",
        SkipUnless = nameof(ComposeDataSkipConditions.AlwaysTrue), SkipType = typeof(ComposeDataSkipConditions))]
    public void AttributeSkipUnlessConditionThatDoesNotTriggerRuns(int value) => value.Should().BeOneOf(1, 2);
}

// F4 (real runner): grouped-skip precedence. The row group (set because the row's Skip is set) wins over
// the attribute group (set because the attribute's Skip is set) - even when the attribute's condition
// would not have skipped.
public sealed class ComposeDataSkipPrecedenceTests
{
    public static readonly object?[][] Rows = [[1], [2]];

    public static object?[] UnconditionallySkippedRows() => [new TheoryDataRow(1) { Skip = "row skip wins" }];

    [Theory]
    [ComposeData(nameof(UnconditionallySkippedRows), Skip = "attribute conditional",
        SkipUnless = nameof(ComposeDataSkipConditions.AlwaysTrue), SkipType = typeof(ComposeDataSkipConditions))]
    public void RowUnconditionalSkipOverridesAttributeConditionalSkip(int value) =>
        Assert.Fail($"Should be skipped, ran with {value}.");

    [Theory]
    [ComposeData(nameof(Rows), Skip = "attribute conditional",
        SkipUnless = nameof(ComposeDataSkipConditions.AlwaysTrue), SkipType = typeof(ComposeDataSkipConditions))]
    public void AttributeConditionalSkipThatDoesNotTriggerRuns(int value) => value.Should().BeOneOf(1, 2);
}

// F4 (real runner): interaction between a theory-level conditional skip and ComposeData rows. The row's
// unconditional Skip governs its own case; the theory-level condition governs when a row carries none.
public sealed class ComposeDataTheoryLevelSkipTests
{
    public static readonly object?[][] Rows = [[1], [2]];

    public static object?[] UnconditionallySkippedRows() => [new TheoryDataRow(1) { Skip = "row skip wins" }];

    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;

    [Theory(Skip = "theory conditional", SkipUnless = nameof(AlwaysTrue))]
    [ComposeData(nameof(UnconditionallySkippedRows))]
    public void RowUnconditionalSkipAppliesWhenTheoryLevelConditionDoesNotTrigger(int value) =>
        Assert.Fail($"Should be skipped, ran with {value}.");

    [Theory(Skip = "theory conditional", SkipWhen = nameof(AlwaysTrue))]
    [ComposeData(nameof(Rows))]
    public void TheoryLevelSkipWhenTrueIsHonored(int value) =>
        Assert.Fail($"Should be skipped, ran with {value}.");

    [Theory(Skip = "theory conditional", SkipUnless = nameof(AlwaysTrue))]
    [ComposeData(nameof(Rows))]
    public void TheoryLevelConditionThatDoesNotTriggerRuns(int value) => value.Should().BeOneOf(1, 2);
}

// B6.4 (ADR-0070 Amendment 4 §1, JIT side): a generic test-class base. A [ComposeData] theory declared
// on an open generic base runs once per closed subclass, and the composition root the engine uses is
// that closed base - the JIT counterpart to the AOT S6 tests (ComposeDataEmissionTests'
// IntGenericComposeTests/StringGenericComposeTests). The probe provider records the
// CompositionProviderRequest.DeclaringType each request carries, so the closed type can be asserted
// from the running test rather than inferred. JIT does support a row member declared on the generic
// base itself; the non-generic holder here (reached via MemberType) keeps this shape compatible with
// Native AOT as well, which rejects a row member declared on a generic holder under CMP0054
// (ADR-0070 Amendment 4 §2a).
public sealed class ComposeDataGenericDeclaringTypeProbeProvider : ICompositionValueProvider
{
    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context) =>
        request.RequestedType == typeof(ComposeDataGenericDeclaringTypeProbe)
            ? CompositionProviderResult.Handled(new ComposeDataGenericDeclaringTypeProbe(request.DeclaringType))
            : CompositionProviderResult.NotHandled;
}

public sealed class ComposeDataGenericDeclaringTypeProbeProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.AddSemanticProvider(new ComposeDataGenericDeclaringTypeProbeProvider());
}

public sealed record ComposeDataGenericDeclaringTypeProbe(Type? DeclaringType);

public static class ComposeDataGenericMemberHolder
{
    public static IEnumerable<object?[]> HolderRows() => [["holder"]];
}

public class ComposeDataGenericRoot
{
    public static IEnumerable<object?[]> RootRows() => [["root"]];
}

public abstract class ComposeDataGenericBase<T> : ComposeDataGenericRoot
{
    [Theory]
    [ComposeData<ComposeDataGenericDeclaringTypeProbeProfile>("HolderRows", MemberType = typeof(ComposeDataGenericMemberHolder))]
    public void InheritedMemberTypeRowSourceComposesAgainstTheClosedGenericBase(string label, ComposeDataGenericDeclaringTypeProbe probe)
    {
        label.Should().Be("holder");
        probe.DeclaringType.Should().Be(GetType().BaseType);
    }

    [Theory]
    [ComposeData<ComposeDataGenericDeclaringTypeProbeProfile>("RootRows")]
    public void InheritedRootRowSourceComposesAgainstTheClosedGenericBase(string label, ComposeDataGenericDeclaringTypeProbe probe)
    {
        label.Should().Be("root");
        probe.DeclaringType.Should().Be(GetType().BaseType);
    }
}

public sealed class ComposeDataGenericIntTests : ComposeDataGenericBase<int>;

public sealed class ComposeDataGenericStringTests : ComposeDataGenericBase<string>;
