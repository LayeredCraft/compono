using System.Reflection;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Compono.XunitV3.Tests;

/// <summary>
/// GetData-level coverage for <see cref="ComposeDataAttribute"/> (ADR-0070 / PLAN-0071 B3). These
/// tests call <c>GetData</c> directly against undecorated methods and assert the produced
/// <see cref="ITheoryDataRow"/> collection, its values, seeds, traits, and failure messages.
/// </summary>
public sealed class ComposeDataGetDataTests
{
    // ---- synchronous member shapes ------------------------------------------------------------
    public static readonly object?[] SimpleRows =
    [
        new object?[] { 1, "one" },
        new object?[] { 2, "two" },
    ];

    public static object?[] PropertyRows =>
    [
        new object?[] { 3, "three" },
    ];

    public static object?[] MethodRows() =>
    [
        new object?[] { 4, "four" },
    ];

    // ---- asynchronous member shapes -----------------------------------------------------------
    public static Task<object?[]> TaskRows() =>
        Task.FromResult<object?[]>([new object?[] { 5, "five" }]);

    public static ValueTask<object?[]> ValueTaskRows() =>
        new([new object?[] { 6, "six" }]);

    public static async IAsyncEnumerable<object?[]> AsyncRows()
    {
        yield return new object?[] { 7, "seven" };
        yield return new object?[] { 8, "eight" };
    }

    // Value-type element rows force the reflection-based async enumeration fallback.
    public static async IAsyncEnumerable<(int Value, string Name)> AsyncTupleRows()
    {
        yield return (9, "nine");
        yield return (10, "ten");
    }

    public static TheoryData<int, string> TheoryRows => new()
    {
        { 11, "eleven" },
        { 12, "twelve" },
    };

    public static int CountedInvocations;

    public static object?[] CountedRows()
    {
        CountedInvocations++;
        return [new object?[] { 13, "thirteen" }];
    }

    // ---- config-driven rows (two-type-parameter form) ----------------------------------------
    public static readonly object?[] ConfigRows =
    [
        new object?[] { new RowConfig("alpha") },
        new object?[] { new RowConfig("beta") },
    ];

    // ---- unsupported member shapes ------------------------------------------------------------
    public static string NotACollection => "nope";

    public static object?[]? NullRows => null;

    public static object?[] RequiresArguments(int unused) => [new object?[] { unused }];

    public object?[] InstanceRows => [new object?[] { 1, "one" }];

    public static Task NonGenericTaskRows => Task.CompletedTask;

    // ---- a seeded source ----------------------------------------------------------------------
    public static readonly object?[] SeedRows =
    [
        new object?[] { 1, "one" },
        new object?[] { 2, "two" },
    ];

    // ---- per-row isolation --------------------------------------------------------------------
    public static readonly object?[] IsolationRows =
    [
        new object?[] { new RowConfig("first") },
        new object?[] { new RowConfig("second") },
    ];

    [Fact]
    public async Task GetData_NonGenericForm_ComposesEachRowAndWritesSeedTrait()
    {
        var attribute = new ComposeDataAttribute(nameof(SimpleRows));
        var rows = await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        rows.Should().HaveCount(2);
        rows[0].GetData().Should().Equal(1, "one");
        rows[1].GetData().Should().Equal(2, "two");
        rows[0].Traits.Should().ContainKey(ComposeDataRows.SeedTraitName);
    }

    [Fact]
    public async Task GetData_ExplicitAttributeSeed_IsPinnedToEveryRow()
    {
        var attribute = new ComposeDataAttribute(nameof(SeedRows)) { Seed = 4242 };
        var rows = await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        rows.Should().HaveCount(2);
        foreach (var row in rows)
        {
            row.Traits![ComposeDataRows.SeedTraitName].Should().ContainSingle().Which.Should().Be("4242");
        }
    }

    [Fact]
    public async Task GetData_ExplicitZeroSeed_IsDistinctFromUnset()
    {
        var explicitZero = await GetDataAsync(new ComposeDataAttribute(nameof(SeedRows)) { Seed = 0 }, GetMethod(nameof(TwoParams)));
        explicitZero.Should().AllSatisfy(row => row.Traits![ComposeDataRows.SeedTraitName].Should().Contain("0"));

        var unset = await GetDataAsync(new ComposeDataAttribute(nameof(SeedRows)), GetMethod(nameof(TwoParams)));
        unset.Should().AllSatisfy(row => row.Traits![ComposeDataRows.SeedTraitName].Should().NotContain("0"));
    }

    [Fact]
    public async Task GetData_NegativeAttributeSeed_FailsBeforeReadingTheMember()
    {
        CountedInvocations = 0;
        var attribute = new ComposeDataAttribute(nameof(CountedRows)) { Seed = -1 };

        var act = async () => await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        await act.Should().ThrowAsync<CompositionException>().WithMessage("*non-negative seed*-1*");
        CountedInvocations.Should().Be(0);
    }

