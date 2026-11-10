using Compono.XunitV3.Aot;
using Compono.XunitV3.Aot.Tests.Support;
using Xunit;
using Xunit.v3;

namespace Compono.XunitV3.Aot.Tests;

// ADR-0070 Amendment 5 / PLAN-0071 B6.0: the AOT mirror of the JIT theory-level skip matrix
// (test/Compono.XunitV3.Tests/ComposeDataTheorySkipParityTests.cs). The same 84 theory x ComposeData
// cases run under the real Native AOT xUnit runner (outcome parity), and the TheorySkipParityMatrix
// [Fact] drives the generated registered factory for every case to assert the exact merged skip reason
// and effective skip outcome (reason parity) through RegisteredComposeDataFactory.
//
// AOT row conditions are Func<bool> delegates (xUnit AOT's TheoryDataRow shape); JIT uses a
// string condition name plus SkipType. Theory/attribute condition names resolve on the test class.

public class ComposeDataMatrixRows
{
    public static object?[][] NoSkip => [[1]];

    public static object?[] Unconditional() => [new TheoryDataRow(1) { Skip = "row" }];

    public static object?[] UnlessTrue() =>
        [new TheoryDataRow(1) { Skip = "row", SkipUnless = static () => true }];

    public static object?[] UnlessFalse() =>
        [new TheoryDataRow(1) { Skip = "row", SkipUnless = static () => false }];

    public static object?[] WhenTrue() =>
        [new TheoryDataRow(1) { Skip = "row", SkipWhen = static () => true }];

    public static object?[] WhenFalse() =>
        [new TheoryDataRow(1) { Skip = "row", SkipWhen = static () => false }];
}

public sealed class ComposeDataTheorySkipMatrix_NoTheoryCondition : ComposeDataMatrixRows
{
    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;

    [Theory] [ComposeData(nameof(NoSkip))]
    public void D00_NoSkip_Runs(int value) => Assert.Equal(1, value);

