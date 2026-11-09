using Compono;
using Compono.XunitV3.Aot;
using Compono.XunitV3.Aot.Tests.Support;
using Xunit;
using Xunit.v3;

namespace Compono.XunitV3.Aot.Tests;

/// <summary>
/// Executable proof (running under the real xUnit Native AOT runner) that the generated
/// <c>[ComposeData]</c> registration produced by <c>AotComposeDataRegistrationEmitter</c> composes
/// theory rows and reports failures correctly - ADR-0070 / PLAN-0071 B5.6-B5.10, slice S2.
/// </summary>
/// <remarks>
/// Success paths are ordinary <c>[Theory]</c> methods the runner executes directly. Failure and
/// metadata paths are plain <c>[ComposeData]</c> targets (no <c>[Theory]</c>, so the runner does not
/// discover them as tests) whose row factory - registered by the module initializer - is driven
/// through <see cref="RegisteredComposeDataFactory"/> from a <c>[Fact]</c>, so the exact
/// generated-row values, merged metadata, and failure text can be asserted. Only the synchronous
/// member shape is exercised here; asynchronous shapes are a later slice.
/// </remarks>
public sealed class ComposeDataEmissionTests : ComposeDataEmissionBase
{
    public static IEnumerable<object[]> UntypedRows() =>
    [
        new object[] { "alpha", 1 },
        new object[] { "beta", 2 },
    ];

    [Theory]
    [ComposeData(nameof(UntypedRows))]
    public void Untyped_rows_are_composed(string label, int quantity)
    {
        Assert.False(string.IsNullOrEmpty(label));
        Assert.InRange(quantity, 1, 2);
    }

    public static TheoryData<string, int> TypedRows { get; } = new()
    {
        { "gamma", 3 },
        { "delta", 4 },
    };

    [Theory]
    [ComposeData(nameof(TypedRows))]
    public void Typed_rows_are_composed(string label, int quantity)
    {
        Assert.False(string.IsNullOrEmpty(label));
        Assert.InRange(quantity, 3, 4);
    }

    public static System.Collections.Immutable.ImmutableArray<object[]> StructRows { get; } =
        System.Collections.Immutable.ImmutableArray.Create<object[]>(new object[] { "struct" });

    [Theory]
    [ComposeData(nameof(StructRows))]
    public void Struct_collection_rows_are_composed(string label) => Assert.Equal("struct", label);

    public static IEnumerable<object[]> MethodRows() => [new object[] { "epsilon" }];

    [Theory]
    [ComposeData(nameof(MethodRows))]
    public void Parameterless_method_rows_are_composed(string label) =>
        Assert.False(string.IsNullOrEmpty(label));

    [Theory]
    [ComposeData(nameof(InheritedRows))]
    public void Inherited_rows_are_composed(string label) =>
        Assert.Equal("inherited", label);

    [Theory]
    [ComposeData(nameof(ComposeDataExternalSource.ExternalRows), MemberType = typeof(ComposeDataExternalSource))]
    public void MemberType_rows_are_composed(string label) =>
        Assert.Equal("external", label);

    [Theory]
    [ComposeData(nameof(PartialRows))]
    public void Partial_rows_compose_missing_parameters(string label, OrderService service)
    {
        Assert.Equal("partial", label);
        Assert.NotNull(service);
    }

    public static IEnumerable<object[]> PartialRows() => [new object[] { "partial" }];

    public static IEnumerable<object[]> ProfileOnlyRows() => [Array.Empty<object>()];

    [Theory]
    [ComposeData<PersistentWidgetProfile>(nameof(ProfileOnlyRows))]
    public void Profile_only_rows_use_profile_registration(OrderService service) =>
        Assert.Equal("profile-registered", service.Name);

    public static IEnumerable<object?[]> ProfileConfigRows() =>
        [new object?[] { new WidgetConfig("configured-widget", 7), "row-label" }];

    [Theory]
    [ComposeData<ConfiguredWidgetProfile, WidgetConfig>(nameof(ProfileConfigRows))]
    public void Profile_config_rows_extract_configuration(string label, OrderService service, int quantity)
    {
        Assert.Equal("row-label", label);
        Assert.Equal("configured-widget", service.Name);
        Assert.Equal(7, quantity);
    }

    public static IEnumerable<object[]> SharedRows() => [Array.Empty<object>()];

    [Theory]
    [ComposeData(nameof(SharedRows))]
    public void Shared_parameter_is_reused_by_a_later_ordinary_parameter(
        [Shared] OrderService shared,
        OrderService repeat)
    {
        Assert.Same(shared, repeat);
        Assert.False(string.IsNullOrEmpty(shared.Name));
    }

    // S3 review: shared parameters resolve before ordinary ones even when the ordinary parameter has
    // the lower ordinal. The generated factory therefore ResolveShared's ordinal 1 first; the ordinal
    // 0 Resolve then finds that shared instance in the row's shared scope. If the generator instead
    // emitted plain ordinal order, the ordinal 0 Resolve would compose its own instance and this
    // Assert.Same would fail.
    [Theory]
    [ComposeData(nameof(SharedRows))]
    public void Shared_parameter_preceded_by_ordinary_parameter_uses_the_shared_instance(
        OrderService first,
        [Shared] OrderService shared)
    {
        Assert.Same(first, shared);
        Assert.False(string.IsNullOrEmpty(first.Name));
    }

    // S3 review / PLAN-0071 B5.11: the three S0 TConfig identity shapes (tuple, dynamic/object,
    // List<dynamic>/List<object>) must be constructed for real through the generated arity-2 factory,
    // not just accepted by discovery.
    public static IEnumerable<object?[]> TupleConfigRows() => [new object?[] { (3, 4) }];

    [Theory]
    [ComposeData<TupleConfigProfile, System.ValueTuple<int, int>>(nameof(TupleConfigRows))]
    public void Tuple_config_identity_shape_is_constructed(OrderService service) =>
        Assert.Equal("3:4", service.Name);

    public static IEnumerable<object?[]> DynamicConfigRows() => [new object?[] { "dynamic-config" }];

    [Theory]
    [ComposeData<DynamicConfigProfile, object>(nameof(DynamicConfigRows))]
    public void Dynamic_config_identity_shape_is_constructed(OrderService service) =>
        Assert.Equal("dynamic-config", service.Name);

    public static IEnumerable<object?[]> ListConfigRows() =>
        [new object?[] { new System.Collections.Generic.List<object> { "a", "b" } }];

    [Theory]
    [ComposeData<ListConfigProfile, System.Collections.Generic.List<object>>(nameof(ListConfigRows))]
    public void List_config_identity_shape_is_constructed(OrderService service) =>
        Assert.Equal("a+b", service.Name);

    public static OrderService SharedExplicitService { get; } = new("shared-explicit");

    public static IEnumerable<object[]> SharedExplicitRows() =>
        [new object[] { SharedExplicitService, "row-label" }];

    [Theory]
    [ComposeData(nameof(SharedExplicitRows))]
    public void Shared_explicit_value_is_reused_by_a_later_ordinary_parameter(
        [Shared] OrderService shared,
        string label,
        OrderService repeat)
    {
        Assert.Same(SharedExplicitService, shared);
        Assert.Same(shared, repeat);
        Assert.Equal("row-label", label);
    }

    [Fact]
    public async Task MetadataRows_CarryConstantsAndSeedTrait()
    {
        var rows = await RegisteredComposeDataFactory.InvokeAsync(
            FailureTargetsIndex,
            nameof(ComposeDataEmissionFailureTargets.Metadata_rows_carry_attribute_metadata));

        var row = Assert.Single(rows);
        var dataRow = Assert.IsType<TheoryDataRow>(row);

        Assert.Equal("the label", dataRow.Label);
        Assert.Equal("the display", dataRow.TestDisplayName);
        Assert.Equal(42, dataRow.Timeout);
        Assert.True(dataRow.Explicit);
        Assert.True(dataRow.DisableParallelization);
        Assert.Contains("pricing", dataRow.Traits["category"]);

        var seedTraits = dataRow.Traits[ComposeDataRows.SeedTraitName];
        Assert.Single(seedTraits);
        Assert.True(int.TryParse(seedTraits.Single(), out var seed));
        Assert.True(seed >= 0);

        Assert.Equal(new object?[] { "metadata" }, row.GetData());
    }

