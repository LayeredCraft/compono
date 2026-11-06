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
    public void ExposedStructMembersAreHiddenFromIntelliSense()
    {
        var label = typeof(RowContext).GetProperty(nameof(RowContext.Label));
        var values = typeof(RowData).GetProperty(nameof(RowData.Values));

        Assert.Equal(EditorBrowsableState.Never, label!.GetCustomAttribute<EditorBrowsableAttribute>()!.State);
        Assert.Equal(EditorBrowsableState.Never, values!.GetCustomAttribute<EditorBrowsableAttribute>()!.State);
    }
}