    [Fact]
    public async Task GetData_PropertyAndMethodShapes_AreSupported()
    {
        var propertyRows = await GetDataAsync(new ComposeDataAttribute(nameof(PropertyRows)), GetMethod(nameof(TwoParams)));
        propertyRows.Should().HaveCount(1);
        propertyRows[0].GetData().Should().Equal(3, "three");

        var methodRows = await GetDataAsync(new ComposeDataAttribute(nameof(MethodRows)), GetMethod(nameof(TwoParams)));
        methodRows.Should().HaveCount(1);
        methodRows[0].GetData().Should().Equal(4, "four");
    }

    [Fact]
    public async Task GetData_TaskValueTaskAndAsyncEnumerableShapes_AreSupported()
    {
        var taskRows = await GetDataAsync(new ComposeDataAttribute(nameof(TaskRows)), GetMethod(nameof(TwoParams)));
        taskRows.Should().HaveCount(1);
        taskRows[0].GetData().Should().Equal(5, "five");

        var valueTaskRows = await GetDataAsync(new ComposeDataAttribute(nameof(ValueTaskRows)), GetMethod(nameof(TwoParams)));
        valueTaskRows.Should().HaveCount(1);
        valueTaskRows[0].GetData().Should().Equal(6, "six");

        var asyncRows = await GetDataAsync(new ComposeDataAttribute(nameof(AsyncRows)), GetMethod(nameof(TwoParams)));
        asyncRows.Should().HaveCount(2);
        asyncRows[1].GetData().Should().Equal(8, "eight");
    }

    [Fact]
    public async Task GetData_AsyncValueTypeElementRows_UseReflectionFallback()
    {
        var rows = await GetDataAsync(new ComposeDataAttribute(nameof(AsyncTupleRows)), GetMethod(nameof(TwoParams)));

        rows.Should().HaveCount(2);
        rows[0].GetData().Should().Equal(9, "nine");
        rows[1].GetData().Should().Equal(10, "ten");
    }

    [Fact]
    public async Task GetData_TheoryDataShape_IsSupported()
    {
        var rows = await GetDataAsync(new ComposeDataAttribute(nameof(TheoryRows)), GetMethod(nameof(TwoParams)));

        rows.Should().HaveCount(2);
        rows[0].GetData().Should().Equal(11, "eleven");
    }

    [Fact]
    public async Task GetData_MemberIsReadOnlyWhenGetDataRuns()
    {
        CountedInvocations = 0;

        await GetDataAsync(new ComposeDataAttribute(nameof(CountedRows)), GetMethod(nameof(TwoParams)));

        CountedInvocations.Should().Be(1);
    }

    [Fact]
    public async Task GetData_MemberTypeOverride_ResolvesTheMemberElsewhere()
    {
        var attribute = new ComposeDataAttribute(nameof(RowSources.OtherRows)) { MemberType = typeof(RowSources) };
        var rows = await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        rows.Should().HaveCount(1);
        rows[0].GetData().Should().Equal(99, "other");
    }

    [Fact]
    public async Task GetData_InheritedStaticMember_IsResolved()
    {
        var attribute = new ComposeDataAttribute("BaseRows") { MemberType = typeof(DerivedSources) };
        var rows = await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        rows.Should().HaveCount(1);
        rows[0].GetData().Should().Equal(21, "base");
    }