    [Fact]
    public async Task NullRow_ReportsR0FailureWithPreRowSeedLine()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Null_row_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.NullRowRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Null_row_fails));
        var expected =
            $"ComposeData row 0 from '{member}' for '{method}' failed: row is null" +
            "\n\nSeed: not assigned (the failure occurred before the row's composition started)";

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task TooManyValues_ReportsFailureWithRowSeedLine()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Too_many_values_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.TooManyValueRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Too_many_values_fails));
        var expectedPrefix =
            $"ComposeData row 0 from '{member}' for '{method}' failed: too many values: 3 supplied, but the method has 2 parameter(s)";

        AssertFailureWithRowSeedLine(exception.Message, expectedPrefix);
    }

    [Fact]
    public async Task NullValue_ReportsFailureWithRowSeedLine()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Null_value_fails)).AsTask());

        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Null_value_fails));
        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.NullValueRows));
        var expectedPrefix =
            $"ComposeData row 0 from '{member}' for '{method}' failed: " +
            $"Row value for parameter 'label' on '{method}' is null, but the parameter is not nullable.";

        AssertFailureWithRowSeedLine(exception.Message, expectedPrefix);
    }

    [Fact]
    public async Task TypeMismatch_ReportsFailureWithRowSeedLine()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Type_mismatch_fails)).AsTask());

        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Type_mismatch_fails));
        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.TypeMismatchRows));
        var expectedPrefix =
            $"ComposeData row 0 from '{member}' for '{method}' failed: " +
            $"Row value for parameter 'label' on '{method}' has type 'System.Int32', which is not assignable to 'System.String'.";

        AssertFailureWithRowSeedLine(exception.Message, expectedPrefix);
    }

    [Fact]
    public async Task MemberReadThrows_ReportsMemberReadFailure()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Read_throws_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.ReadThrowsRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Read_throws_fails));

        Assert.Equal(
            $"ComposeData member '{member}' for '{method}' failed while reading its value: read boom",
            exception.Message);
        Assert.Null(exception.Diagnostic);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("read boom", exception.InnerException!.Message);
    }

    [Fact]
    public async Task MemberReadThrowsCompositionException_PreservesDiagnostic()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Read_throws_composition_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.ReadThrowsCompositionRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Read_throws_composition_fails));

        Assert.Equal(
            $"ComposeData member '{member}' for '{method}' failed while reading its value: diagnosed read failure",
            exception.Message);
        Assert.Same(ComposeDataEmissionFailureTargets.ReadDiagnostic, exception.Diagnostic);
        Assert.IsType<CompositionException>(exception.InnerException);
    }

    [Fact]
    public async Task NullCollection_ReportsNullResult()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Null_collection_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.NullCollectionRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Null_collection_fails));

        Assert.Equal(
            $"ComposeData member '{member}' for '{method}' returned null.",
            exception.Message);
        Assert.Null(exception.Diagnostic);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task EnumerationThrows_ReportsMemberReadFailure()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Enumeration_throws_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.EnumerationThrowsRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Enumeration_throws_fails));

        Assert.Equal(
            $"ComposeData member '{member}' for '{method}' failed while reading its value: enumeration boom",
            exception.Message);
        Assert.Null(exception.Diagnostic);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task EnumerationThrowsCompositionException_PropagatesUnwrapped()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Enumeration_throws_composition_fails)).AsTask());

        Assert.Same(ComposeDataEmissionFailureTargets.EnumerationDiagnostic, exception.Diagnostic);
        Assert.Equal("diagnosed enumeration failure", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task TwoRows_ProduceDistinctSeedsAndFreshComposition()
    {
        var rows = await RegisteredComposeDataFactory.InvokeAsync(
            FailureTargetsIndex,
            nameof(ComposeDataEmissionFailureTargets.Two_rows_compose_independently));

        Assert.Equal(2, rows.Count);
        var rowArray = rows.ToArray();
        var first = Assert.IsType<TheoryDataRow>(rowArray[0]);
        var second = Assert.IsType<TheoryDataRow>(rowArray[1]);

        var firstSeed = first.Traits[ComposeDataRows.SeedTraitName].Single();
        var secondSeed = second.Traits[ComposeDataRows.SeedTraitName].Single();
        Assert.NotEqual(firstSeed, secondSeed);

        var firstData = rowArray[0].GetData();
        var secondData = rowArray[1].GetData();
        Assert.Equal("one", firstData[0]);
        Assert.Equal("two", secondData[0]);
        Assert.NotNull(firstData[1]);
        Assert.NotNull(secondData[1]);
        Assert.NotSame(firstData[1], secondData[1]);
    }

    [Fact]
    public async Task ZeroParameterMethod_ComposesEmptyRow()
    {
        var rows = await RegisteredComposeDataFactory.InvokeAsync(
            FailureTargetsIndex,
            nameof(ComposeDataEmissionFailureTargets.Zero_parameter_method_is_composed));

        var row = Assert.Single(rows);
        Assert.Empty(row.GetData());
        var dataRow = Assert.IsType<TheoryDataRow>(row);
        Assert.Single(dataRow.Traits[ComposeDataRows.SeedTraitName]);
    }

    [Fact]
    public async Task AttributeSeed_IsAppliedToEveryRow()
    {
        var rows = await RegisteredComposeDataFactory.InvokeAsync(
            FailureTargetsIndex,
            nameof(ComposeDataEmissionFailureTargets.Attribute_seed_is_applied));

        Assert.Equal(2, rows.Count);
        var rowArray = rows.ToArray();

        foreach (var row in rowArray)
        {
            var dataRow = Assert.IsType<TheoryDataRow>(row);
            Assert.Equal("42", dataRow.Traits[ComposeDataRows.SeedTraitName].Single());
            Assert.Equal("seeded", row.GetData()[0]);
        }
    }

    [Fact]
    public async Task ProfileWithSeed_IsReportedAsTheRowSeed()
    {
        var rows = await RegisteredComposeDataFactory.InvokeAsync(
            FailureTargetsIndex,
            nameof(ComposeDataEmissionFailureTargets.Profile_seed_is_applied));

        var dataRow = Assert.IsType<TheoryDataRow>(Assert.Single(rows));
        Assert.Equal("1234", dataRow.Traits[ComposeDataRows.SeedTraitName].Single());
    }

    [Fact]
    public async Task AttributeSeedZero_IsAppliedAndReported()
    {
        var rows = await RegisteredComposeDataFactory.InvokeAsync(
            FailureTargetsIndex,
            nameof(ComposeDataEmissionFailureTargets.Zero_seed_is_applied));

        var row = Assert.Single(rows);
        var dataRow = Assert.IsType<TheoryDataRow>(row);
        Assert.Equal("0", dataRow.Traits[ComposeDataRows.SeedTraitName].Single());
        Assert.Equal("zero", row.GetData()[0]);
    }

    [Fact]
    public async Task SeededConfigExtractionFailure_ReportsAttributeSeedInContext()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Seeded_config_extraction_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.SeededConfigExtractionRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Seeded_config_extraction_fails));
        var expected =
            $"ComposeData row 0 from '{member}' for '{method}' failed: " +
            $"the first row element must be a non-null '{typeof(WidgetConfig)}' profile configuration value" +
            "\n\nSeed: 42";

        Assert.Equal(expected, exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Null(exception.Diagnostic);
    }

    [Fact]
    public async Task SeededPositionalValueFailure_ReportsAttributeSeedAsTheRowSeed()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Seeded_type_mismatch_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.SeededTypeMismatchRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Seeded_type_mismatch_fails));
        var expected =
            $"ComposeData row 0 from '{member}' for '{method}' failed: " +
            $"Row value for parameter 'label' on '{method}' has type 'System.Int32', which is not assignable to 'System.String'." +
            "\n\nSeed: 42";

        Assert.Equal(expected, exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Null(exception.Diagnostic);
    }

    [Fact]
    public async Task ThrowingProfileConstructor_ReportsPreCompositionFailureWithInnerException()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Throwing_profile_constructor_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.ThrowingProfileRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Throwing_profile_constructor_fails));
        var expected =
            $"ComposeData row 0 from '{member}' for '{method}' failed: profile constructor boom" +
            "\n\nSeed: not assigned (the failure occurred before the row's composition started)";

        Assert.Equal(expected, exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("profile constructor boom", exception.InnerException!.Message);
        Assert.Null(exception.Diagnostic);
    }

    [Fact]
    public async Task ThrowingProfileConstructor_PreservesCompositionExceptionDiagnostic()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Diagnosed_profile_constructor_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.ThrowingProfileRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Diagnosed_profile_constructor_fails));
        var expected =
            $"ComposeData row 0 from '{member}' for '{method}' failed: diagnosed constructor failure" +
            "\n\nSeed: not assigned (the failure occurred before the row's composition started)";

        Assert.Equal(expected, exception.Message);
        Assert.Same(DiagnosedThrowingCtorProfile.Diagnostic, exception.Diagnostic);
        var inner = Assert.IsType<CompositionException>(exception.InnerException);
        Assert.Same(DiagnosedThrowingCtorProfile.Diagnostic, inner.Diagnostic);
    }

    [Fact]
    public async Task SharedRows_DoNotShareInstancesAcrossRowBoundaries()
    {
        var rows = await RegisteredComposeDataFactory.InvokeAsync(
            FailureTargetsIndex,
            nameof(ComposeDataEmissionFailureTargets.Shared_rows_do_not_share_across_rows));

        Assert.Equal(2, rows.Count);
        var rowArray = rows.ToArray();
        Assert.NotSame(rowArray[0].GetData()[0], rowArray[1].GetData()[0]);
    }

    [Fact]
    public async Task ProfileConstructorIsInvokedOncePerRow()
    {
        var before = CountingWidgetProfile.InstanceCount;

        var rows = await RegisteredComposeDataFactory.InvokeAsync(
            FailureTargetsIndex,
            nameof(ComposeDataEmissionFailureTargets.Profile_is_constructed_once_per_row));

        Assert.Equal(2, rows.Count);
        Assert.Equal(before + 2, CountingWidgetProfile.InstanceCount);

        var rowArray = rows.ToArray();
        var firstName = Assert.IsType<OrderService>(rowArray[0].GetData()[0]).Name;
        var secondName = Assert.IsType<OrderService>(rowArray[1].GetData()[0]).Name;
        Assert.NotEqual(firstName, secondName);
        Assert.NotSame(rowArray[0].GetData()[0], rowArray[1].GetData()[0]);
    }

    [Fact]
    public async Task NegativeAttributeSeed_FailsBeforeReadingOrComposing()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Negative_attribute_seed_fails)).AsTask());

        Assert.Equal(
            "Compono.XunitV3 requires a non-negative seed, but the configured seed was -1." +
            "\n\nSeed: -1",
            exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Null(exception.Diagnostic);
    }

    [Fact]
    public async Task ProfileWithNegativeSeed_ReportsNegativeRowSeed()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Negative_profile_seed_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.NegativeProfileSeedRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Negative_profile_seed_fails));
        var expected =
            $"ComposeData row 0 from '{member}' for '{method}' failed: " +
            "Compono.XunitV3 requires a non-negative seed, but the configured seed was -5." +
            "\n\nSeed: -5";

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task AttributeSeedAndProfileSeed_ReportDuplicateConfiguration()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Duplicate_seed_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.DuplicateSeedRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Duplicate_seed_fails));
        var expectedPrefix = $"ComposeData row 0 from '{member}' for '{method}' failed: ";

        Assert.StartsWith(expectedPrefix, exception.Message);
        Assert.Contains("was configured more than once", exception.Message);
        Assert.EndsWith("\n\nSeed: 9", exception.Message);
        Assert.IsType<CompositionConfigurationException>(exception.InnerException);
    }

    [Fact]
    public async Task ConfigExtractionFailure_ReportsPreRowSeedLine()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Config_extraction_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.ConfigExtractionRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Config_extraction_fails));
        var expectedPrefix =
            $"ComposeData row 0 from '{member}' for '{method}' failed: " +
            $"the first row element must be a non-null '{typeof(WidgetConfig)}' profile configuration value";

        Assert.StartsWith(expectedPrefix, exception.Message);
        Assert.EndsWith(
            "\n\nSeed: not assigned (the failure occurred before the row's composition started)",
            exception.Message);
    }

    // ---------------------------------------------------------------------------------------------
    // S4: asynchronous member sources. Success paths are ordinary [Theory] targets the runner
    // executes directly; the generated factory awaits the member (Task<T>/ValueTask<T>) or the
    // async-enumerable reader before composing rows.
    // ---------------------------------------------------------------------------------------------

    public static async Task<IEnumerable<object[]>> TaskSourceRows()
    {
        await Task.Yield();
        return [new object[] { "task" }];
    }

    [Theory]
    [ComposeData(nameof(TaskSourceRows))]
    public void Task_source_rows_are_composed(string label) => Assert.Equal("task", label);

    public static Task<object> TaskObjectRows() =>
        Task.FromResult<object>(new object[] { new object[] { "task-object" } });

    [Theory]
    [ComposeData(nameof(TaskObjectRows))]
    public void Task_object_source_rows_are_composed(string label) => Assert.Equal("task-object", label);

    public static ValueTask<IEnumerable<object[]>> ValueTaskSourceRows() =>
        new([new object[] { "value-task" }]);

    [Theory]
    [ComposeData(nameof(ValueTaskSourceRows))]
    public void ValueTask_source_rows_are_composed(string label) => Assert.Equal("value-task", label);

    public static async IAsyncEnumerable<object[]> AsyncReferenceRows()
    {
        await Task.Yield();
        yield return new object[] { "async-ref" };
    }

    [Theory]
    [ComposeData(nameof(AsyncReferenceRows))]
    public void Async_reference_element_rows_are_composed(string label) => Assert.Equal("async-ref", label);

    public static async IAsyncEnumerable<(int Quantity, string Value)> AsyncValueRows()
    {
        await Task.Yield();
        yield return (5, "async-value");
    }

    [Theory]
    [ComposeData(nameof(AsyncValueRows))]
    public void Async_value_element_rows_are_composed(int quantity, string value)
    {
        Assert.Equal(5, quantity);
        Assert.Equal("async-value", value);
    }

    public static async Task<IEnumerable<object[]>> TaskProfileRows()
    {
        await Task.Yield();
        return [Array.Empty<object>()];
    }

    [Theory]
    [ComposeData<PersistentWidgetProfile>(nameof(TaskProfileRows))]
    public void Async_source_combines_with_profile(OrderService service) =>
        Assert.Equal("profile-registered", service.Name);

    public static async Task<IEnumerable<object?[]>> TaskConfigRows()
    {
        await Task.Yield();
        return [new object?[] { new WidgetConfig("async-widget", 9), "async-label" }];
    }

    [Theory]
    [ComposeData<ConfiguredWidgetProfile, WidgetConfig>(nameof(TaskConfigRows))]
    public void Async_source_combines_with_profile_config(string label, OrderService service, int quantity)
    {
        Assert.Equal("async-label", label);
        Assert.Equal("async-widget", service.Name);
        Assert.Equal(9, quantity);
    }

    public static async Task<IEnumerable<object[]>> TaskSharedRows()
    {
        await Task.Yield();
        return [Array.Empty<object>()];
    }

    [Theory]
    [ComposeData(nameof(TaskSharedRows))]
    public void Async_source_combines_with_shared_parameter([Shared] OrderService shared, OrderService repeat)
    {
        Assert.Same(shared, repeat);
        Assert.False(string.IsNullOrEmpty(shared.Name));
    }

    [Fact]
    public async Task AsyncAttributeSeed_IsAppliedToEveryRow()
    {
        var rows = await RegisteredComposeDataFactory.InvokeAsync(
            FailureTargetsIndex,
            nameof(ComposeDataEmissionFailureTargets.Async_attribute_seed_is_applied));

        Assert.Equal(2, rows.Count);
        foreach (var row in rows)
        {
            var dataRow = Assert.IsType<TheoryDataRow>(row);
            Assert.Equal("55", dataRow.Traits[ComposeDataRows.SeedTraitName].Single());
            Assert.Equal("async-seed", row.GetData()[0]);
        }
    }

    [Fact]
    public async Task AsyncRows_PreserveOrderAfterMaterialization()
    {
        var rows = await RegisteredComposeDataFactory.InvokeAsync(
            FailureTargetsIndex,
            nameof(ComposeDataEmissionFailureTargets.Async_rows_preserve_order));

        Assert.Equal(3, rows.Count);
        var rowArray = rows.ToArray();
        Assert.Equal("first", rowArray[0].GetData()[0]);
        Assert.Equal("second", rowArray[1].GetData()[0]);
        Assert.Equal("third", rowArray[2].GetData()[0]);
    }

    [Fact]
    public async Task AsyncMixedReferenceAndValue_TakesTheReferenceReaderAtRuntime()
    {
        // Declared member type is IAsyncEnumerable<(int, string)> (a value element -> the value reader
        // is generated), but the runtime object also implements IAsyncEnumerable<string>. IAsyncEnumerable
        // is covariant, so the runtime object is an IAsyncEnumerable<object?> and the generated value
        // reader takes the reference path first - exactly the JIT runtime-object path selection.
        //
        // The reference reader therefore reads the string rows, and each string is then rejected as an
        // unsupported row shape (a string is not ITheoryDataRow, object?[], or ITuple). Had the value
        // reader been taken instead, its GetAsyncEnumerator would have thrown the mixed source's
        // "value-element path" guard, so this failure proves the reference path was selected.
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_mixed_rows_read_reference_path)).AsTask());

        Assert.Contains("row has unsupported type 'System.String'", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("value-element path", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AsyncNullTask_ReportsNullResult()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_null_task_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.NullTaskRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Async_null_task_fails));

        Assert.Equal(
            $"ComposeData member '{member}' for '{method}' returned null.",
            exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Null(exception.Diagnostic);
    }

    [Fact]
    public async Task AsyncTaskReadThrows_ReportsMemberReadFailure()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_task_read_throws_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.ThrowingTaskRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Async_task_read_throws_fails));

        Assert.Equal(
            $"ComposeData member '{member}' for '{method}' failed while reading its value: task read boom",
            exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task AsyncTaskReadThrowsCompositionException_IsWrappedAndPreservesDiagnostic()
    {
        // A member that throws CompositionException while being invoked is still wrapped by ReadMember
        // (which only lets OperationCanceledException through), carrying the diagnostic onto the
        // MemberReadFailure - the JIT InvokeRead behaviour.
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_task_read_throws_composition_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.ThrowingTaskCompositionRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Async_task_read_throws_composition_fails));

        Assert.Equal(
            $"ComposeData member '{member}' for '{method}' failed while reading its value: diagnosed task read failure",
            exception.Message);
        Assert.Same(ComposeDataEmissionFailureTargets.TaskReadDiagnostic, exception.Diagnostic);
        Assert.IsType<CompositionException>(exception.InnerException);
    }

    [Fact]
    public async Task AsyncTaskAwaitThrows_ReportsMemberReadFailure()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_task_await_throws_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.ThrowingAwaitTaskRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Async_task_await_throws_fails));

        Assert.Equal(
            $"ComposeData member '{member}' for '{method}' failed while reading its value: await boom",
            exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task AsyncValueTaskAwaitThrows_ReportsMemberReadFailure()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_value_task_await_throws_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.ThrowingAwaitValueTaskRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Async_value_task_await_throws_fails));

        Assert.Equal(
            $"ComposeData member '{member}' for '{method}' failed while reading its value: await value-task boom",
            exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task AsyncTaskInvalidRow_ReportsRowFailure()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_task_null_value_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.TaskNullValueRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Async_task_null_value_fails));
        var expectedPrefix =
            $"ComposeData row 0 from '{member}' for '{method}' failed: " +
            $"Row value for parameter 'label' on '{method}' is null, but the parameter is not nullable.";

        AssertFailureWithRowSeedLine(exception.Message, expectedPrefix);
    }

    [Fact]
    public async Task AsyncReferenceEnumerationThrows_ReportsMemberReadFailure()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_reference_enumeration_throws_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.ThrowingAsyncReferenceEnumeratorRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Async_reference_enumeration_throws_fails));

        Assert.Equal(
            $"ComposeData member '{member}' for '{method}' failed while reading its value: async enumeration boom",
            exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task AsyncReferenceEnumerationThrowsCompositionException_PropagatesUnwrapped()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_reference_enumeration_throws_composition_fails)).AsTask());

        Assert.Same(ComposeDataEmissionFailureTargets.AsyncEnumerationDiagnostic, exception.Diagnostic);
        Assert.Equal("diagnosed async enumeration failure", exception.Message);
    }

    [Fact]
    public async Task AsyncValueEnumerationThrows_ReportsMemberReadFailure()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_value_enumeration_throws_fails)).AsTask());

        var member = MemberDisplay(nameof(ComposeDataEmissionFailureTargets.ThrowingAsyncValueEnumeratorRows));
        var method = MethodDisplay(nameof(ComposeDataEmissionFailureTargets.Async_value_enumeration_throws_fails));

        Assert.Equal(
            $"ComposeData member '{member}' for '{method}' failed while reading its value: async value enumeration boom",
            exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task AsyncValueEnumerationCancelled_PropagatesOperationCanceled()
    {
        // A value-element async enumerable that faults with OperationCanceledException: the generated
        // manual loop captures the failure, disposes the enumerator, then rethrows it verbatim (never
        // wrapped in a MemberReadFailure) - the JIT AppendAsyncByReflectionAsync rule.
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_value_enumeration_cancelled)).AsTask());

        Assert.Equal("async value cancellation", exception.Message);
    }

    [Fact]
    public async Task AsyncReferenceEnumerationCancelled_PropagatesOperationCanceled()
    {
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_reference_enumeration_cancelled)).AsTask());

        Assert.Equal("async reference cancellation", exception.Message);
    }

    // ---------------------------------------------------------------------------------------------
    // S4 review: awaited CompositionException passes through unwrapped (both Task<T> and
    // ValueTask<T>). This is distinct from a synchronous member-invocation failure, which ReadMember
    // wraps - see AsyncTaskReadThrowsCompositionException_IsWrappedAndPreservesDiagnostic.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task AsyncTaskAwaitThrowsCompositionException_PropagatesUnwrapped()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_task_await_throws_composition_fails)).AsTask());

        // The await rethrows the fault verbatim: the same diagnostic, the original message, and no
        // extra wrapping.
        Assert.Equal("diagnosed await failure", exception.Message);
        Assert.Same(ComposeDataEmissionFailureTargets.AwaitCompositionDiagnostic, exception.Diagnostic);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task AsyncValueTaskAwaitThrowsCompositionException_PropagatesUnwrapped()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_value_task_await_throws_composition_fails)).AsTask());

        Assert.Equal("diagnosed await failure", exception.Message);
        Assert.Same(ComposeDataEmissionFailureTargets.AwaitCompositionDiagnostic, exception.Diagnostic);
        Assert.Null(exception.InnerException);
    }

    // ---------------------------------------------------------------------------------------------
    // S4 review: GetAsyncEnumerator exceptions. The reference reader's filter exempts
    // CompositionException (passes through); the value reader's filter exempts only
    // OperationCanceledException (wraps), and the wrapper still preserves the diagnostic and exposes
    // the original as InnerException.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task AsyncReferenceGetEnumeratorThrowsCompositionException_PropagatesUnwrapped()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_reference_get_enumerator_throws_composition_fails)).AsTask());

        Assert.Equal("diagnosed reference GetAsyncEnumerator failure", exception.Message);
        Assert.Same(ComposeDataEmissionFailureTargets.ReferenceEnumeratorDiagnostic, exception.Diagnostic);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task AsyncValueGetEnumeratorThrowsCompositionException_IsWrappedWithDiagnostic()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_value_get_enumerator_throws_composition_fails)).AsTask());

        Assert.StartsWith(
            $"ComposeData member '{MemberDisplay(nameof(ComposeDataEmissionFailureTargets.ThrowingValueGetEnumeratorRows))}'",
            exception.Message);
        Assert.Contains(
            "failed while reading its value: diagnosed value GetAsyncEnumerator failure",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Same(ComposeDataEmissionFailureTargets.ValueEnumeratorDiagnostic, exception.Diagnostic);

        var inner = Assert.IsType<CompositionException>(exception.InnerException);
        Assert.Same(ComposeDataEmissionFailureTargets.ValueEnumeratorDiagnostic, inner.Diagnostic);
    }

    // ---------------------------------------------------------------------------------------------
    // S4 review: DisposeAsync failure precedence. The reference reader uses await foreach, so a
    // disposal failure replaces the original enumeration failure (the finally wins). The value
    // reader's manual loop keeps the original failure, and a disposal failure after a successful
    // enumeration becomes the reader failure.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task AsyncReferenceDisposeFailure_ReplacesEnumerationFailure()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_reference_dispose_replaces_failure)).AsTask());

        Assert.Contains("reference dispose boom", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("original reference enumeration", exception.Message, StringComparison.Ordinal);
        Assert.Equal(
            "reference dispose boom",
            Assert.IsType<InvalidOperationException>(exception.InnerException).Message);
    }

    [Fact]
    public async Task AsyncValueDisposeFailure_KeepsOriginalEnumerationFailure()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_value_dispose_keeps_original_failure)).AsTask());

        Assert.Contains("original value enumeration", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("value dispose boom", exception.Message, StringComparison.Ordinal);
        Assert.Equal(
            "original value enumeration",
            Assert.IsType<InvalidOperationException>(exception.InnerException).Message);
    }

    [Fact]
    public async Task AsyncValueDisposeFailureAfterSuccess_ReportsMemberReadFailure()
    {
        var exception = await Assert.ThrowsAsync<CompositionException>(
            () => RegisteredComposeDataFactory.InvokeAsync(
                FailureTargetsIndex,
                nameof(ComposeDataEmissionFailureTargets.Async_value_dispose_failure_after_success)).AsTask());

        Assert.Contains("value dispose after success boom", exception.Message, StringComparison.Ordinal);
        Assert.Equal(
            "value dispose after success boom",
            Assert.IsType<InvalidOperationException>(exception.InnerException).Message);
    }

    // ---------------------------------------------------------------------------------------------
    // S4 review: a source implementing both the value- and reference-element IAsyncEnumerable<T>
    // interfaces takes the reference path and reads its valid rows - here declared concretely, and
    // again via runtime covariance where the declared element is a value type.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task StaticMixedSource_ReadsReferenceRowsInOrder()
    {
        var rows = await RegisteredComposeDataFactory.InvokeAsync(
            FailureTargetsIndex,
            nameof(ComposeDataEmissionFailureTargets.Static_mixed_source_reads_reference_rows));

        Assert.Equal(2, rows.Count);
        var rowArray = rows.ToArray();
        Assert.Equal("dual-a", rowArray[0].GetData()[0]);
        Assert.Equal("dual-b", rowArray[1].GetData()[0]);
    }

    [Fact]
    public async Task RuntimeCovariance_ReadsReferenceRowsInOrder()
    {
        // Declared member type is IAsyncEnumerable<(int, string)> (a value element, so the generator
        // emits the value reader), but the runtime object also implements IAsyncEnumerable<object[]> and
        // is therefore an IAsyncEnumerable<object?> via covariance - the reference path is selected and
        // reads the object[]-side rows successfully. The value path's GetAsyncEnumerator would have
        // thrown the mixed source's guard, so a successful read proves the reference path was taken.
        var rows = await RegisteredComposeDataFactory.InvokeAsync(
            FailureTargetsIndex,
            nameof(ComposeDataEmissionFailureTargets.Async_covariant_rows_read_reference_path));

        Assert.Equal(2, rows.Count);
        var rowArray = rows.ToArray();
        Assert.Equal("dual-a", rowArray[0].GetData()[0]);
        Assert.Equal("dual-b", rowArray[1].GetData()[0]);
    }

    private const string FailureTargetsIndex =
        "global::Compono.XunitV3.Aot.Tests.ComposeDataEmissionFailureTargets";

    private static string MemberDisplay(string member) =>
        $"{typeof(ComposeDataEmissionFailureTargets).FullName}.{member}";

    private static string MethodDisplay(string method) =>
        $"{typeof(ComposeDataEmissionFailureTargets).FullName}.{method}";

    private static void AssertFailureWithRowSeedLine(string message, string expectedPrefix)
    {
        Assert.StartsWith(expectedPrefix, message);
        Assert.Matches(@"\n\nSeed: \d+$", message);
    }
}

