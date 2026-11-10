namespace Compono.XunitV3.Tests;

// ADR-0070 Amendment 5 / PLAN-0071 B6.0: the full theory-level skip interaction matrix, run through
// the real xUnit v3 runner. Every case is a [Theory][ComposeData] method whose body fails if it runs
// when a skip is expected. This is the JIT reference the AOT mirror (ComposeDataTheorySkipParityTests
// in Compono.XunitV3.Aot.Tests) must match in outcome and skip reason.
//
// Theory variants x ComposeData variants = 6 x 14 = 84 cases. The theory-level skip condition names
// resolve on this class (SkipUnless/SkipWhen without SkipType); row-level conditions resolve through
// ComposeDataMatrixConditions via SkipType.

public static class ComposeDataMatrixConditions
{
    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;
}

public class ComposeDataMatrixRows
{
    public static object?[][] NoSkip => [[1]];

    public static object?[] Unconditional() => [new TheoryDataRow(1) { Skip = "row" }];

    public static object?[] UnlessTrue() =>
        [new TheoryDataRow(1) { Skip = "row", SkipUnless = nameof(ComposeDataMatrixConditions.AlwaysTrue), SkipType = typeof(ComposeDataMatrixConditions) }];

    public static object?[] UnlessFalse() =>
        [new TheoryDataRow(1) { Skip = "row", SkipUnless = nameof(ComposeDataMatrixConditions.AlwaysFalse), SkipType = typeof(ComposeDataMatrixConditions) }];

    public static object?[] WhenTrue() =>
        [new TheoryDataRow(1) { Skip = "row", SkipWhen = nameof(ComposeDataMatrixConditions.AlwaysTrue), SkipType = typeof(ComposeDataMatrixConditions) }];

    public static object?[] WhenFalse() =>
        [new TheoryDataRow(1) { Skip = "row", SkipWhen = nameof(ComposeDataMatrixConditions.AlwaysFalse), SkipType = typeof(ComposeDataMatrixConditions) }];
}

public sealed class ComposeDataTheorySkipMatrix_NoTheoryCondition : ComposeDataMatrixRows
{
    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;

    [Theory] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip))]
    public void D00_NoSkip_Runs(int value) => value.Should().Be(1);

    [Theory] [ComposeData(nameof(ComposeDataMatrixRows.Unconditional))]
    public void D01_RowUnconditional_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(ComposeDataMatrixRows.UnlessTrue))]
    public void D02_RowUnlessTrue_Runs(int value) => value.Should().Be(1);

    [Theory] [ComposeData(nameof(ComposeDataMatrixRows.UnlessFalse))]
    public void D03_RowUnlessFalse_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(ComposeDataMatrixRows.WhenTrue))]
    public void D04_RowWhenTrue_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(ComposeDataMatrixRows.WhenFalse))]
    public void D05_RowWhenFalse_Runs(int value) => value.Should().Be(1);

    [Theory] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr")]
    public void D06_AttributeUnconditional_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D07_AttributeUnlessTrue_Runs(int value) => value.Should().Be(1);

    [Theory] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D08_AttributeUnlessFalse_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysTrue))]
    public void D09_AttributeWhenTrue_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysFalse))]
    public void D10_AttributeWhenFalse_Runs(int value) => value.Should().Be(1);

    [Theory] [ComposeData(nameof(ComposeDataMatrixRows.Unconditional), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D11_UnconditionalRowOverConditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(ComposeDataMatrixRows.UnlessFalse), Skip = "attr")]
    public void D12_ConditionalRowOverUnconditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(ComposeDataMatrixRows.WhenTrue), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D13_OppositeKindRowOverAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");
}

public sealed class ComposeDataTheorySkipMatrix_TheorySkipUnlessTrue : ComposeDataMatrixRows
{
    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip))]
    public void D00_NoSkip_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.Unconditional))]
    public void D01_RowUnconditional_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.UnlessTrue))]
    public void D02_RowUnlessTrue_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.UnlessFalse))]
    public void D03_RowUnlessFalse_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.WhenTrue))]
    public void D04_RowWhenTrue_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.WhenFalse))]
    public void D05_RowWhenFalse_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr")]
    public void D06_AttributeUnconditional_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D07_AttributeUnlessTrue_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D08_AttributeUnlessFalse_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysTrue))]
    public void D09_AttributeWhenTrue_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysFalse))]
    public void D10_AttributeWhenFalse_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.Unconditional), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D11_UnconditionalRowOverConditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.UnlessFalse), Skip = "attr")]
    public void D12_ConditionalRowOverUnconditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.WhenTrue), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D13_OppositeKindRowOverAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");
}