    [Fact]
    public async Task GetData_MissingMember_ReportsD1()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute("DoesNotExist"), GetMethod(nameof(TwoParams)));

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*was not found*");
    }

    [Fact]
    public async Task GetData_NonStaticMember_ReportsD2()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute(nameof(InstanceRows)), GetMethod(nameof(TwoParams)));

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*must be public and static*");
    }

    [Fact]
    public async Task GetData_MemberRequiringArguments_ReportsD3()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute(nameof(RequiresArguments)), GetMethod(nameof(TwoParams)));

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*requires arguments*");
    }

    [Fact]
    public async Task GetData_UnsupportedReturnType_ReportsD4()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute(nameof(NotACollection)), GetMethod(nameof(TwoParams)));

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*unsupported type*");
    }

    [Fact]
    public async Task GetData_NullResult_ReportsD5()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute(nameof(NullRows)), GetMethod(nameof(TwoParams)));

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*returned null*");
    }

    [Fact]
    public async Task GetData_NonGenericTask_ReportsD4()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute(nameof(NonGenericTaskRows)), GetMethod(nameof(TwoParams)));

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*unsupported type*");
    }

    [Fact]
    public async Task GetData_TooManyValues_ReportsRowFailure()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute(nameof(SimpleRows)), GetMethod(nameof(OneParam)));

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*ComposeData row 0*too many values: 2 supplied, but the method has 1 parameter(s)*");
    }

    [Fact]
    public async Task GetData_TypeMismatch_ReportsRowFailureNamingParameter()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute(nameof(WrongTypeRows)), GetMethod(nameof(TwoParams)));

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*Row value for parameter 'value'*not assignable*");
    }

    [Fact]
    public async Task GetData_NullForNonNullableValueType_ReportsRowFailure()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute(nameof(NullValueRows)), GetMethod(nameof(TwoParams)));

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*Row value for parameter 'value'*is null, but the parameter is not nullable.*");
    }

    [Fact]
    public async Task GetData_SharedParameter_SuppliedByRowIsSharedExplicitly()
    {
        var rows = await GetDataAsync(new ComposeDataAttribute(nameof(SharedRows)), GetMethod(nameof(SharedFirst)));

        rows.Should().HaveCount(1);
        rows[0].GetData()[0].Should().Be("shared-value");
        rows[0].GetData()[1].Should().Be(5);
    }

    [Fact]
    public async Task GetData_ComposedSharedParameter_IsResolvedWhenNotSupplied()
    {
        var rows = await GetDataAsync(new ComposeDataAttribute(nameof(SingleValueRows)), GetMethod(nameof(ComposedShared)));

        rows.Should().HaveCount(1);
        rows[0].GetData()[0].Should().Be(31);
        rows[0].GetData()[1].Should().NotBeNull();
    }

    [Fact]
    public async Task GetData_ProfileGenericForm_AppliesTheProfile()
    {
        var rows = await GetDataAsync(new ComposeDataAttribute<ConstrainedProfile>(nameof(ConstrainedRows)), GetMethod(nameof(OneParam)));

        rows.Should().HaveCount(1);
        rows[0].GetData()[0].Should().Be("profile-value");
    }

    [Fact]
    public async Task GetData_ConfigGenericForm_ExtractsPerRowConfigAndIsolatesProfiles()
    {
        ConfigProfile.Instances = 0;

        var rows = await GetDataAsync(new ComposeDataAttribute<ConfigProfile, RowConfig>(nameof(ConfigRows)), GetMethod(nameof(OneParam)));

        rows.Should().HaveCount(2);
        rows[0].GetData()[0].Should().Be("alpha");
        rows[1].GetData()[0].Should().Be("beta");
        ConfigProfile.Instances.Should().Be(2);
    }

    [Fact]
    public async Task GetData_ConfigGenericForm_MissingConfigElement_ReportsRowFailure()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute<ConfigProfile, RowConfig>(nameof(SimpleRows)), GetMethod(nameof(OneParam)));

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*the first row element must be a non-null*profile configuration value*");
    }

    [Fact]
    public async Task GetData_AttributeSeedCollidingWithProfileSeed_ReportsCoreConflict()
    {
        var attribute = new ComposeDataAttribute<SeededProfile>(nameof(OneValueRows)) { Seed = 7 };

        var act = async () => await GetDataAsync(attribute, GetMethod(nameof(OneParam)));

        var exception = await act.Should().ThrowAsync<CompositionException>();
        exception.Which.InnerException.Should().BeOfType<CompositionConfigurationException>();
    }

    [Fact]
    public async Task GetData_NegativeProfileSeed_ReportsRowFailureWithRowSeed()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute<NegativeSeededProfile>(nameof(OneValueRows)), GetMethod(nameof(OneParam)));

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*non-negative seed*-3*");
    }

    [Fact]
    public async Task GetData_FailureInLaterRow_NamesTheRowIndex()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute(nameof(SecondRowBadRows)), GetMethod(nameof(TwoParams)));

        var exception = await act.Should().ThrowAsync<CompositionException>();
        exception.Which.Message.Should().Contain("ComposeData row 1");
        // A row-value validation failure is a direct R0 cause, not a wrapped exception, so the ADR-0070
        // failure-context contract must not fabricate an inner exception for it.
        exception.Which.InnerException.Should().BeNull();
    }

    [Fact]
    public async Task GetData_Metadata_IsMergedOntoEachRow()
    {
        var attribute = new ComposeDataAttribute(nameof(SeedRows))
        {
            Label = "attr-label",
            TestDisplayName = "attr-display",
            Skip = "attr-skip",
            Explicit = true,
            Timeout = 5000,
            Traits = ["category", "unit"],
        };

        var rows = await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        rows.Should().AllSatisfy(row =>
        {
            row.Label.Should().Be("attr-label");
            row.TestDisplayName.Should().Be("attr-display");
            row.Skip.Should().Be("attr-skip");
            row.Explicit.Should().BeTrue();
            row.Timeout.Should().Be(5000);
            row.Traits!["category"].Should().Contain("unit");
        });
    }

    [Fact]
    public async Task GetData_RowMetadata_OverridesAttributeMetadata()
    {
        var attribute = new ComposeDataAttribute(nameof(TaggedRows)) { Label = "attr-label" };
        var rows = await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        rows[0].Label.Should().Be("row-label");
        rows[0].GetData().Should().Equal(41, "tagged");
    }

    [Fact]
    public async Task GetData_SkipGroup_RowSkipWinsOverAttributeSkip()
    {
        var attribute = new ComposeDataAttribute(nameof(TaggedRows)) { Skip = "attr-skip" };
        var rows = await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        rows[0].Skip.Should().Be("row-skip");
    }

    [Fact]
    public async Task GetData_UnsupportedSubclass_ThrowsCompositionException()
    {
        var attribute = new DerivedComposeDataAttribute(nameof(SimpleRows));

        var act = async () => await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*is not supported*");
    }

    [Fact]
    public async Task GetData_ComposeAndComposeData_AreMutuallyExclusive()
    {
        var attribute = new ComposeDataAttribute(nameof(SimpleRows));

        var act = async () => await GetDataAsync(attribute, GetMethod(nameof(BothFamilies)));

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*only one Compose-family attribute*");
    }

    [Fact]
    public void SupportsDiscoveryEnumeration_IsFalse() =>
        new ComposeDataAttribute(nameof(SimpleRows)).SupportsDiscoveryEnumeration().Should().BeFalse();

    [Fact]
    public async Task GetData_DefaultSeeds_AreDistinctPerRow()
    {
        var rows = await GetDataAsync(new ComposeDataAttribute(nameof(SimpleRows)), GetMethod(nameof(TwoParams)));

        var first = rows[0].Traits![ComposeDataRows.SeedTraitName].Single();
        var second = rows[1].Traits![ComposeDataRows.SeedTraitName].Single();
        first.Should().NotBe(second);
    }

    [Fact]
    public async Task GetData_ProfileWithSeed_PinsEveryRow()
    {
        var rows = await GetDataAsync(new ComposeDataAttribute<SeededProfile>(nameof(SimpleRows)), GetMethod(nameof(TwoParams)));

        rows.Should().OnlyContain(row => row.Traits![ComposeDataRows.SeedTraitName].Single() == "11");
    }

    [Fact]
    public async Task GetData_NullableParameters_AllowNullRowValues()
    {
        var rows = await GetDataAsync(new ComposeDataAttribute(nameof(NullableValueRows)), GetMethod(nameof(NullableParams)));

        var values = rows[0].GetData();
        values[0].Should().BeNull();
        values[1].Should().BeNull();
    }

    [Fact]
    public async Task GetData_NullRow_ReportsRowFailure()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute(nameof(NullRowRows)), GetMethod(nameof(TwoParams)));

        await act.Should().ThrowAsync<CompositionException>().WithMessage("*row is null*");
    }

    [Fact]
    public async Task GetData_UnsupportedRow_ReportsRowFailure()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute(nameof(UnsupportedRowRows)), GetMethod(nameof(TwoParams)));

        await act.Should().ThrowAsync<CompositionException>().WithMessage("*row has unsupported type*");
    }

    [Fact]
    public async Task GetData_ConfigureThrowing_WithoutSeed_ReportsNotAssignedSeedLine()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute<ThrowingConfigureProfile>(nameof(ConstrainedRows)), GetMethod(nameof(OneParam)));

        await act.Should().ThrowAsync<CompositionException>().WithMessage("*configure-boom*not assigned*");
    }

    [Fact]
    public async Task GetData_ConfigureThrowing_WithSeed_ReportsTheConfiguredSeed()
    {
        var act = async () => await GetDataAsync(
            new ComposeDataAttribute<ThrowingConfigureProfile>(nameof(ConstrainedRows)) { Seed = 5 },
            GetMethod(nameof(OneParam)));

        await act.Should().ThrowAsync<CompositionException>().WithMessage("*configure-boom*Seed: 5*");
    }

    [Fact]
    public async Task GetData_MemberLevelFailure_HasNoSeedLine()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute("NoSuchMember"), GetMethod(nameof(TwoParams)));

        var exception = await act.Should().ThrowAsync<CompositionException>();
        exception.Which.Message.Should().NotContain("Seed:");
    }

    [Fact]
    public async Task GetData_PostCreateRowFailure_ReportsRowSeed()
    {
        var act = async () => await GetDataAsync(new ComposeDataAttribute(nameof(WrongTypeRows)), GetMethod(nameof(TwoParams)));

        var exception = await act.Should().ThrowAsync<CompositionException>();
        exception.Which.Message.Should().MatchRegex(@"Seed: \d+");
    }

    [Fact]
    public async Task GetData_PinnedSeed_ReproducesComposedValues()
    {
        var first = await GetDataAsync(new ComposeDataAttribute(nameof(EmptyRows)) { Seed = 4242 }, GetMethod(nameof(OneInt)));
        var second = await GetDataAsync(new ComposeDataAttribute(nameof(EmptyRows)) { Seed = 4242 }, GetMethod(nameof(OneInt)));

        first[0].GetData()[0].Should().Be(second[0].GetData()[0]);
    }

    // ---- additional row sources ---------------------------------------------------------------
    public static readonly object?[] WrongTypeRows = [new object?[] { "not-an-int", "name" }];

    public static readonly object?[] NullValueRows = [new object?[] { null, "name" }];

    public static readonly object?[] SingleValueRows = [new object?[] { 31 }];

    public static readonly object?[] SharedRows = [new object?[] { "shared-value", 5 }];

    public static readonly object?[] OneValueRows = [new object?[] { 1, "one" }];

    public static readonly object?[] ConstrainedRows = [new object?[] { }];

    public static readonly object?[] SecondRowBadRows =
    [
        new object?[] { 1, "one" },
        new object?[] { "wrong", "two" },
    ];

    public static readonly object?[] TaggedRows =
    [
        new TheoryDataRow(41, "tagged") { Label = "row-label", Skip = "row-skip" },
    ];

    public static readonly object?[] NullableValueRows = [new object?[] { null, null }];

    public static readonly object?[] NullRowRows = [null];

    public static readonly object?[] UnsupportedRowRows = [new object()];

    public static readonly object?[] EmptyRows = [new object?[] { }];

    // ---- test methods (undecorated; GetData only reads DeclaringType + parameters) ------------
    private static void TwoParams(int value, string name)
    {
        _ = value;
        _ = name;
    }

    private static void OneParam(string configured)
    {
        _ = configured;
    }

    private static void SharedFirst([Shared] string shared, int value)
    {
        _ = shared;
        _ = value;
    }

    private static void ComposedShared(int value, [Shared] string shared)
    {
        _ = value;
        _ = shared;
    }

    private static void NullableParams(int? value, string? name)
    {
        _ = value;
        _ = name;
    }

    private static void OneInt(int value)
    {
        _ = value;
    }