public static class ComposeDataExternalSource
{
    public static IEnumerable<object[]> ExternalRows() => [new object[] { "external" }];
}

public abstract class ComposeDataEmissionBase
{
    public static IEnumerable<object[]> InheritedRows() => [new object[] { "inherited" }];
}

public sealed class ComposeDataEmissionFailureTargets
{
    public static IEnumerable<object?> NullRowRows() => [null];

    [ComposeData(nameof(NullRowRows))]
    public void Null_row_fails(string label) => _ = label;

    public static IEnumerable<object[]> TooManyValueRows() =>
    [
        new object[] { "a", 1, true },
    ];

    [ComposeData(nameof(TooManyValueRows))]
    public void Too_many_values_fails(string label, int quantity)
    {
        _ = label;
        _ = quantity;
    }

    public static IEnumerable<object?[]> NullValueRows() => [new object?[] { null }];

    [ComposeData(nameof(NullValueRows))]
    public void Null_value_fails(string label) => _ = label;

    public static IEnumerable<object[]> TypeMismatchRows() => [new object[] { 42 }];

    [ComposeData(nameof(TypeMismatchRows))]
    public void Type_mismatch_fails(string label) => _ = label;

    public static IEnumerable<object[]> MetadataRows() => [new object[] { "metadata" }];

