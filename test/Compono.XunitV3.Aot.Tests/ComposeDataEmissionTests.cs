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
}