#pragma warning disable xUnit1008 // BothFamilies is deliberately both [Compose] and [ComposeData] and is read directly, not run
    [Compose]
    [ComposeData(nameof(SimpleRows))]
    private static void BothFamilies(int value, string name)
    {
        _ = value;
        _ = name;
    }
#pragma warning restore xUnit1008

    private static MethodInfo GetMethod(string name) =>
        typeof(ComposeDataGetDataTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static async Task<IReadOnlyList<ITheoryDataRow>> GetDataAsync(DataAttribute attribute, MethodInfo method) =>
        [.. await attribute.GetData(method, new DisposalTracker())];

    // ---- nested fixtures ----------------------------------------------------------------------
    internal sealed record RowConfig(string Value);

    internal static class RowSources
    {
        public static readonly object?[] OtherRows = [new object?[] { 99, "other" }];
    }

    internal class BaseSources
    {
        public static readonly object?[] BaseRows = [new object?[] { 21, "base" }];
    }

    internal sealed class DerivedSources : BaseSources;

    private sealed class ConstrainedProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.Register(() => "profile-value");
    }

    private sealed class ConfigProfile : ICompositionProfile
    {
        public static int Instances;

        private readonly RowConfig _config;

        public ConfigProfile(RowConfig config)
        {
            Instances++;
            _config = config;
        }

        public void Configure(CompositionBuilder builder) => builder.Register(() => _config.Value);
    }

    private sealed class SeededProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.WithSeed(11);
    }

    private sealed class NegativeSeededProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.WithSeed(-3);
    }

    private sealed class ThrowingConfigureProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => throw new InvalidOperationException("configure-boom");
    }

    // ---------------------------------------------------------------------------------------------
    // Remediation coverage (F1, F2, F4, F6, F7).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task GetData_LazySyncCollectionThatThrowsMidEnumeration_IsWrappedAsCompositionException()
    {
        var attribute = new ComposeDataAttribute(nameof(ThrowingSyncRows));

        var act = async () => await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        var exception = await act.Should().ThrowAsync<CompositionException>();
        exception.Which.Message.Should().Contain("failed while reading its value");
        exception.Which.Message.Should().Contain("sync-boom");
        exception.Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task GetData_AmbiguousInheritedStaticMember_ReportsCompositionException()
    {
        var attribute = new ComposeDataAttribute("Rows") { MemberType = typeof(AmbiguousDerived) };

        var act = async () => await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        var exception = await act.Should().ThrowAsync<CompositionException>();
        exception.Which.Message.Should().Contain("ambiguous");
        exception.Which.InnerException.Should().BeOfType<AmbiguousMatchException>();
    }

    [Fact]
    public async Task GetDataCore_CancellationDuringAsyncEnumeration_PropagatesOperationCanceled()
    {
        CancellationSource = new CancellationTokenSource();
        var attribute = new ComposeDataAttribute(nameof(CancellingRows));

        var act = async () => await attribute.GetDataCore(GetMethod(nameof(TwoParams)), CancellationSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetDataCore_CancellationDuringAsyncValueTypeEnumeration_PropagatesOperationCanceled()
    {
        CancellationSource = new CancellationTokenSource();
        var attribute = new ComposeDataAttribute(nameof(CancellingTupleRows));

        var act = async () => await attribute.GetDataCore(GetMethod(nameof(TwoParams)), CancellationSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetData_AttributeSkipGroup_IsMergedOntoRows()
    {
        var attribute = new ComposeDataAttribute(nameof(SimpleRows))
        {
            Skip = "attribute-skip",
            SkipType = typeof(RowSources),
            SkipUnless = "ConditionProperty",
            SkipWhen = "OtherCondition",
        };

        var rows = await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        rows.Should().AllSatisfy(row =>
        {
            row.Skip.Should().Be("attribute-skip");
            row.SkipType.Should().Be(typeof(RowSources));
            row.SkipUnless.Should().Be("ConditionProperty");
            row.SkipWhen.Should().Be("OtherCondition");
        });
    }

    [Fact]
    public async Task GetData_AttributeDisableParallelization_IsMergedOntoRows()
    {
        var attribute = new ComposeDataAttribute(nameof(SimpleRows)) { DisableParallelization = true };

        var rows = await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        rows.Should().AllSatisfy(row => row.DisableParallelization.Should().BeTrue());
    }

    [Fact]
    public async Task GetData_DisableParallelization_DefaultsToFalse()
    {
        var attribute = new ComposeDataAttribute(nameof(SimpleRows));

        var rows = await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        rows.Should().AllSatisfy(row => row.DisableParallelization.Should().BeFalse());
    }

    [Fact]
    public async Task GetData_D1Message_IsExact()
    {
        var typeName = typeof(ComposeDataGetDataTests).FullName;
        var methodName = $"{typeName}.{nameof(TwoParams)}";
        var attribute = new ComposeDataAttribute("NoSuchMember");

        var act = async () => await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        var exception = await act.Should().ThrowAsync<CompositionException>();
        exception.Which.Message.Should().Be(
            $"ComposeData member '{typeName}.NoSuchMember' for '{methodName}' was not found. Expected a public static field, property, or parameterless method.");
    }

    [Fact]
    public async Task GetData_D2Message_IsExact()
    {
        var typeName = typeof(ComposeDataGetDataTests).FullName;
        var methodName = $"{typeName}.{nameof(TwoParams)}";
        var attribute = new ComposeDataAttribute(nameof(InstanceRows));

        var act = async () => await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        var exception = await act.Should().ThrowAsync<CompositionException>();
        exception.Which.Message.Should().Be(
            $"ComposeData member '{typeName}.InstanceRows' for '{methodName}' must be public and static.");
    }

    [Fact]
    public async Task GetData_D3Message_IsExact()
    {
        var typeName = typeof(ComposeDataGetDataTests).FullName;
        var methodName = $"{typeName}.{nameof(TwoParams)}";
        var attribute = new ComposeDataAttribute(nameof(RequiresArguments));

        var act = async () => await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        var exception = await act.Should().ThrowAsync<CompositionException>();
        exception.Which.Message.Should().Be(
            $"ComposeData member '{typeName}.RequiresArguments' for '{methodName}' requires arguments, which ComposeData does not support.");
    }

    [Fact]
    public async Task GetData_D4Message_IsExact()
    {
        var typeName = typeof(ComposeDataGetDataTests).FullName;
        var methodName = $"{typeName}.{nameof(TwoParams)}";
        var attribute = new ComposeDataAttribute(nameof(NotACollection));

        var act = async () => await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        var exception = await act.Should().ThrowAsync<CompositionException>();
        exception.Which.Message.Should().Be(
            $"ComposeData member '{typeName}.NotACollection' for '{methodName}' returned unsupported type 'System.String'. Expected IEnumerable, Task/ValueTask of IEnumerable, or IAsyncEnumerable of ITheoryDataRow, object?[], or ITuple rows.");
    }

    [Fact]
    public async Task GetData_D5Message_IsExact()
    {
        var typeName = typeof(ComposeDataGetDataTests).FullName;
        var methodName = $"{typeName}.{nameof(TwoParams)}";
        var attribute = new ComposeDataAttribute(nameof(NullRows));

        var act = async () => await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        var exception = await act.Should().ThrowAsync<CompositionException>();
        exception.Which.Message.Should().Be(
            $"ComposeData member '{typeName}.NullRows' for '{methodName}' returned null.");
    }

    [Fact]
    public async Task GetData_NestedTypeMember_UsesClrPlusFormattedName()
    {
        var attribute = new ComposeDataAttribute("NoSuchMember") { MemberType = typeof(RowSources) };

        var act = async () => await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        var exception = await act.Should().ThrowAsync<CompositionException>();
        exception.Which.Message.Should().Contain("ComposeDataGetDataTests+RowSources.NoSuchMember");
    }

    [Fact]
    public async Task GetData_DiagnosticIsPreservedThroughRowFailure()
    {
        var attribute = new ComposeDataAttribute<DiagnosticThrowingProfile>(nameof(EmptyRows));

        var act = async () => await GetDataAsync(attribute, GetMethod(nameof(OneInt)));

        var exception = await act.Should().ThrowAsync<CompositionException>();
        exception.Which.Diagnostic.Should().NotBeNull();
        exception.Which.Diagnostic!.Message.Should().Be("diag-boom");
        exception.Which.InnerException.Should().BeOfType<CompositionException>();
    }

    [Fact]
    public async Task GetData_FailureInLaterRow_NamesMemberIndexAndSeed()
    {
        var attribute = new ComposeDataAttribute(nameof(SecondRowBadRows));

        var act = async () => await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        var exception = await act.Should().ThrowAsync<CompositionException>();
        exception.Which.Message.Should().Contain("ComposeData row 1");
        exception.Which.Message.Should().Contain(nameof(SecondRowBadRows));
        exception.Which.Message.Should().Contain("Row value for parameter 'value'");
        exception.Which.Message.Should().MatchRegex(@"Seed: \d+");
        // Row-value validation failures are direct R0 causes (no wrapped exception), so the message is
        // the full contract here - asserting no inner documents that it is not double-wrapped.
        exception.Which.InnerException.Should().BeNull();
    }

    [Fact]
    public async Task GetData_CapturedSeed_ReproducesComposedValues()
    {
        var first = await GetDataAsync(new ComposeDataAttribute(nameof(EmptyRows)), GetMethod(nameof(OneInt)));
        var seed = first[0].Traits![ComposeDataRows.SeedTraitName].Single();

        var second = await GetDataAsync(
            new ComposeDataAttribute(nameof(EmptyRows)) { Seed = int.Parse(seed, System.Globalization.CultureInfo.InvariantCulture) },
            GetMethod(nameof(OneInt)));

        second[0].Traits![ComposeDataRows.SeedTraitName].Should().ContainSingle().Which.Should().Be(seed);
        second[0].GetData()[0].Should().Be(first[0].GetData()[0]);
    }

    [Fact]
    public async Task GetData_DateTimeOffsetConfig_ReachesTheProfile()
    {
        var when = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var attribute = new ComposeDataAttribute<DateConfigProfile, DateTimeOffset>(nameof(DateConfigRows));

        var rows = await GetDataAsync(attribute, GetMethod(nameof(OneString)));

        rows.Should().HaveCount(1);
        rows[0].GetData().Should().ContainSingle().Which.Should().Be(when.ToString("O"));
    }

    [Fact]
    public async Task GetData_SyncValueTupleRows_AreFlattened()
    {
        var attribute = new ComposeDataAttribute(nameof(TupleRows));

        var rows = await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        rows.Should().HaveCount(2);
        rows[0].GetData().Should().Equal(1, "one");
        rows[1].GetData().Should().Equal(2, "two");
    }

    [Fact]
    public async Task GetData_TypedTheoryDataRowArray_IsSupported()
    {
        var attribute = new ComposeDataAttribute(nameof(TypedTheoryRows));

        var rows = await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        rows.Should().HaveCount(2);
        rows[0].GetData().Should().Equal(1, "one");
    }

    [Fact]
    public async Task GetData_ListOfObjectArray_IsSupported()
    {
        var attribute = new ComposeDataAttribute(nameof(ListRows));

        var rows = await GetDataAsync(attribute, GetMethod(nameof(TwoParams)));

        rows.Should().HaveCount(2);
        rows[0].GetData().Should().Equal(1, "one");
    }

    [Fact]
    public async Task GetData_RowAndComposedValues_AreNotRegisteredWithTheCallersDisposalTracker()
    {
        // Ownership boundary (PLAN-0071 Pass 1 B6.2): Compono's GetData composes row values but never
        // registers any of them with the DisposalTracker it is handed. That tracker belongs to the
        // xUnit runner, which later calls DisposalTracker.AddRange(row.GetData()) for each row it
        // executes (RESEARCH-0038 §2.4) - i.e. JIT theory-argument disposal is xUnit's behavior, not
        // Compono's. So a caller-owned tracker comes back empty here, and disposing it disposes none
        // of the values. No real-runner disposal is asserted (SQ3).
        var tracker = new DisposalTracker();
        var attribute = new ComposeDataAttribute<TrackedDisposableProfile>(nameof(DisposalRows));

        var rows = await attribute.GetData(GetMethod(nameof(DisposalTarget)), tracker);

        tracker.TrackedObjects.Should().BeEmpty();

        var data = rows.Should().ContainSingle().Subject.GetData();
        var rowSupplied = data[0].Should().BeOfType<ComposeDataTrackedDisposable>().Subject;
        var composed = data[1].Should().BeOfType<ComposeDataTrackedDisposable>().Subject;

        await tracker.DisposeAsync();

        rowSupplied.Disposed.Should().BeFalse();
        composed.Disposed.Should().BeFalse();
    }

    // ---- remediation row sources ----

    public static IEnumerable<object?[]> ThrowingSyncRows()
    {
        yield return new object?[] { 1, "one" };
        throw new InvalidOperationException("sync-boom");
    }

    public static CancellationTokenSource CancellationSource = new();

    // ComposeData members must be parameterless, so this iterator cancels the shared source part way
    // through; the accessor's between-row cancellation check observes the token and propagates it.
    public static async IAsyncEnumerable<object?[]> CancellingRows()
    {
        yield return new object?[] { 1, "one" };
        CancellationSource.Cancel();
        await Task.Yield();
        yield return new object?[] { 2, "two" };
    }

    public static readonly (int Value, string Name)[] TupleRows = [(1, "one"), (2, "two")];

    // Value-type element stream, so it takes the reflection fallback path - it must observe and
    // propagate the cancellation token identically to the reference-element fast path.
    public static async IAsyncEnumerable<(int Value, string Name)> CancellingTupleRows()
    {
        yield return (1, "one");
        CancellationSource.Cancel();
        await Task.Yield();
        yield return (2, "two");
    }

    public static readonly TheoryDataRow<int, string>[] TypedTheoryRows = [new(1, "one"), new(2, "two")];

    public static readonly List<object?[]> ListRows = [new object?[] { 1, "one" }, new object?[] { 2, "two" }];

    public static readonly object?[] DateConfigRows =
        [new object?[] { new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero) }];

    // ---- remediation test methods ----

    public static object?[] DisposalRows => [new object?[] { new ComposeDataTrackedDisposable() }];

    // Carries [ComposeData] (but no [Theory], so the runner never executes it) purely so the source
    // generator registers a row-binding dispatch for ComposeDataTrackedDisposable - the real GetData
    // path below cannot bind a custom parameter type without one. xUnit1008 is suppressed because the
    // attribute is a generator-dispatch request here, not a test-data source.
#pragma warning disable xUnit1008
    [ComposeData<TrackedDisposableProfile>(nameof(DisposalRows))]
    private static void DisposalTarget(ComposeDataTrackedDisposable rowSupplied, ComposeDataTrackedDisposable composed)
    {
        _ = rowSupplied;
        _ = composed;
    }
#pragma warning restore xUnit1008

    private static void OneString(string configured)
    {
        _ = configured;
    }

    // ---- remediation fixtures ----

    internal class AmbiguousBase
    {
        public static object?[] Rows => [new object?[] { 1, "base" }];
    }

    internal sealed class AmbiguousDerived : AmbiguousBase
    {
        public static new string[] Rows => ["derived"];
    }

    private sealed class DateConfigProfile : ICompositionProfile
    {
        private readonly DateTimeOffset _config;

        public DateConfigProfile(DateTimeOffset config) => _config = config;

        public void Configure(CompositionBuilder builder) => builder.Register(() => _config.ToString("O"));
    }

    private static readonly CompositionDiagnostic ProfileDiagnostic = new()
    {
        RootType = typeof(string),
        FailedType = typeof(int),
        Path = "root",
        Trace = [],
        Seed = 123UL,
        Message = "diag-boom",
    };

    private sealed class DiagnosticThrowingProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => throw new CompositionException(ProfileDiagnostic);
    }

    private sealed class DerivedComposeDataAttribute(string memberName) : ComposeDataAttribute(memberName);
}

// Disposal ownership-boundary fixture types (PLAN-0071 Pass 1 B6.2). Declared at namespace scope
// rather than nested: the source generator only emits row-binding dispatch for accessible parameter
// types, and a private nested type trips CMP0013.
public sealed class ComposeDataTrackedDisposable : IDisposable
{
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}

public sealed class TrackedDisposableProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new ComposeDataTrackedDisposable());
}