    [ComposeData(
        nameof(MetadataRows),
        Label = "the label",
        TestDisplayName = "the display",
        Timeout = 42,
        Explicit = true,
        DisableParallelization = true,
        Traits = new[] { "category", "pricing" })]
    public void Metadata_rows_carry_attribute_metadata(string label) => _ = label;

    public static CompositionDiagnostic ReadDiagnostic { get; } = new()
    {
        RootType = typeof(string),
        FailedType = typeof(string),
        Path = "string",
        Trace = Array.Empty<ProviderAttempt>(),
        Seed = 1,
        Message = "diagnosed read failure",
    };

    public static IEnumerable<object[]> ReadThrowsRows() =>
        throw new InvalidOperationException("read boom");

    [ComposeData(nameof(ReadThrowsRows))]
    public void Read_throws_fails(string label) => _ = label;

    public static IEnumerable<object[]> ReadThrowsCompositionRows() =>
        throw new CompositionException(ReadDiagnostic);

    [ComposeData(nameof(ReadThrowsCompositionRows))]
    public void Read_throws_composition_fails(string label) => _ = label;

    public static IEnumerable<object[]> NullCollectionRows() => null!;

    [ComposeData(nameof(NullCollectionRows))]
    public void Null_collection_fails(string label) => _ = label;

