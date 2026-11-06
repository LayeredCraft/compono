using System.Runtime.CompilerServices;

namespace Compono.XunitV3.Tests;

public sealed class ComposeDataRowsTests
{
    private const string Member = "Sample.Namespace.SampleClass.Cases";
    private const string Method = "Sample.Namespace.PricingTests.Prices_by_date";

    // ---- collection classification ----

    [Fact]
    public void IsSupportedCollectionResult_IsTrue_ForTheoryDataCollection()
    {
        var collection = new TheoryData<int, string> { { 1, "a" }, { 2, "b" } };

        ComposeDataRows.IsSupportedCollectionResult(collection).Should().BeTrue();
    }

    [Fact]
    public void IsSupportedCollectionResult_IsTrue_ForTheoryDataRowArray()
    {
        var collection = new TheoryDataRow<int, string>[] { new(1, "a") };

        ComposeDataRows.IsSupportedCollectionResult(collection).Should().BeTrue();
    }

    [Fact]
    public void IsSupportedCollectionResult_IsTrue_ForListOfObjectArrays()
    {
        var collection = new List<object?[]> { new object?[] { 1, "a" } };

        ComposeDataRows.IsSupportedCollectionResult(collection).Should().BeTrue();
    }

    [Fact]
    public void IsSupportedCollectionResult_IsTrue_ForArrayOfObjectArrays()
    {
        var collection = new object[] { new object?[] { 1, "a" }, new object?[] { 2, "b" } };

        ComposeDataRows.IsSupportedCollectionResult(collection).Should().BeTrue();
    }

    [Fact]
    public void IsSupportedCollectionResult_IsTrue_ForTupleArray()
    {
        var collection = new (int, string)[] { (1, "a"), (2, "b") };

        ComposeDataRows.IsSupportedCollectionResult(collection).Should().BeTrue();
    }

    [Fact]
    public void IsSupportedCollectionResult_IsFalse_ForString()
    {
        ComposeDataRows.IsSupportedCollectionResult("not a collection").Should().BeFalse();
    }

    [Fact]
    public void IsSupportedCollectionResult_IsFalse_ForSingleTheoryDataRow()
    {
        ComposeDataRows.IsSupportedCollectionResult(new TheoryDataRow(1, "a")).Should().BeFalse();
    }

    [Fact]
    public void IsSupportedCollectionResult_IsFalse_ForNonEnumerable()
    {
        ComposeDataRows.IsSupportedCollectionResult(42).Should().BeFalse();
    }

    [Fact]
    public void IsSupportedCollectionResult_IsFalse_ForNull()
    {
        ComposeDataRows.IsSupportedCollectionResult(null).Should().BeFalse();
    }

    // ---- row normalization ----

    [Fact]
    public void NormalizeRow_UsesGetDataAndKeepsSource_ForTheoryDataRow()
    {
        var row = new TheoryDataRow<int, string>(1, "a");

        var normalized = ComposeDataRows.NormalizeRow(row, new RowContext(0, null, Member, Method), null);

        normalized.Values.Should().Equal(new object?[] { 1, "a" });
        normalized.Source.Should().BeSameAs(row);
    }

    [Fact]
    public void NormalizeRow_UsesArrayAsIs_WithNoSource()
    {
        object?[] row = [1, "a"];

        var normalized = ComposeDataRows.NormalizeRow(row, new RowContext(0, null, Member, Method), null);

        normalized.Values.Should().BeSameAs(row);
        normalized.Source.Should().BeNull();
    }

    [Fact]
    public void NormalizeRow_FlattensTuple_WithNoSource()
    {
        var normalized = ComposeDataRows.NormalizeRow((1, "a"), new RowContext(0, null, Member, Method), null);

        normalized.Values.Should().Equal(new object?[] { 1, "a" });
        normalized.Source.Should().BeNull();
    }

