using System.ComponentModel;
using System.Reflection;
using Compono.XunitV3.Aot;
using Xunit;

namespace Compono.XunitV3.Aot.Tests;

public sealed class ComposeDataRowsTests
{
    [Fact]
    public void ComposeDataRows_IsPublicAndHiddenFromIntelliSense()
    {
        var type = typeof(ComposeDataRows);

        Assert.True(type.IsPublic);
        var browsable = type.GetCustomAttribute<EditorBrowsableAttribute>();
        Assert.NotNull(browsable);
        Assert.Equal(EditorBrowsableState.Never, browsable!.State);
    }

    [Fact]
    public void ComposeDataRows_MembersArePublicAndHiddenFromIntelliSense()
    {
        var member = typeof(ComposeDataRows).GetMethod(nameof(ComposeDataRows.MemberNotFound));

        Assert.NotNull(member);
        Assert.True(member!.IsPublic);
        Assert.Equal(EditorBrowsableState.Never, member.GetCustomAttribute<EditorBrowsableAttribute>()!.State);
    }

    [Fact]
    public void ComposeDataRows_HelperMembersArePublicStaticAndHiddenFromIntelliSense()
    {
        var helperNames = new[]
        {
            nameof(ComposeDataRows.MemberReadFailure),
            nameof(ComposeDataRows.NegativeAttributeSeed),
            nameof(ComposeDataRows.NegativeRowSeed),
            nameof(ComposeDataRows.ResolveGenericTestClassType),
        };

        foreach (var name in helperNames)
        {
            var method = typeof(ComposeDataRows).GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            Assert.NotNull(method);
            Assert.True(method!.IsPublic);
            Assert.True(method.IsStatic);
            Assert.Equal(
                EditorBrowsableState.Never,
                method.GetCustomAttribute<EditorBrowsableAttribute>()!.State);
        }
    }

    [Fact]
    public void ComposeDataRows_AllDeclaredPublicMembersAreHiddenFromIntelliSense()
    {
        var publicMembers = typeof(ComposeDataRows).GetMembers(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        Assert.NotEmpty(publicMembers);
        foreach (var publicMember in publicMembers)
        {
            var browsable = publicMember.GetCustomAttribute<EditorBrowsableAttribute>();
            Assert.NotNull(browsable);
            Assert.Equal(EditorBrowsableState.Never, browsable!.State);
        }
    }

    [Fact]
    public void NormalizeRow_IsCallableFromAotConsumer()
    {
        var normalized = ComposeDataRows.NormalizeRow(new object?[] { 1, "a" }, new RowContext(0, null, "M", "T"), null);

        Assert.Equal(new object?[] { 1, "a" }, normalized.Values);
        Assert.Null(normalized.Source);
    }

    [Fact]
    public void CreateDataRow_IsCallableFromAotConsumer()
    {
        var context = new RowContext(0, null, "M", "T");

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, default, 5);

        Assert.Contains("5", dataRow.Traits[ComposeDataRows.SeedTraitName]);
    }