    public static IEnumerable<object[]> EnumerationThrowsRows()
    {
        yield return new object[] { "first" };
        throw new InvalidOperationException("enumeration boom");
    }

    [ComposeData(nameof(EnumerationThrowsRows))]
    public void Enumeration_throws_fails(string label) => _ = label;

    public static CompositionDiagnostic EnumerationDiagnostic { get; } = new()
    {
        RootType = typeof(string),
        FailedType = typeof(string),
        Path = "string",
        Trace = Array.Empty<ProviderAttempt>(),
        Seed = 2,
        Message = "diagnosed enumeration failure",
    };

    public static IEnumerable<object[]> EnumerationThrowsCompositionRows()
    {
        yield return new object[] { "first" };
        throw new CompositionException(EnumerationDiagnostic);
    }

    [ComposeData(nameof(EnumerationThrowsCompositionRows))]
    public void Enumeration_throws_composition_fails(string label) => _ = label;

    public static IEnumerable<object[]> TwoRows() =>
    [
        new object[] { "one" },
        new object[] { "two" },
    ];

    [ComposeData(nameof(TwoRows))]
    public void Two_rows_compose_independently(string label, OrderService service)
    {
        _ = label;
        _ = service;
    }

    public static IEnumerable<object[]> ZeroParameterRows() => [Array.Empty<object>()];