    [Theory] [ComposeData(nameof(Unconditional))]
    public void D01_RowUnconditional_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(UnlessTrue))]
    public void D02_RowUnlessTrue_Runs(int value) => Assert.Equal(1, value);

    [Theory] [ComposeData(nameof(UnlessFalse))]
    public void D03_RowUnlessFalse_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(WhenTrue))]
    public void D04_RowWhenTrue_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(WhenFalse))]
    public void D05_RowWhenFalse_Runs(int value) => Assert.Equal(1, value);

    [Theory] [ComposeData(nameof(NoSkip), Skip = "attr")]
    public void D06_AttributeUnconditional_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D07_AttributeUnlessTrue_Runs(int value) => Assert.Equal(1, value);

    [Theory] [ComposeData(nameof(NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D08_AttributeUnlessFalse_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysTrue))]
    public void D09_AttributeWhenTrue_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysFalse))]
    public void D10_AttributeWhenFalse_Runs(int value) => Assert.Equal(1, value);

    [Theory] [ComposeData(nameof(Unconditional), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D11_UnconditionalRowOverConditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(UnlessFalse), Skip = "attr")]
    public void D12_ConditionalRowOverUnconditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory] [ComposeData(nameof(WhenTrue), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D13_OppositeKindRowOverAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");
}

public sealed class ComposeDataTheorySkipMatrix_TheorySkipUnlessTrue : ComposeDataMatrixRows
{
    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(NoSkip))]
    public void D00_NoSkip_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(Unconditional))]
    public void D01_RowUnconditional_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(UnlessTrue))]
    public void D02_RowUnlessTrue_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(UnlessFalse))]
    public void D03_RowUnlessFalse_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(WhenTrue))]
    public void D04_RowWhenTrue_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(WhenFalse))]
    public void D05_RowWhenFalse_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(NoSkip), Skip = "attr")]
    public void D06_AttributeUnconditional_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D07_AttributeUnlessTrue_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D08_AttributeUnlessFalse_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysTrue))]
    public void D09_AttributeWhenTrue_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysFalse))]
    public void D10_AttributeWhenFalse_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(Unconditional), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D11_UnconditionalRowOverConditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(UnlessFalse), Skip = "attr")]
    public void D12_ConditionalRowOverUnconditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysTrue))] [ComposeData(nameof(WhenTrue), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D13_OppositeKindRowOverAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");
}

public sealed class ComposeDataTheorySkipMatrix_TheorySkipUnlessFalse : ComposeDataMatrixRows
{
    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(NoSkip))]
    public void D00_NoSkip_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(Unconditional))]
    public void D01_RowUnconditional_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(UnlessTrue))]
    public void D02_RowUnlessTrue_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(UnlessFalse))]
    public void D03_RowUnlessFalse_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(WhenTrue))]
    public void D04_RowWhenTrue_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(WhenFalse))]
    public void D05_RowWhenFalse_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(NoSkip), Skip = "attr")]
    public void D06_AttributeUnconditional_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D07_AttributeUnlessTrue_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D08_AttributeUnlessFalse_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysTrue))]
    public void D09_AttributeWhenTrue_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysFalse))]
    public void D10_AttributeWhenFalse_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(Unconditional), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D11_UnconditionalRowOverConditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(UnlessFalse), Skip = "attr")]
    public void D12_ConditionalRowOverUnconditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipUnless = nameof(AlwaysFalse))] [ComposeData(nameof(WhenTrue), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D13_OppositeKindRowOverAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");
}

public sealed class ComposeDataTheorySkipMatrix_TheorySkipWhenTrue : ComposeDataMatrixRows
{
    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(NoSkip))]
    public void D00_NoSkip_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(Unconditional))]
    public void D01_RowUnconditional_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(UnlessTrue))]
    public void D02_RowUnlessTrue_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(UnlessFalse))]
    public void D03_RowUnlessFalse_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(WhenTrue))]
    public void D04_RowWhenTrue_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(WhenFalse))]
    public void D05_RowWhenFalse_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(NoSkip), Skip = "attr")]
    public void D06_AttributeUnconditional_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D07_AttributeUnlessTrue_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D08_AttributeUnlessFalse_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysTrue))]
    public void D09_AttributeWhenTrue_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysFalse))]
    public void D10_AttributeWhenFalse_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(Unconditional), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D11_UnconditionalRowOverConditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(UnlessFalse), Skip = "attr")]
    public void D12_ConditionalRowOverUnconditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysTrue))] [ComposeData(nameof(WhenTrue), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D13_OppositeKindRowOverAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");
}

public sealed class ComposeDataTheorySkipMatrix_TheorySkipWhenFalse : ComposeDataMatrixRows
{
    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(NoSkip))]
    public void D00_NoSkip_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(Unconditional))]
    public void D01_RowUnconditional_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(UnlessTrue))]
    public void D02_RowUnlessTrue_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(UnlessFalse))]
    public void D03_RowUnlessFalse_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(WhenTrue))]
    public void D04_RowWhenTrue_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(WhenFalse))]
    public void D05_RowWhenFalse_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(NoSkip), Skip = "attr")]
    public void D06_AttributeUnconditional_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D07_AttributeUnlessTrue_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D08_AttributeUnlessFalse_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysTrue))]
    public void D09_AttributeWhenTrue_Skips(int value) => Assert.Fail($"Attribute skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysFalse))]
    public void D10_AttributeWhenFalse_Runs(int value) => Assert.Equal(1, value);

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(Unconditional), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D11_UnconditionalRowOverConditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(UnlessFalse), Skip = "attr")]
    public void D12_ConditionalRowOverUnconditionalAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");

    [Theory(Skip = "theory", SkipWhen = nameof(AlwaysFalse))] [ComposeData(nameof(WhenTrue), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D13_OppositeKindRowOverAttribute_Skips(int value) => Assert.Fail($"Row skip expected; ran with {value}.");
}

public sealed class ComposeDataTheorySkipMatrix_TheorySkipUnconditional : ComposeDataMatrixRows
{
    public static bool AlwaysTrue => true;

    public static bool AlwaysFalse => false;

    [Theory(Skip = "theory")] [ComposeData(nameof(NoSkip))]
    public void D00_NoSkip_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(Unconditional))]
    public void D01_RowUnconditional_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(UnlessTrue))]
    public void D02_RowUnlessTrue_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(UnlessFalse))]
    public void D03_RowUnlessFalse_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(WhenTrue))]
    public void D04_RowWhenTrue_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(WhenFalse))]
    public void D05_RowWhenFalse_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(NoSkip), Skip = "attr")]
    public void D06_AttributeUnconditional_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D07_AttributeUnlessTrue_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(NoSkip), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D08_AttributeUnlessFalse_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysTrue))]
    public void D09_AttributeWhenTrue_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(NoSkip), Skip = "attr", SkipWhen = nameof(AlwaysFalse))]
    public void D10_AttributeWhenFalse_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(Unconditional), Skip = "attr", SkipUnless = nameof(AlwaysFalse))]
    public void D11_UnconditionalRowOverConditionalAttribute_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(UnlessFalse), Skip = "attr")]
    public void D12_ConditionalRowOverUnconditionalAttribute_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");

    [Theory(Skip = "theory")] [ComposeData(nameof(WhenTrue), Skip = "attr", SkipUnless = nameof(AlwaysTrue))]
    public void D13_OppositeKindRowOverAttribute_Skips(int value) => Assert.Fail($"Theory skip expected; ran with {value}.");
}