    [Fact]
    public void NormalizeRow_FlattensNineElementValueTuple()
    {
        var normalized = ComposeDataRows.NormalizeRow((1, 2, 3, 4, 5, 6, 7, 8, 9), new RowContext(0, null, Member, Method), null);

        normalized.Values.Should().HaveCount(9);
        normalized.Values.Should().Equal(new object?[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
    }

    [Fact]
    public void NormalizeRow_KeepsNestedArrayAsOneValue()
    {
        object?[] row = [new object?[] { 1, 2 }, 3];

        var normalized = ComposeDataRows.NormalizeRow(row, new RowContext(0, null, Member, Method), null);

        normalized.Values.Should().HaveCount(2);
        normalized.Values[0].Should().BeEquivalentTo(new object?[] { 1, 2 });
        normalized.Values[1].Should().Be(3);
    }

    [Fact]
    public void NormalizeRow_ClassifiesAsTheoryDataRow_WhenRowIsBothRowAndTuple()
    {
        var row = new RowAndTuple();

        var normalized = ComposeDataRows.NormalizeRow(row, new RowContext(0, null, Member, Method), null);

        normalized.Source.Should().BeSameAs(row);
        normalized.Values.Should().Equal(new object?[] { 1, 2 });
    }

    [Fact]
    public void NormalizeRow_ThrowsRowFailure_WhenRowIsNull()
    {
        var exception = Assert.Throws<CompositionException>(
            () => ComposeDataRows.NormalizeRow(null, new RowContext(2, null, Member, Method), 42));

        exception.Message.Should().Be(
            $"ComposeData row 2 from '{Member}' for '{Method}' failed: row is null\n\nSeed: 42");
    }

    [Fact]
    public void NormalizeRow_ThrowsRowFailure_WhenRowShapeIsUnsupported()
    {
        var exception = Assert.Throws<CompositionException>(
            () => ComposeDataRows.NormalizeRow(42, new RowContext(0, null, Member, Method), null));

        exception.Message.Should().Be(
            $"ComposeData row 0 from '{Member}' for '{Method}' failed: row has unsupported type 'System.Int32'; expected ITheoryDataRow, object?[], or ITuple" +
            "\n\nSeed: not assigned (the failure occurred before the row's composition started)");
    }

    // ---- TConfig extraction ----

    [Fact]
    public void ExtractConfig_ReturnsConfigAndRemainingValues()
    {
        object?[] values = ["config", 10.0m, "leaf"];
        var context = new RowContext(0, null, Member, Method);

        var (config, remaining) = ComposeDataRows.ExtractConfig<string>(values, context, null);

        config.Should().Be("config");
        remaining.Should().Equal(new object?[] { 10.0m, "leaf" });
    }

    [Fact]
    public void ExtractConfig_ReturnsStructConfig()
    {
        var expected = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        object?[] values = [expected, 10.0m];
        var context = new RowContext(0, null, Member, Method);

        var (config, remaining) = ComposeDataRows.ExtractConfig<DateTimeOffset>(values, context, null);

        config.Should().Be(expected);
        remaining.Should().Equal(new object?[] { 10.0m });
    }

    [Fact]
    public void ExtractConfig_ThrowsRowFailure_WhenConfigIsMissing()
    {
        object?[] values = [];
        var context = new RowContext(1, null, Member, Method);

        var exception = Assert.Throws<CompositionException>(
            () => ComposeDataRows.ExtractConfig<string>(values, context, 5));

        exception.Message.Should().Be(
            $"ComposeData row 1 from '{Member}' for '{Method}' failed: the first row element must be a non-null 'System.String' profile configuration value\n\nSeed: 5");
    }

    [Fact]
    public void ExtractConfig_ThrowsRowFailure_WhenConfigIsNull()
    {
        object?[] values = [null, 10.0m];
        var context = new RowContext(0, null, Member, Method);

        var exception = Assert.Throws<CompositionException>(
            () => ComposeDataRows.ExtractConfig<string>(values, context, null));

        exception.Message.Should().Be(
            $"ComposeData row 0 from '{Member}' for '{Method}' failed: the first row element must be a non-null 'System.String' profile configuration value" +
            "\n\nSeed: not assigned (the failure occurred before the row's composition started)");
    }

    [Fact]
    public void ExtractConfig_ThrowsRowFailure_WhenConfigIsWrongType()
    {
        object?[] values = [42, 10.0m];
        var context = new RowContext(0, null, Member, Method);

        var exception = Assert.Throws<CompositionException>(
            () => ComposeDataRows.ExtractConfig<string>(values, context, null));

        exception.Message.Should().Be(
            $"ComposeData row 0 from '{Member}' for '{Method}' failed: the first row element must be a non-null 'System.String' profile configuration value" +
            "\n\nSeed: not assigned (the failure occurred before the row's composition started)");
    }

    // ---- positional value binding ----

    [Fact]
    public void BindValue_ReturnsNullableValue_WhenValueIsBoxedUnderlying()
    {
        object?[] values = [5];
        var context = new RowContext(0, null, Member, Method);

        var bound = ComposeDataRows.BindValue<int?>(values, 0, parameterIsNullable: true, "count", context, 7);

        bound.Should().Be(5);
    }

    [Fact]
    public void BindValue_ReturnsNull_WhenValueIsNullAndParameterIsNullable()
    {
        object?[] values = [null];
        var context = new RowContext(0, null, Member, Method);

        var bound = ComposeDataRows.BindValue<int?>(values, 0, parameterIsNullable: true, "count", context, 7);

        bound.Should().BeNull();
    }

    [Fact]
    public void BindValue_ThrowsRowFailure_WhenNullForNonNullableParameter()
    {
        object?[] values = [null];
        var context = new RowContext(0, null, Member, Method);

        var exception = Assert.Throws<CompositionException>(
            () => ComposeDataRows.BindValue<int>(values, 0, parameterIsNullable: false, "count", context, 7));

        exception.Message.Should().Be(
            $"ComposeData row 0 from '{Member}' for '{Method}' failed: Row value for parameter 'count' on '{Method}' is null, but the parameter is not nullable.\n\nSeed: 7");
    }

    [Fact]
    public void BindValue_ThrowsRowFailure_WhenValueTypeDoesNotWiden()
    {
        object?[] values = [5];
        var context = new RowContext(0, null, Member, Method);

        var exception = Assert.Throws<CompositionException>(
            () => ComposeDataRows.BindValue<long>(values, 0, parameterIsNullable: false, "count", context, 7));

        exception.Message.Should().Be(
            $"ComposeData row 0 from '{Member}' for '{Method}' failed: Row value for parameter 'count' on '{Method}' has type 'System.Int32', which is not assignable to 'System.Int64'.\n\nSeed: 7");
    }

    [Fact]
    public void BindValue_ThrowsRowFailure_WhenNullForNonNullableReferenceParameter()
    {
        // The flag is supplied directly here; genuine compiler obliviousness
        // (NullableAnnotation.None) coverage lives in B3/B4 where the binding plan derives the flag
        // from the method signature.
        object?[] values = [null];
        var context = new RowContext(0, null, Member, Method);

        var exception = Assert.Throws<CompositionException>(
            () => ComposeDataRows.BindValue<string>(values, 0, parameterIsNullable: false, "email", context, 7));

        exception.Message.Should().Be(
            $"ComposeData row 0 from '{Member}' for '{Method}' failed: Row value for parameter 'email' on '{Method}' is null, but the parameter is not nullable.\n\nSeed: 7");
    }

    [Fact]
    public void BindValue_ThrowsRowFailure_WhenNullForNonNullableValueType_EvenIfFlagSaysNullable()
    {
        object?[] values = [null];
        var context = new RowContext(0, null, Member, Method);

        var exception = Assert.Throws<CompositionException>(
            () => ComposeDataRows.BindValue<int>(values, 0, parameterIsNullable: true, "count", context, 7));

        exception.Message.Should().Be(
            $"ComposeData row 0 from '{Member}' for '{Method}' failed: Row value for parameter 'count' on '{Method}' is null, but the parameter is not nullable.\n\nSeed: 7");
    }

    [Fact]
    public void BindValue_ReturnsNull_WhenValueIsNullAndReferenceParameterIsNullable()
    {
        object?[] values = [null];
        var context = new RowContext(0, null, Member, Method);

        var bound = ComposeDataRows.BindValue<string>(values, 0, parameterIsNullable: true, "email", context, 7);

        bound.Should().BeNull();
    }

    [Fact]
    public void BindValue_ReturnsDateTimeOffsetValue()
    {
        var expected = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        object?[] values = [expected];
        var context = new RowContext(0, null, Member, Method);

        var bound = ComposeDataRows.BindValue<DateTimeOffset>(values, 0, parameterIsNullable: false, "now", context, 7);

        bound.Should().Be(expected);
    }

    [Fact]
    public void BindValue_ReturnsSubclassInstance_ForBaseTypeParameter()
    {
        object?[] values = [new DerivedService()];
        var context = new RowContext(0, null, Member, Method);

        var bound = ComposeDataRows.BindValue<BaseService>(values, 0, parameterIsNullable: false, "service", context, 7);

        bound.Should().BeOfType<DerivedService>();
    }

    // ---- metadata merge ----

    [Fact]
    public void CreateDataRow_WritesSeedTrait_Always()
    {
        var context = new RowContext(0, null, Member, Method);

        var dataRow = ComposeDataRows.CreateDataRow([1], context, default, 99);

        dataRow.Traits[ComposeDataRows.SeedTraitName].Should().ContainSingle().Which.Should().Be("99");
    }

    [Fact]
    public void CreateDataRow_UsesRowSkipGroup_WhenRowSkipIsSet()
    {
        var source = new TheoryDataRow(1)
        {
            Skip = "row-skip",
            SkipUnless = "row-unless",
            SkipWhen = "row-when",
            SkipType = typeof(ComposeDataRowsTests),
        };
        var context = new RowContext(0, source, Member, Method);
        var attribute = new ComposeDataAttributeMetadata
        {
            Skip = "attr-skip",
            SkipUnless = "attr-unless",
            SkipWhen = "attr-when",
            SkipType = typeof(CompositionException),
        };

        var dataRow = ComposeDataRows.CreateDataRow([1], context, attribute, 0);

        dataRow.Skip.Should().Be("row-skip");
        dataRow.SkipUnless.Should().Be("row-unless");
        dataRow.SkipWhen.Should().Be("row-when");
        dataRow.SkipType.Should().Be(typeof(ComposeDataRowsTests));
    }

    [Fact]
    public void CreateDataRow_UsesAttributeSkipGroup_WhenRowSkipIsNotSet()
    {
        var source = new TheoryDataRow(1) { Label = "l" };
        var context = new RowContext(0, source, Member, Method);
        var attribute = new ComposeDataAttributeMetadata
        {
            Skip = "attr-skip",
            SkipUnless = "attr-unless",
            SkipWhen = "attr-when",
            SkipType = typeof(CompositionException),
        };

        var dataRow = ComposeDataRows.CreateDataRow([1], context, attribute, 0);

        dataRow.Skip.Should().Be("attr-skip");
        dataRow.SkipUnless.Should().Be("attr-unless");
        dataRow.SkipWhen.Should().Be("attr-when");
        dataRow.SkipType.Should().Be(typeof(CompositionException));
    }

    [Fact]
    public void CreateDataRow_LeavesSkipUnset_WhenNeitherRowNorAttributeSetSkip()
    {
        var source = new TheoryDataRow(1) { Label = "l" };
        var context = new RowContext(0, source, Member, Method);
        var attribute = new ComposeDataAttributeMetadata { SkipUnless = "attr-unless" };

        var dataRow = ComposeDataRows.CreateDataRow([1], context, attribute, 0);

        dataRow.Skip.Should().BeNull();
        dataRow.SkipUnless.Should().BeNull();
        dataRow.SkipWhen.Should().BeNull();
        dataRow.SkipType.Should().BeNull();
    }

    [Fact]
    public void CreateDataRow_PrefersRowMetadata_ForRowOrAttributeFields()
    {
        var source = new TheoryDataRow(1)
        {
            Label = "row-label",
            TestDisplayName = "row-display",
            Timeout = 11,
            Explicit = true,
        };
        var context = new RowContext(0, source, Member, Method);
        var attribute = new ComposeDataAttributeMetadata
        {
            Label = "attr-label",
            TestDisplayName = "attr-display",
            Timeout = 22,
            Explicit = false,
        };

        var dataRow = ComposeDataRows.CreateDataRow([1], context, attribute, 0);

        dataRow.Label.Should().Be("row-label");
        dataRow.TestDisplayName.Should().Be("row-display");
        dataRow.Timeout.Should().Be(11);
        dataRow.Explicit.Should().BeTrue();
    }

    [Fact]
    public void CreateDataRow_FallsBackToAttributeMetadata_WhenRowFieldsAreNull()
    {
        var source = new TheoryDataRow(1);
        var context = new RowContext(0, source, Member, Method);
        var attribute = new ComposeDataAttributeMetadata
        {
            Label = "attr-label",
            TestDisplayName = "attr-display",
            Timeout = 22,
            Explicit = false,
        };

        var dataRow = ComposeDataRows.CreateDataRow([1], context, attribute, 0);

        dataRow.Label.Should().Be("attr-label");
        dataRow.TestDisplayName.Should().Be("attr-display");
        dataRow.Timeout.Should().Be(22);
        dataRow.Explicit.Should().BeFalse();
    }

    [Fact]
    public void CreateDataRow_UnionsRowAndAttributeTraits()
    {
        var source = new TheoryDataRow(1)
        {
            Traits = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["row"] = new(StringComparer.Ordinal) { "r1" },
            },
        };
        var context = new RowContext(0, source, Member, Method);
        var attribute = new ComposeDataAttributeMetadata { Traits = ["attr", "a1", "other", "o1", "dangling"] };

        var dataRow = ComposeDataRows.CreateDataRow([1], context, attribute, 5);

        dataRow.Traits["row"].Should().ContainSingle().Which.Should().Be("r1");
        dataRow.Traits["attr"].Should().ContainSingle().Which.Should().Be("a1");
        dataRow.Traits["other"].Should().ContainSingle().Which.Should().Be("o1");
        dataRow.Traits.Should().NotContainKey("dangling");
        dataRow.Traits[ComposeDataRows.SeedTraitName].Should().ContainSingle().Which.Should().Be("5");
    }

    [Fact]
    public void CreateDataRow_UsesRowSkipGroupWithoutConditions_OverAttributeGroupWithConditions()
    {
        var source = new TheoryDataRow(1) { Skip = "row-skip" };
        var context = new RowContext(0, source, Member, Method);
        var attribute = new ComposeDataAttributeMetadata
        {
            Skip = "attr-skip",
            SkipUnless = "attr-unless",
            SkipWhen = "attr-when",
            SkipType = typeof(CompositionException),
        };

        var dataRow = ComposeDataRows.CreateDataRow([1], context, attribute, 0);

        dataRow.Skip.Should().Be("row-skip");
        dataRow.SkipUnless.Should().BeNull();
        dataRow.SkipWhen.Should().BeNull();
        dataRow.SkipType.Should().BeNull();
    }

    [Fact]
    public void CreateDataRow_DiscardsRowCondition_WhenRowSkipIsNotSet()
    {
        var source = new TheoryDataRow(1) { SkipUnless = "row-unless" };
        var context = new RowContext(0, source, Member, Method);
        var attribute = new ComposeDataAttributeMetadata { Skip = "attr-skip", SkipUnless = "attr-unless" };

        var dataRow = ComposeDataRows.CreateDataRow([1], context, attribute, 0);

        dataRow.Skip.Should().Be("attr-skip");
        dataRow.SkipUnless.Should().Be("attr-unless");
    }

    [Fact]
    public void CreateDataRow_UsesAttributeSkipWithoutConditions_WhenRowHasNoSkip()
    {
        var context = new RowContext(0, null, Member, Method);
        var attribute = new ComposeDataAttributeMetadata { Skip = "attr-skip" };

        var dataRow = ComposeDataRows.CreateDataRow([1], context, attribute, 0);

        dataRow.Skip.Should().Be("attr-skip");
        dataRow.SkipUnless.Should().BeNull();
        dataRow.SkipWhen.Should().BeNull();
        dataRow.SkipType.Should().BeNull();
    }

    [Fact]
    public void CreateDataRow_ReadsDisableParallelization_FromCustomTheoryDataRow()
    {
        var source = new RowAndTuple { DisableParallelization = true };
        var context = new RowContext(0, source, Member, Method);

        var dataRow = ComposeDataRows.CreateDataRow([1], context, default, 0);

        dataRow.DisableParallelization.Should().BeTrue();
    }

    [Fact]
    public void CreateDataRow_ReadsDisableParallelization_FromGenericTheoryDataRow()
    {
        var source = new TheoryDataRow<int, string>(1, "a") { DisableParallelization = true };
        var context = new RowContext(0, source, Member, Method);

        var dataRow = ComposeDataRows.CreateDataRow([1], context, default, 0);

        dataRow.DisableParallelization.Should().BeTrue();
    }

    [Fact]
    public void CreateDataRow_PrefersRowDisableParallelization_OverAttribute()
    {
        var source = new TheoryDataRow(1) { DisableParallelization = false };
        var context = new RowContext(0, source, Member, Method);
        var attribute = new ComposeDataAttributeMetadata { DisableParallelization = true };

        var dataRow = ComposeDataRows.CreateDataRow([1], context, attribute, 0);

        dataRow.DisableParallelization.Should().BeFalse();
    }

    [Fact]
    public void CreateDataRow_FallsBackToAttributeDisableParallelization()
    {
        var context = new RowContext(0, null, Member, Method);
        var attribute = new ComposeDataAttributeMetadata { DisableParallelization = true };

        var dataRow = ComposeDataRows.CreateDataRow([1], context, attribute, 0);

        dataRow.DisableParallelization.Should().BeTrue();
    }

    [Fact]
    public void CreateDataRow_ReplacesSourceSeedTrait_WithAuthoritativeSeed()
    {
        var source = new TheoryDataRow(1)
        {
            Traits = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                [ComposeDataRows.SeedTraitName] = new(StringComparer.Ordinal) { "999" },
                ["row"] = new(StringComparer.Ordinal) { "r1" },
            },
        };
        var context = new RowContext(0, source, Member, Method);

        var dataRow = ComposeDataRows.CreateDataRow([1], context, default, 5);

        dataRow.Traits[ComposeDataRows.SeedTraitName].Should().ContainSingle().Which.Should().Be("5");
        dataRow.Traits["row"].Should().ContainSingle().Which.Should().Be("r1");
    }