    [ComposeData(nameof(ZeroParameterRows))]
    public void Zero_parameter_method_is_composed() { }

    public static IEnumerable<object[]> AttributeSeedRows() =>
    [
        new object[] { "seeded" },
        new object[] { "seeded" },
    ];

    [ComposeData(nameof(AttributeSeedRows), Seed = 42)]
    public void Attribute_seed_is_applied(string label) => _ = label;

    public static IEnumerable<object[]> ProfileSeedRows() => [Array.Empty<object>()];

    [ComposeData<SeededWidgetProfile>(nameof(ProfileSeedRows))]
    public void Profile_seed_is_applied() { }

    public static IEnumerable<object[]> NegativeAttributeSeedRows() =>
        throw new InvalidOperationException("the member must never be read when the attribute seed is negative");

    [ComposeData(nameof(NegativeAttributeSeedRows), Seed = -1)]
    public void Negative_attribute_seed_fails(string label) => _ = label;

    public static IEnumerable<object[]> NegativeProfileSeedRows() => [Array.Empty<object>()];

    [ComposeData<NegativeSeedWidgetProfile>(nameof(NegativeProfileSeedRows))]
    public void Negative_profile_seed_fails() { }

    public static IEnumerable<object[]> DuplicateSeedRows() => [Array.Empty<object>()];

    [ComposeData<SeededWidgetProfile>(nameof(DuplicateSeedRows), Seed = 9)]
    public void Duplicate_seed_fails() { }

    public static IEnumerable<object[]> ConfigExtractionRows() => [new object[] { "not-a-config" }];

    [ComposeData<ConfiguredWidgetProfile, WidgetConfig>(nameof(ConfigExtractionRows))]
    public void Config_extraction_fails(string label) => _ = label;

    public static IEnumerable<object[]> ZeroSeedRows() => [new object[] { "zero" }];

    [ComposeData(nameof(ZeroSeedRows), Seed = 0)]
    public void Zero_seed_is_applied(string label) => _ = label;

    public static IEnumerable<object[]> SeededConfigExtractionRows() => [new object[] { "not-a-config" }];

    [ComposeData<ConfiguredWidgetProfile, WidgetConfig>(nameof(SeededConfigExtractionRows), Seed = 42)]
    public void Seeded_config_extraction_fails(string label) => _ = label;

    public static IEnumerable<object[]> SeededTypeMismatchRows() => [new object[] { 42 }];

    [ComposeData(nameof(SeededTypeMismatchRows), Seed = 42)]
    public void Seeded_type_mismatch_fails(string label) => _ = label;

    public static IEnumerable<object?[]> ThrowingProfileRows() =>
        [new object?[] { new WidgetConfig("boom", 1) }];

    [ComposeData<ThrowingCtorProfile, WidgetConfig>(nameof(ThrowingProfileRows))]
    public void Throwing_profile_constructor_fails(string label) => _ = label;

    [ComposeData<DiagnosedThrowingCtorProfile, WidgetConfig>(nameof(ThrowingProfileRows))]
    public void Diagnosed_profile_constructor_fails(string label) => _ = label;

    public static IEnumerable<object[]> TwoSharedRows() => [Array.Empty<object>(), Array.Empty<object>()];

    [ComposeData(nameof(TwoSharedRows))]
    public void Shared_rows_do_not_share_across_rows([Shared] OrderService shared) => _ = shared;

    public static IEnumerable<object[]> TwoProfileRows() => [Array.Empty<object>(), Array.Empty<object>()];

    [ComposeData<CountingWidgetProfile>(nameof(TwoProfileRows))]
    public void Profile_is_constructed_once_per_row(OrderService service) => _ = service;

    // ---------------------------------------------------------------------------------------------
    // S4: asynchronous member sources - failure / metadata paths driven through the registered
    // factory from the [Fact] drivers above.
    // ---------------------------------------------------------------------------------------------

    public static CompositionDiagnostic TaskReadDiagnostic { get; } = new()
    {
        RootType = typeof(string),
        FailedType = typeof(string),
        Path = "string",
        Trace = Array.Empty<ProviderAttempt>(),
        Seed = 3,
        Message = "diagnosed task read failure",
    };

    public static Task<IEnumerable<object[]>> NullTaskRows() => null!;

    [ComposeData(nameof(NullTaskRows))]
    public void Async_null_task_fails(string label) => _ = label;

    public static Task<IEnumerable<object[]>> ThrowingTaskRows() =>
        throw new InvalidOperationException("task read boom");

    [ComposeData(nameof(ThrowingTaskRows))]
    public void Async_task_read_throws_fails(string label) => _ = label;

    public static Task<IEnumerable<object[]>> ThrowingTaskCompositionRows() =>
        throw new CompositionException(TaskReadDiagnostic);

    [ComposeData(nameof(ThrowingTaskCompositionRows))]
    public void Async_task_read_throws_composition_fails(string label) => _ = label;

    public static async Task<IEnumerable<object[]>> ThrowingAwaitTaskRows()
    {
        await Task.Yield();
        throw new InvalidOperationException("await boom");
    }

    [ComposeData(nameof(ThrowingAwaitTaskRows))]
    public void Async_task_await_throws_fails(string label) => _ = label;

    public static async ValueTask<IEnumerable<object[]>> ThrowingAwaitValueTaskRows()
    {
        await Task.Yield();
        throw new InvalidOperationException("await value-task boom");
    }

    [ComposeData(nameof(ThrowingAwaitValueTaskRows))]
    public void Async_value_task_await_throws_fails(string label) => _ = label;

    public static async Task<IEnumerable<object?[]>> TaskNullValueRows()
    {
        await Task.Yield();
        return [new object?[] { null }];
    }

    [ComposeData(nameof(TaskNullValueRows))]
    public void Async_task_null_value_fails(string label) => _ = label;

    public static async IAsyncEnumerable<object[]> ThrowingAsyncReferenceEnumeratorRows()
    {
        await Task.Yield();
        yield return new object[] { "first" };
        throw new InvalidOperationException("async enumeration boom");
    }

    [ComposeData(nameof(ThrowingAsyncReferenceEnumeratorRows))]
    public void Async_reference_enumeration_throws_fails(string label) => _ = label;

    public static CompositionDiagnostic AsyncEnumerationDiagnostic { get; } = new()
    {
        RootType = typeof(string),
        FailedType = typeof(string),
        Path = "string",
        Trace = Array.Empty<ProviderAttempt>(),
        Seed = 4,
        Message = "diagnosed async enumeration failure",
    };