    [Fact]
    public void CreateDataRow_UsesRowSkipGroupWithFuncCondition_WhenRowSkipIsSet()
    {
        var source = new TheoryDataRow(1) { Skip = "row skip", SkipUnless = () => true };
        var context = new RowContext(0, source, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { Skip = "attribute skip", SkipUnless = () => false };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.Equal("row skip", dataRow.Skip);
        Assert.True(dataRow.SkipUnless!());
        Assert.Null(dataRow.SkipWhen);
    }

    [Fact]
    public void CreateDataRow_UsesAttributeSkipGroupWithFuncCondition_WhenRowSkipIsNotSet()
    {
        var source = new TheoryDataRow(1) { SkipUnless = () => true };
        var context = new RowContext(0, source, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { Skip = "attribute skip", SkipUnless = () => false };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.Equal("attribute skip", dataRow.Skip);
        Assert.False(dataRow.SkipUnless!());
    }

    [Fact]
    public void CreateDataRow_UsesRowSkipWithoutCondition_OverAttributeSkipWithCondition()
    {
        var source = new TheoryDataRow(1) { Skip = "row skip" };
        var context = new RowContext(0, source, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { Skip = "attribute skip", SkipUnless = () => false, SkipWhen = () => true };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.Equal("row skip", dataRow.Skip);
        Assert.Null(dataRow.SkipUnless);
        Assert.Null(dataRow.SkipWhen);
    }

    [Fact]
    public void CreateDataRow_LeavesSkipUnset_WhenNoSkipGroupIsSet()
    {
        var context = new RowContext(0, null, "M", "T");

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, default, 1);

        Assert.Null(dataRow.Skip);
        Assert.Null(dataRow.SkipUnless);
        Assert.Null(dataRow.SkipWhen);
    }

    [Fact]
    public void CreateDataRow_PrefersRowDisableParallelization_OverAttribute()
    {
        var source = new TheoryDataRow(1) { DisableParallelization = false };
        var context = new RowContext(0, source, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { DisableParallelization = true };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.False(dataRow.DisableParallelization);
    }

    [Fact]
    public void CreateDataRow_FallsBackToAttributeDisableParallelization()
    {
        var context = new RowContext(0, null, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { DisableParallelization = true };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.True(dataRow.DisableParallelization);
    }

    [Fact]
    public void ResolveGenericTestClassType_FromNonGenericTestClass_ThrowsByteExactMessage()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ComposeDataRows.ResolveGenericTestClassType(typeof(ComposeDataRowsGenericBase<>)));

        Assert.Equal(
            $"ComposeData could not resolve the closed generic test class for '{typeof(ComposeDataRowsGenericBase<>)}' from the executing test class.",
            exception.Message);
    }

    [Fact]
    public void ExposedStructMembersAreHiddenFromIntelliSense()
    {
        var label = typeof(RowContext).GetProperty(nameof(RowContext.Label));
        var values = typeof(RowData).GetProperty(nameof(RowData.Values));

        Assert.Equal(EditorBrowsableState.Never, label!.GetCustomAttribute<EditorBrowsableAttribute>()!.State);
        Assert.Equal(EditorBrowsableState.Never, values!.GetCustomAttribute<EditorBrowsableAttribute>()!.State);
    }

    // PLAN-0071 B6.0 / ADR-0070 Amendment 5: the theory skip-condition normalization. Codes are
    // 0 = None, 1 = Unless, 2 = When; the runtime rewrites the composed row's already-selected skip
    // group into the single field the theory uses, matching the JIT runner's whole-group replacement.

    [Fact]
    public void CreateDataRow_TheoryNone_LeavesConditionalGroupUnchanged()
    {
        var source = new TheoryDataRow(1) { Skip = "row skip", SkipWhen = () => true };
        var context = new RowContext(0, source, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { TheorySkipCondition = 0 };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.Equal("row skip", dataRow.Skip);
        Assert.Null(dataRow.SkipUnless);
        Assert.True(dataRow.SkipWhen!());
    }

    [Fact]
    public void CreateDataRow_TheoryConditionWithoutEffectiveSkip_IsNotConsulted()
    {
        var context = new RowContext(0, null, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { TheorySkipCondition = 1 };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.Null(dataRow.Skip);
        Assert.Null(dataRow.SkipUnless);
        Assert.Null(dataRow.SkipWhen);
    }

    [Fact]
    public void CreateDataRow_TheoryUnless_UnconditionalGroup_BecomesAlwaysFalse()
    {
        var source = new TheoryDataRow(1) { Skip = "row skip" };
        var context = new RowContext(0, source, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { TheorySkipCondition = 1 };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.Equal("row skip", dataRow.Skip);
        Assert.NotNull(dataRow.SkipUnless);
        Assert.False(dataRow.SkipUnless!());
        Assert.Null(dataRow.SkipWhen);
    }

    [Fact]
    public void CreateDataRow_TheoryWhen_UnconditionalGroup_BecomesAlwaysTrue()
    {
        var source = new TheoryDataRow(1) { Skip = "row skip" };
        var context = new RowContext(0, source, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { TheorySkipCondition = 2 };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.Equal("row skip", dataRow.Skip);
        Assert.NotNull(dataRow.SkipWhen);
        Assert.True(dataRow.SkipWhen!());
        Assert.Null(dataRow.SkipUnless);
    }

    [Fact]
    public void CreateDataRow_TheoryUnless_SameKindGroup_PreservesPredicateAndNullsWhen()
    {
        var source = new TheoryDataRow(1) { Skip = "row skip", SkipUnless = () => true, SkipWhen = () => true };
        var context = new RowContext(0, source, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { TheorySkipCondition = 1 };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.True(dataRow.SkipUnless!());
        Assert.Null(dataRow.SkipWhen);
    }

    [Fact]
    public void CreateDataRow_TheoryWhen_SameKindGroup_PreservesPredicateAndNullsUnless()
    {
        var source = new TheoryDataRow(1) { Skip = "row skip", SkipUnless = () => true, SkipWhen = () => true };
        var context = new RowContext(0, source, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { TheorySkipCondition = 2 };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.True(dataRow.SkipWhen!());
        Assert.Null(dataRow.SkipUnless);
    }

    [Fact]
    public void CreateDataRow_TheoryUnless_OppositeConditionalGroup_NegatesWhen()
    {
        var source = new TheoryDataRow(1) { Skip = "row skip", SkipWhen = () => true };
        var context = new RowContext(0, source, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { TheorySkipCondition = 1 };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.Equal("row skip", dataRow.Skip);
        Assert.NotNull(dataRow.SkipUnless);
        Assert.False(dataRow.SkipUnless!());
        Assert.Null(dataRow.SkipWhen);
    }

    [Fact]
    public void CreateDataRow_TheoryUnless_OppositeConditionalGroup_NegatesWhen_FalsePredicate()
    {
        var source = new TheoryDataRow(1) { Skip = "row skip", SkipWhen = () => false };
        var context = new RowContext(0, source, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { TheorySkipCondition = 1 };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.Equal("row skip", dataRow.Skip);
        Assert.NotNull(dataRow.SkipUnless);
        Assert.True(dataRow.SkipUnless!());
        Assert.Null(dataRow.SkipWhen);
    }

    [Fact]
    public void CreateDataRow_TheoryWhen_OppositeConditionalGroup_NegatesUnless()
    {
        var source = new TheoryDataRow(1) { Skip = "row skip", SkipUnless = () => true };
        var context = new RowContext(0, source, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { TheorySkipCondition = 2 };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.Equal("row skip", dataRow.Skip);
        Assert.NotNull(dataRow.SkipWhen);
        Assert.False(dataRow.SkipWhen!());
        Assert.Null(dataRow.SkipUnless);
    }

    [Fact]
    public void CreateDataRow_TheoryWhen_OppositeConditionalGroup_NegatesUnless_FalsePredicate()
    {
        var source = new TheoryDataRow(1) { Skip = "row skip", SkipUnless = () => false };
        var context = new RowContext(0, source, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { TheorySkipCondition = 2 };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.Equal("row skip", dataRow.Skip);
        Assert.NotNull(dataRow.SkipWhen);
        Assert.True(dataRow.SkipWhen!());
        Assert.Null(dataRow.SkipUnless);
    }

    [Fact]
    public void CreateDataRow_TheoryUnless_AttributeGroupCondition_IsPreserved()
    {
        var context = new RowContext(0, null, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { Skip = "attribute skip", SkipWhen = () => true, TheorySkipCondition = 1 };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.Equal("attribute skip", dataRow.Skip);
        Assert.NotNull(dataRow.SkipUnless);
        Assert.False(dataRow.SkipUnless!());
        Assert.Null(dataRow.SkipWhen);
    }

    [Fact]
    public void CreateDataRow_TheoryUnconditional_ReusesCachedDelegateAcrossRows()
    {
        var context = new RowContext(0, null, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { Skip = "attribute skip", TheorySkipCondition = 1 };

        var first = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);
        var second = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 2);

        Assert.Same(first.SkipUnless, second.SkipUnless);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public void CreateDataRow_InvalidTheorySkipCondition_WithEffectiveSkip_ThrowsByteExactMessage(int code)
    {
        var source = new TheoryDataRow(1) { Skip = "row skip" };
        var context = new RowContext(0, source, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { TheorySkipCondition = code };

        var exception = Assert.Throws<InvalidOperationException>(
            () => ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1));

        Assert.Equal(
            $"ComposeData received an unsupported theory skip-condition code {code}; expected 0 (None), 1 (SkipUnless), or 2 (SkipWhen).",
            exception.Message);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public void CreateDataRow_InvalidTheorySkipCondition_WithoutEffectiveSkip_IsIgnored(int code)
    {
        var context = new RowContext(0, null, "M", "T");
        var attribute = new ComposeDataAttributeMetadata { TheorySkipCondition = code };

        var dataRow = ComposeDataRows.CreateDataRow(new object?[] { 1 }, context, attribute, 1);

        Assert.Null(dataRow.Skip);
        Assert.Null(dataRow.SkipUnless);
        Assert.Null(dataRow.SkipWhen);
    }
}

public class ComposeDataRowsGenericBase<T>;

public sealed class ResolveGenericTestClassTypeIntTests : ComposeDataRowsGenericBase<int>
{
    [Fact]
    public void ResolveGenericTestClassType_FromClosedIntSubclass_ReturnsClosedBase()
    {
        var resolved = ComposeDataRows.ResolveGenericTestClassType(typeof(ComposeDataRowsGenericBase<>));

        Assert.Equal(typeof(ComposeDataRowsGenericBase<int>), resolved);
    }
}

public sealed class ResolveGenericTestClassTypeStringTests : ComposeDataRowsGenericBase<string>
{
    [Fact]
    public void ResolveGenericTestClassType_FromClosedStringSubclass_ReturnsClosedBase()
    {
        var resolved = ComposeDataRows.ResolveGenericTestClassType(typeof(ComposeDataRowsGenericBase<>));

        Assert.Equal(typeof(ComposeDataRowsGenericBase<string>), resolved);
    }
}

public class Middle<T> : ComposeDataRowsGenericBase<T>;

public sealed class ResolveGenericTestClassTypeLeafTests : Middle<int>
{
    [Fact]
    public void ResolveGenericTestClassType_FromMultiLevelSubclass_ReturnsRootClosedBase()
    {
        var resolved = ComposeDataRows.ResolveGenericTestClassType(typeof(ComposeDataRowsGenericBase<>));

        Assert.Equal(typeof(ComposeDataRowsGenericBase<int>), resolved);
    }
}