    [Fact]
    public void CreateDataRow_ReplacesAttributeSeedTrait_WithAuthoritativeSeed()
    {
        var context = new RowContext(0, null, Member, Method);
        var attribute = new ComposeDataAttributeMetadata { Traits = [ComposeDataRows.SeedTraitName, "7"] };

        var dataRow = ComposeDataRows.CreateDataRow([1], context, attribute, 5);

        dataRow.Traits[ComposeDataRows.SeedTraitName].Should().ContainSingle().Which.Should().Be("5");
    }

    // ---- member-level failures (no seed line) ----

    [Fact]
    public void MemberNotFound_BuildsD1Message()
    {
        ComposeDataRows.MemberNotFound(Member, Method).Message.Should().Be(
            $"ComposeData member '{Member}' for '{Method}' was not found. Expected a public static field, property, or parameterless method.");
    }

    [Fact]
    public void MemberNotPublicStatic_BuildsD2Message()
    {
        ComposeDataRows.MemberNotPublicStatic(Member, Method).Message.Should().Be(
            $"ComposeData member '{Member}' for '{Method}' must be public and static.");
    }

    [Fact]
    public void MemberRequiresArguments_BuildsD3Message()
    {
        ComposeDataRows.MemberRequiresArguments(Member, Method).Message.Should().Be(
            $"ComposeData member '{Member}' for '{Method}' requires arguments, which ComposeData does not support.");
    }