public sealed class ComposeDataTheorySkipParityTests
{
    private const string NoTheory = "global::Compono.XunitV3.Aot.Tests.ComposeDataTheorySkipMatrix_NoTheoryCondition";
    private const string UnlessTrue = "global::Compono.XunitV3.Aot.Tests.ComposeDataTheorySkipMatrix_TheorySkipUnlessTrue";
    private const string UnlessFalse = "global::Compono.XunitV3.Aot.Tests.ComposeDataTheorySkipMatrix_TheorySkipUnlessFalse";
    private const string WhenTrue = "global::Compono.XunitV3.Aot.Tests.ComposeDataTheorySkipMatrix_TheorySkipWhenTrue";
    private const string WhenFalse = "global::Compono.XunitV3.Aot.Tests.ComposeDataTheorySkipMatrix_TheorySkipWhenFalse";
    private const string Unconditional = "global::Compono.XunitV3.Aot.Tests.ComposeDataTheorySkipMatrix_TheorySkipUnconditional";

    // Theory-level skip-condition kind the generator records from the [Theory]/[Fact] named arguments.
    private const int TheoryNone = 0;
    private const int TheoryUnless = 1;
    private const int TheoryWhen = 2;

    // Which condition field (if any) a ComposeData group carries.
    private const int GroupNoCondition = 0;
    private const int GroupUnless = 1;
    private const int GroupWhen = 2;

    // All six matrix theory classes paired with the theory-condition kind the generator discovers.
    private static readonly (string Index, int TheoryKind)[] AllTheories =
    [
        (NoTheory, TheoryNone),
        (UnlessTrue, TheoryUnless),
        (UnlessFalse, TheoryUnless),
        (WhenTrue, TheoryWhen),
        (WhenFalse, TheoryWhen),
        (Unconditional, TheoryNone),
    ];

    // The 13 grouped ComposeData variants: the governing group's Skip reason, that group's own
    // condition field, and the row's final skip decision.
    private static readonly (string Name, string Reason, int GroupKind, bool ExpectedSkip)[] GroupedCases =
    [
        ("D01_RowUnconditional_Skips", "row", GroupNoCondition, true),
        ("D02_RowUnlessTrue_Runs", "row", GroupUnless, false),
        ("D03_RowUnlessFalse_Skips", "row", GroupUnless, true),
        ("D04_RowWhenTrue_Skips", "row", GroupWhen, true),
        ("D05_RowWhenFalse_Runs", "row", GroupWhen, false),
        ("D06_AttributeUnconditional_Skips", "attr", GroupNoCondition, true),
        ("D07_AttributeUnlessTrue_Runs", "attr", GroupUnless, false),
        ("D08_AttributeUnlessFalse_Skips", "attr", GroupUnless, true),
        ("D09_AttributeWhenTrue_Skips", "attr", GroupWhen, true),
        ("D10_AttributeWhenFalse_Runs", "attr", GroupWhen, false),
        ("D11_UnconditionalRowOverConditionalAttribute_Skips", "row", GroupNoCondition, true),
        ("D12_ConditionalRowOverUnconditionalAttribute_Skips", "row", GroupUnless, true),
        ("D13_OppositeKindRowOverAttribute_Skips", "row", GroupWhen, true),
    ];