public sealed class ComposeDataTheorySkipMatrix_TheorySkipUnlessFalse : ComposeDataMatrixRows
{
    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip))]
    public void D00_NoSkip_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.Unconditional))]
    public void D01_RowUnconditional_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.UnlessTrue))]
    public void D02_RowUnlessTrue_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.UnlessFalse))]
    public void D03_RowUnlessFalse_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.WhenTrue))]
    public void D04_RowWhenTrue_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.WhenFalse))]
    public void D05_RowWhenFalse_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr")]
    public void D06_AttributeUnconditional_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D07_AttributeUnlessTrue_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D08_AttributeUnlessFalse_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysTrue))]
    public void D09_AttributeWhenTrue_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysFalse))]
    public void D10_AttributeWhenFalse_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.Unconditional), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D11_UnconditionalRowOverConditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.UnlessFalse), Skip = "attr")]
    public void D12_ConditionalRowOverUnconditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.WhenTrue), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D13_OppositeKindRowOverAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");
}

public sealed class ComposeDataTheorySkipMatrix_TheorySkipWhenTrue : ComposeDataMatrixRows
{
    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip))]
    public void D00_NoSkip_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.Unconditional))]
    public void D01_RowUnconditional_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.UnlessTrue))]
    public void D02_RowUnlessTrue_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.UnlessFalse))]
    public void D03_RowUnlessFalse_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.WhenTrue))]
    public void D04_RowWhenTrue_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.WhenFalse))]
    public void D05_RowWhenFalse_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr")]
    public void D06_AttributeUnconditional_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D07_AttributeUnlessTrue_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D08_AttributeUnlessFalse_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysTrue))]
    public void D09_AttributeWhenTrue_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysFalse))]
    public void D10_AttributeWhenFalse_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.Unconditional), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D11_UnconditionalRowOverConditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.UnlessFalse), Skip = "attr")]
    public void D12_ConditionalRowOverUnconditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(ComposeDataMatrixRows.WhenTrue), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D13_OppositeKindRowOverAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");
}

public sealed class ComposeDataTheorySkipMatrix_TheorySkipWhenFalse : ComposeDataMatrixRows
{
    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip))]
    public void D00_NoSkip_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.Unconditional))]
    public void D01_RowUnconditional_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.UnlessTrue))]
    public void D02_RowUnlessTrue_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.UnlessFalse))]
    public void D03_RowUnlessFalse_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.WhenTrue))]
    public void D04_RowWhenTrue_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.WhenFalse))]
    public void D05_RowWhenFalse_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr")]
    public void D06_AttributeUnconditional_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D07_AttributeUnlessTrue_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D08_AttributeUnlessFalse_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysTrue))]
    public void D09_AttributeWhenTrue_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysFalse))]
    public void D10_AttributeWhenFalse_Runs(int value) => value.Should().Be(1);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.Unconditional), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D11_UnconditionalRowOverConditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.UnlessFalse), Skip = "attr")]
    public void D12_ConditionalRowOverUnconditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(ComposeDataMatrixRows.WhenTrue), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D13_OppositeKindRowOverAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");
}

public sealed class ComposeDataTheorySkipMatrix_TheorySkipUnconditional : ComposeDataMatrixRows
{
    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;

    [Theory(Skip = "theory")] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip))]
    public void D00_NoSkip_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(ComposeDataMatrixRows.Unconditional))]
    public void D01_RowUnconditional_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(ComposeDataMatrixRows.UnlessTrue))]
    public void D02_RowUnlessTrue_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(ComposeDataMatrixRows.UnlessFalse))]
    public void D03_RowUnlessFalse_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(ComposeDataMatrixRows.WhenTrue))]
    public void D04_RowWhenTrue_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(ComposeDataMatrixRows.WhenFalse))]
    public void D05_RowWhenFalse_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr")]
    public void D06_AttributeUnconditional_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D07_AttributeUnlessTrue_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D08_AttributeUnlessFalse_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysTrue))]
    public void D09_AttributeWhenTrue_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(ComposeDataMatrixRows.NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysFalse))]
    public void D10_AttributeWhenFalse_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(ComposeDataMatrixRows.Unconditional), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D11_UnconditionalRowOverConditionalAttribute_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(ComposeDataMatrixRows.UnlessFalse), Skip = "attr")]
    public void D12_ConditionalRowOverUnconditionalAttribute_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(ComposeDataMatrixRows.WhenTrue), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D13_OppositeKindRowOverAttribute_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");
}