    [Fact]
    public void UnsupportedReturnType_BuildsD4Message()
    {
        ComposeDataRows.UnsupportedReturnType(Member, Method, typeof(int)).Message.Should().Be(
            $"ComposeData member '{Member}' for '{Method}' returned unsupported type 'System.Int32'. Expected IEnumerable, Task/ValueTask of IEnumerable, or IAsyncEnumerable of ITheoryDataRow, object?[], or ITuple rows.");
    }

    [Fact]
    public void NullResult_BuildsD5Message()
    {
        ComposeDataRows.NullResult(Member, Method).Message.Should().Be(
            $"ComposeData member '{Member}' for '{Method}' returned null.");
    }

    // ---- seed lines and R0 shape ----

    [Fact]
    public void PreRowSeedLine_IsNotAssigned_WhenAttributeSeedIsNull()
    {
        ComposeDataRows.PreRowSeedLine(null).Should().Be(
            "\n\nSeed: not assigned (the failure occurred before the row's composition started)");
    }

    [Fact]
    public void PreRowSeedLine_IsValue_WhenAttributeSeedIsSet()
    {
        ComposeDataRows.PreRowSeedLine(42).Should().Be("\n\nSeed: 42");
    }

    [Fact]
    public void RowSeedLine_IsRowSeed()
    {
        ComposeDataRows.RowSeedLine(7).Should().Be("\n\nSeed: 7");
    }