    public static async IAsyncEnumerable<object[]> ThrowingAsyncReferenceEnumeratorCompositionRows()
    {
        await Task.Yield();
        yield return new object[] { "first" };
        throw new CompositionException(AsyncEnumerationDiagnostic);
    }

    [ComposeData(nameof(ThrowingAsyncReferenceEnumeratorCompositionRows))]
    public void Async_reference_enumeration_throws_composition_fails(string label) => _ = label;

    public static async IAsyncEnumerable<(int Quantity, string Value)> ThrowingAsyncValueEnumeratorRows()
    {
        await Task.Yield();
        yield return (1, "first");
        throw new InvalidOperationException("async value enumeration boom");
    }

    [ComposeData(nameof(ThrowingAsyncValueEnumeratorRows))]
    public void Async_value_enumeration_throws_fails(int quantity, string value)
    {
        _ = quantity;
        _ = value;
    }

    public static async IAsyncEnumerable<(int Quantity, string Value)> CancelledAsyncValueRows()
    {
        await Task.Yield();
        yield return (0, "cancelled");
        throw new OperationCanceledException("async value cancellation");
    }

    [ComposeData(nameof(CancelledAsyncValueRows))]
    public void Async_value_enumeration_cancelled(int quantity, string value)
    {
        _ = quantity;
        _ = value;
    }

    public static async IAsyncEnumerable<object[]> CancelledAsyncReferenceRows()
    {
        await Task.Yield();
        yield return new object[] { "cancelled" };
        throw new OperationCanceledException("async reference cancellation");
    }

    [ComposeData(nameof(CancelledAsyncReferenceRows))]
    public void Async_reference_enumeration_cancelled(string label) => _ = label;

    public static async Task<IEnumerable<object[]>> AsyncSeedRows()
    {
        await Task.Yield();
        return [new object[] { "async-seed" }, new object[] { "async-seed" }];
    }

    [ComposeData(nameof(AsyncSeedRows), Seed = 55)]
    public void Async_attribute_seed_is_applied(string label) => _ = label;

    public static async Task<IEnumerable<object[]>> OrderedAsyncRows()
    {
        await Task.Yield();
        return
        [
            new object[] { "first" },
            new object[] { "second" },
            new object[] { "third" },
        ];
    }

    [ComposeData(nameof(OrderedAsyncRows))]
    public void Async_rows_preserve_order(string label) => _ = label;

    public static IAsyncEnumerable<(int Quantity, string Value)> MixedAsyncIntStringRowsSource() =>
        new MixedAsyncIntStringRows();

    [ComposeData(nameof(MixedAsyncIntStringRowsSource))]
    public void Async_mixed_rows_read_reference_path(string value) => _ = value;

    // ---------------------------------------------------------------------------------------------
    // S4 review additions.
    // ---------------------------------------------------------------------------------------------

    public static CompositionDiagnostic AwaitCompositionDiagnostic { get; } = new()
    {
        RootType = typeof(string),
        FailedType = typeof(string),
        Path = "string",
        Trace = Array.Empty<ProviderAttempt>(),
        Seed = 5,
        Message = "diagnosed await failure",
    };

    public static async Task<IEnumerable<object[]>> ThrowingAwaitTaskCompositionRows()
    {
        await Task.Yield();
        throw new CompositionException(AwaitCompositionDiagnostic);
    }

    [ComposeData(nameof(ThrowingAwaitTaskCompositionRows))]
    public void Async_task_await_throws_composition_fails(string label) => _ = label;

    public static async ValueTask<IEnumerable<object[]>> ThrowingAwaitValueTaskCompositionRows()
    {
        await Task.Yield();
        throw new CompositionException(AwaitCompositionDiagnostic);
    }

    [ComposeData(nameof(ThrowingAwaitValueTaskCompositionRows))]
    public void Async_value_task_await_throws_composition_fails(string label) => _ = label;

    public static CompositionDiagnostic ReferenceEnumeratorDiagnostic { get; } = new()
    {
        RootType = typeof(string),
        FailedType = typeof(string),
        Path = "string",
        Trace = Array.Empty<ProviderAttempt>(),
        Seed = 6,
        Message = "diagnosed reference GetAsyncEnumerator failure",
    };

    public static IAsyncEnumerable<object[]> ThrowingReferenceGetEnumeratorRows() =>
        new ThrowingReferenceEnumeratorSource();

    [ComposeData(nameof(ThrowingReferenceGetEnumeratorRows))]
    public void Async_reference_get_enumerator_throws_composition_fails(string label) => _ = label;

    public static CompositionDiagnostic ValueEnumeratorDiagnostic { get; } = new()
    {
        RootType = typeof(string),
        FailedType = typeof(string),
        Path = "string",
        Trace = Array.Empty<ProviderAttempt>(),
        Seed = 7,
        Message = "diagnosed value GetAsyncEnumerator failure",
    };

    public static IAsyncEnumerable<(int Quantity, string Value)> ThrowingValueGetEnumeratorRows() =>
        new ThrowingValueEnumeratorSource();

    [ComposeData(nameof(ThrowingValueGetEnumeratorRows))]
    public void Async_value_get_enumerator_throws_composition_fails(int quantity, string value)
    {
        _ = quantity;
        _ = value;
    }

    public static IAsyncEnumerable<object[]> ReferenceDisposeReplacesRows() =>
        new ReferenceDisposeReplacesSource();

    [ComposeData(nameof(ReferenceDisposeReplacesRows))]
    public void Async_reference_dispose_replaces_failure(string label) => _ = label;

    public static IAsyncEnumerable<(int Quantity, string Value)> ValueDisposeKeepsOriginalRows() =>
        new ValueDisposeKeepsOriginalSource();

    [ComposeData(nameof(ValueDisposeKeepsOriginalRows))]
    public void Async_value_dispose_keeps_original_failure(int quantity, string value)
    {
        _ = quantity;
        _ = value;
    }

    public static IAsyncEnumerable<(int Quantity, string Value)> ValueDisposeAfterSuccessRows() =>
        new ValueDisposeAfterSuccessSource();

    [ComposeData(nameof(ValueDisposeAfterSuccessRows))]
    public void Async_value_dispose_failure_after_success(int quantity, string value)
    {
        _ = quantity;
        _ = value;
    }

    public static MixedAsyncValueAndReferenceRows StaticMixedRowsSource() => new();

    [ComposeData(nameof(StaticMixedRowsSource))]
    public void Static_mixed_source_reads_reference_rows(string label) => _ = label;

    public static IAsyncEnumerable<(int Quantity, string Value)> CovariantReferenceRows() =>
        new MixedAsyncValueAndReferenceRows();

    [ComposeData(nameof(CovariantReferenceRows))]
    public void Async_covariant_rows_read_reference_path(string label) => _ = label;
}

public sealed class SeededWidgetProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.WithSeed(1234);
}

public sealed class NegativeSeedWidgetProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.WithSeed(-5);
}

public sealed class TupleConfigProfile : ICompositionProfile
{
    private readonly (int A, int B) _config;

    public TupleConfigProfile((int A, int B) config) => _config = config;

    public void Configure(CompositionBuilder builder) =>
        builder.Register<OrderService>(() => new OrderService($"{_config.A}:{_config.B}"));
}

public sealed class DynamicConfigProfile : ICompositionProfile
{
    private readonly object _config;

    public DynamicConfigProfile(dynamic config) => _config = (object)config;

    public void Configure(CompositionBuilder builder) =>
        builder.Register<OrderService>(() => new OrderService(_config.ToString()!));
}

public sealed class ListConfigProfile : ICompositionProfile
{
    private readonly System.Collections.Generic.List<object> _config;

    public ListConfigProfile(System.Collections.Generic.List<dynamic> config) => _config = config;