    [Fact]
    public async Task TheorySkipParityMatrix_EveryCaseCarriesOneGoverningFieldWithTheExpectedReasonAndOutcome()
    {
        var checkedCases = 0;

        foreach (var (testClassIndex, theoryKind) in AllTheories)
        {
            foreach (var (methodName, reason, groupKind, expectedSkip) in GroupedCases)
            {
                // ADR-0070 Amendment 5: when the theory records a condition kind, normalization rewrites
                // the selected group into the theory's own field; when it is None the group's own field is
                // left as the B5.8 grouped selection produced it.
                var expectedFieldKind = theoryKind == TheoryNone ? groupKind : theoryKind;
                await AssertGroupedRow(testClassIndex, MethodNameFor(testClassIndex, methodName), reason, expectedFieldKind, expectedSkip);
                checkedCases++;
            }

            // D00 (no ComposeData skip) must be untouched by normalization wherever the theory records a
            // condition: no group, so no row fields at all.
            var d00Rows = await RegisteredComposeDataFactory.InvokeAsync(testClassIndex, D00MethodName(testClassIndex));
            var d00Row = Assert.IsType<TheoryDataRow>(Assert.Single(d00Rows));
            Assert.Null(d00Row.Skip);
            Assert.Null(d00Row.SkipUnless);
            Assert.Null(d00Row.SkipWhen);
            checkedCases++;
        }

        Assert.Equal(84, checkedCases);
    }

    // Each matrix class defines exactly one D00 method: "Runs" when the theory's own skip does not apply
    // to the no-skip row (None, UnlessTrue, WhenFalse), "Skips" when it does (UnlessFalse, WhenTrue, and
    // the unconditional-Skip theory).
    private static string D00MethodName(string testClassIndex) => testClassIndex switch
    {
        UnlessFalse => "D00_NoSkip_Skips",
        WhenTrue => "D00_NoSkip_Skips",
        Unconditional => "D00_NoSkip_Skips",
        _ => "D00_NoSkip_Runs",
    };

    // The unconditional-Skip theory short-circuits every row, so its four expected-run cases are named
    // "..._Skips" like the rest of that class; the other five classes use "..._Runs" for those cases.
    private static string MethodNameFor(string testClassIndex, string methodName) =>
        testClassIndex == Unconditional ? methodName.Replace("_Runs", "_Skips") : methodName;

    private static async Task AssertGroupedRow(string testClassIndex, string methodName, string reason, int expectedFieldKind, bool expectedSkip)
    {
        var rows = await RegisteredComposeDataFactory.InvokeAsync(testClassIndex, methodName);
        var dataRow = Assert.IsType<TheoryDataRow>(Assert.Single(rows));

        // The governing group's Skip reason is preserved exactly; normalization never rewrites it.
        Assert.Equal(reason, dataRow.Skip);

        // Normalization guarantees at most one condition field governs the row, never both (the xUnit
        // AOT mutual-exclusion failure mode this fix removes).
        Assert.False(dataRow.SkipUnless is not null && dataRow.SkipWhen is not null);

        switch (expectedFieldKind)
        {
            case GroupNoCondition:
                Assert.Null(dataRow.SkipUnless);
                Assert.Null(dataRow.SkipWhen);
                Assert.True(expectedSkip);
                break;
            case GroupUnless:
                Assert.NotNull(dataRow.SkipUnless);
                Assert.Null(dataRow.SkipWhen);
                Assert.Equal(!expectedSkip, dataRow.SkipUnless!());
                break;
            case GroupWhen:
                Assert.NotNull(dataRow.SkipWhen);
                Assert.Null(dataRow.SkipUnless);
                Assert.Equal(expectedSkip, dataRow.SkipWhen!());
                break;
        }
    }
}