    [Fact]
    public void RowFailure_IncludesLabel_WhenSourceLabelIsSet()
    {
        var context = new RowContext(3, new TheoryDataRow(1) { Label = "valid" }, Member, Method);
        var exception = ComposeDataRows.RowFailure(context, "row is null", ComposeDataRows.RowSeedLine(8));

        exception.Message.Should().Be(
            $"ComposeData row 3 [valid] from '{Member}' for '{Method}' failed: row is null\n\nSeed: 8");
    }

    [Fact]
    public void RowFailure_PreservesInnerCompositionExceptionAndDiagnostic()
    {
        var diagnostic = new CompositionDiagnostic
        {
            RootType = typeof(ComposeDataRowsTests),
            FailedType = typeof(ComposeDataRowsTests),
            Path = "ComposeDataRowsTests",
            Trace = [],
            Seed = 1UL,
            Message = "pipeline failure",
        };
        var cause = new CompositionException(diagnostic);

        var context = new RowContext(0, null, Member, Method);
        var exception = ComposeDataRows.RowFailure(context, "pipeline failure", "\n\nSeed: 8", cause);

        exception.InnerException.Should().BeSameAs(cause);
        exception.Diagnostic.Should().BeSameAs(diagnostic);
    }

    [Fact]
    public void RowFailure_PreservesPlainInnerException_WithNoDiagnostic()
    {
        var cause = new InvalidOperationException("configure blew up");

        var context = new RowContext(0, null, Member, Method);
        var exception = ComposeDataRows.RowFailure(context, "configure blew up", "\n\nSeed: 8", cause);

        exception.InnerException.Should().BeSameAs(cause);
        exception.Diagnostic.Should().BeNull();
    }