    public void Configure(CompositionBuilder builder)
    {
        var name = string.Empty;
        foreach (var item in _config)
        {
            name = name.Length == 0 ? item?.ToString() ?? string.Empty : name + "+" + item;
        }

        builder.Register<OrderService>(() => new OrderService(name));
    }
}

public sealed class ThrowingCtorProfile : ICompositionProfile
{
    public ThrowingCtorProfile(WidgetConfig config) =>
        throw new InvalidOperationException("profile constructor boom");

    public void Configure(CompositionBuilder builder) { }
}

public sealed class DiagnosedThrowingCtorProfile : ICompositionProfile
{
    public static CompositionDiagnostic Diagnostic { get; } = new()
    {
        RootType = typeof(string),
        FailedType = typeof(int),
        Path = "root",
        Trace = Array.Empty<ProviderAttempt>(),
        Seed = 77,
        Message = "diagnosed constructor failure",
    };

    public DiagnosedThrowingCtorProfile(WidgetConfig config) => throw new CompositionException(Diagnostic);

    public void Configure(CompositionBuilder builder) { }
}

public sealed class CountingWidgetProfile : ICompositionProfile
{
    private static int _instances;

    private readonly int _instance = System.Threading.Interlocked.Increment(ref _instances);

    public static int InstanceCount => System.Threading.Volatile.Read(ref _instances);

    public void Configure(CompositionBuilder builder) =>
        builder.Register<OrderService>(() => new OrderService($"instance-{_instance}"));
}

/// <summary>
/// An async-row source whose declared, statically-visible element type is <c>(int, string)</c> (a value
/// type), so the generator emits the value-element reader - yet the runtime object also implements
/// <c>IAsyncEnumerable&lt;string&gt;</c>. Because <c>IAsyncEnumerable&lt;out T&gt;</c> is covariant, the
/// object is an <c>IAsyncEnumerable&lt;object?&gt;</c> at run time, and the generated reader's runtime
/// check selects the reference path - reading the string rows. This is the JIT runtime-object path
/// selection reproduced statically (ADR-0070 Amendment 4 / PLAN-0071 B5.11).
/// </summary>
public sealed class MixedAsyncIntStringRows : IAsyncEnumerable<(int Quantity, string Value)>, IAsyncEnumerable<string>
{
    public IAsyncEnumerator<(int Quantity, string Value)> GetAsyncEnumerator(System.Threading.CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("the value-element path must not be taken for this runtime object");

    IAsyncEnumerator<string> IAsyncEnumerable<string>.GetAsyncEnumerator(System.Threading.CancellationToken cancellationToken) =>
        new StringEnumerator();

    private sealed class StringEnumerator : IAsyncEnumerator<string>
    {
        private int _index;

        public string Current => _index switch
        {
            1 => "mixed-a",
            2 => "mixed-b",
            _ => string.Empty,
        };

        public ValueTask DisposeAsync() => default;

        public ValueTask<bool> MoveNextAsync() => new(++_index <= 2);
    }
}

// ---------------------------------------------------------------------------------------------
// S4 review: hand-written sources/enumerators used to place a failure at a specific stage
// (GetAsyncEnumerator or DisposeAsync) that compiler-generated iterators cannot express.
// ---------------------------------------------------------------------------------------------

/// <summary>Throws a <see cref="CompositionException"/> from the reference-element GetAsyncEnumerator.</summary>
public sealed class ThrowingReferenceEnumeratorSource : IAsyncEnumerable<object[]>
{
    public IAsyncEnumerator<object[]> GetAsyncEnumerator(System.Threading.CancellationToken cancellationToken = default) =>
        throw new CompositionException(ComposeDataEmissionFailureTargets.ReferenceEnumeratorDiagnostic);
}

/// <summary>Throws a <see cref="CompositionException"/> from the value-element GetAsyncEnumerator.</summary>
public sealed class ThrowingValueEnumeratorSource : IAsyncEnumerable<(int Quantity, string Value)>
{
    public IAsyncEnumerator<(int Quantity, string Value)> GetAsyncEnumerator(System.Threading.CancellationToken cancellationToken = default) =>
        throw new CompositionException(ComposeDataEmissionFailureTargets.ValueEnumeratorDiagnostic);
}

/// <summary>Reference path: a disposal failure that must replace the original enumeration failure.</summary>
public sealed class ReferenceDisposeReplacesSource : IAsyncEnumerable<object[]>
{
    public IAsyncEnumerator<object[]> GetAsyncEnumerator(System.Threading.CancellationToken cancellationToken = default) =>
        new Enumerator();

    private sealed class Enumerator : IAsyncEnumerator<object[]>
    {
        private int _index;

        public object[] Current => new object[] { "reference-dispose-first" };

        public ValueTask DisposeAsync() => throw new InvalidOperationException("reference dispose boom");

        public ValueTask<bool> MoveNextAsync()
        {
            _index++;
            return _index == 1
                ? new ValueTask<bool>(true)
                : throw new InvalidOperationException("original reference enumeration");
        }
    }
}

/// <summary>Value path: the original enumeration failure must win over a disposal failure.</summary>
public sealed class ValueDisposeKeepsOriginalSource : IAsyncEnumerable<(int Quantity, string Value)>
{
    public IAsyncEnumerator<(int Quantity, string Value)> GetAsyncEnumerator(System.Threading.CancellationToken cancellationToken = default) =>
        new Enumerator();

    private sealed class Enumerator : IAsyncEnumerator<(int Quantity, string Value)>
    {
        private int _index;

        public (int Quantity, string Value) Current => (1, "value-dispose-first");

        public ValueTask DisposeAsync() => throw new InvalidOperationException("value dispose boom");

        public ValueTask<bool> MoveNextAsync()
        {
            _index++;
            return _index == 1
                ? new ValueTask<bool>(true)
                : throw new InvalidOperationException("original value enumeration");
        }
    }
}

/// <summary>Value path: a disposal failure after a successful enumeration becomes the reader failure.</summary>
public sealed class ValueDisposeAfterSuccessSource : IAsyncEnumerable<(int Quantity, string Value)>
{
    public IAsyncEnumerator<(int Quantity, string Value)> GetAsyncEnumerator(System.Threading.CancellationToken cancellationToken = default) =>
        new Enumerator();

    private sealed class Enumerator : IAsyncEnumerator<(int Quantity, string Value)>
    {
        private int _index;

        public (int Quantity, string Value) Current => (7, "value-dispose-ok");

        public ValueTask DisposeAsync() => throw new InvalidOperationException("value dispose after success boom");

        public ValueTask<bool> MoveNextAsync() => new(++_index == 1);
    }
}

/// <summary>
/// A source whose declared concrete type implements both the value-element
/// (<c>IAsyncEnumerable&lt;(int, string)&gt;</c>) and the reference-element (<c>IAsyncEnumerable&lt;object[]&gt;</c>)
/// interfaces. The runtime reference-path check wins and reads the object[]-side rows; the value-side
/// GetAsyncEnumerator throws a guard, so a successful read proves the reference path was taken.
/// </summary>
public sealed class MixedAsyncValueAndReferenceRows : IAsyncEnumerable<(int Quantity, string Value)>, IAsyncEnumerable<object[]>
{
    public IAsyncEnumerator<(int Quantity, string Value)> GetAsyncEnumerator(System.Threading.CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("the value-element path must not be taken for this runtime object");

    IAsyncEnumerator<object[]> IAsyncEnumerable<object[]>.GetAsyncEnumerator(System.Threading.CancellationToken cancellationToken) =>
        new ReferenceEnumerator();

    private sealed class ReferenceEnumerator : IAsyncEnumerator<object[]>
    {
        private int _index;

        public object[] Current => _index switch
        {
            1 => new object[] { "dual-a" },
            2 => new object[] { "dual-b" },
            _ => Array.Empty<object>(),
        };

        public ValueTask DisposeAsync() => default;

        public ValueTask<bool> MoveNextAsync() => new(++_index <= 2);
    }
}