    [Fact]
    public void RowFailure_HasNoInner_WhenNoCauseSupplied()
    {
        var context = new RowContext(0, null, Member, Method);
        var exception = ComposeDataRows.RowFailure(context, "row is null", "\n\nSeed: not assigned (the failure occurred before the row's composition started)");

        exception.InnerException.Should().BeNull();
        exception.Diagnostic.Should().BeNull();
    }

    [Fact]
    public void TooManyValues_BuildsByteExactRowFailure_FromRowContextAndSeed()
    {
        var source = new TheoryDataRow(1) { Label = "case" };
        var context = new RowContext(2, source, Member, Method);

        var exception = ComposeDataRows.TooManyValues(context, supplied: 5, parameterCount: 3, rowSeed: 8);

        exception.Message.Should().Be(
            $"ComposeData row 2 [case] from '{Member}' for '{Method}' failed: too many values: 5 supplied, but the method has 3 parameter(s)\n\nSeed: 8");
    }

    private class BaseService;

    private sealed class DerivedService : BaseService;

    // Implements both ITheoryDataRow and ITuple to prove row classification prefers ITheoryDataRow.
    private sealed class RowAndTuple : ITheoryDataRow, ITuple
    {
        public bool? Explicit { get; set; }

        public string? Label { get; set; }

        public string? Skip { get; set; }

        public Type? SkipType { get; set; }

        public string? SkipUnless { get; set; }

        public string? SkipWhen { get; set; }

        public string? TestDisplayName { get; set; }

        public int? Timeout { get; set; }

        public Dictionary<string, HashSet<string>>? Traits { get; set; }

        public bool? DisableParallelization { get; set; }

        public int Length => 2;

        public object? this[int index] => index == 0 ? 1 : 2;

        public object?[] GetData() => [1, 2];
    }
}
