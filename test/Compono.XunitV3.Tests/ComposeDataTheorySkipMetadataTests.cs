using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Compono.XunitV3.Tests;

// ADR-0070 Amendment 5 / PLAN-0071 B6.0: the JIT counterpart of the AOT factory metadata matrix.
// For all 84 theory x ComposeData cases it reads the ComposeData row group through
// ComposeDataAttribute.GetData, asserts the grouped metadata (whole-group precedence: the
// row-source group replaces the ComposeData-attribute group), then applies xUnit's JIT skip-merge
// contract (an unconditional theory Skip short-circuits every row; otherwise the row group replaces
// the theory group, else the theory group applies) and asserts the final outcome and skip reason
// against the same explicit expectation the real-runner matrix and the AOT factory test use.
public sealed class ComposeDataTheorySkipMetadataTests
{
    private const int GroupNoCondition = 0;
    private const int GroupUnless = 1;
    private const int GroupWhen = 2;

    [Fact]
    public async Task GetData_MetadataMatchesExpectedOutcomeAndReasonForAll84Cases()
    {
        var checkedCases = 0;

        foreach (var (suffix, theoryType) in TheoryTypes())
        {
            var cases = CollectCases(theoryType);

            foreach (var (code, method) in cases)
            {
                var attribute = method.GetCustomAttribute<ComposeDataAttribute>();
                Assert.NotNull(attribute);

                var rows = await attribute!.GetData(method, new DisposalTracker());
                var row = Assert.IsType<TheoryDataRow>(Assert.Single(rows));

                AssertGroupMetadata(code, row);

                var theory = method.GetCustomAttribute<FactAttribute>();
                Assert.NotNull(theory);

                var (expectedSkip, expectedReason) = Expected(suffix, code);
                var (actualSkip, actualReason) = Evaluate(row, theory!, method.DeclaringType!);

                Assert.Equal(expectedSkip, actualSkip);
                Assert.Equal(expectedReason, actualReason);

                checkedCases++;
            }
        }

        Assert.Equal(84, checkedCases);
    }

    private static void AssertGroupMetadata(string code, TheoryDataRow row)
    {
        var (reason, kind) = GroupExpectation(code);

        Assert.Equal(reason, row.Skip);

        switch (kind)
        {
            case GroupNoCondition:
                Assert.Null(row.SkipUnless);
                Assert.Null(row.SkipWhen);
                break;
            case GroupUnless:
                Assert.NotNull(row.SkipUnless);
                Assert.Null(row.SkipWhen);
                break;
            case GroupWhen:
                Assert.Null(row.SkipUnless);
                Assert.NotNull(row.SkipWhen);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(code));
        }
    }

    private static (bool Skip, string? Reason) Evaluate(TheoryDataRow row, FactAttribute theory, Type fallbackType)
    {
        // An unconditional theory-level Skip (reason set, no condition) short-circuits the whole theory.
        if (theory.Skip is not null && theory.SkipUnless is null && theory.SkipWhen is null)
        {
            return (true, theory.Skip);
        }

        string? reason;
        string? unless;
        string? when;
        Type? skipType;

        if (row.Skip is not null)
        {
            // Whole-group precedence: the row-source group replaces the ComposeData-attribute group.
            reason = row.Skip;
            unless = row.SkipUnless;
            when = row.SkipWhen;
            skipType = row.SkipType ?? fallbackType;
        }
        else if (theory.Skip is not null)
        {
            reason = theory.Skip;
            unless = theory.SkipUnless;
            when = theory.SkipWhen;
            skipType = theory.SkipType ?? fallbackType;
        }
        else
        {
            return (false, null);
        }

        // The skip reason is only reported when the row actually skips.
        if (when is not null)
        {
            var skip = ResolvePredicate(skipType, when);
            return (skip, skip ? reason : null);
        }

        if (unless is not null)
        {
            var skip = !ResolvePredicate(skipType, unless);
            return (skip, skip ? reason : null);
        }

        return (true, reason);
    }

    private static bool ResolvePredicate(Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        Assert.NotNull(property);
        return (bool)property!.GetValue(null)!;
    }

    private static (string? Reason, int Kind) GroupExpectation(string code) => code switch
    {
        "D00" => (null, GroupNoCondition),
        "D01" => ("row", GroupNoCondition),
        "D02" => ("row", GroupUnless),
        "D03" => ("row", GroupUnless),
        "D04" => ("row", GroupWhen),
        "D05" => ("row", GroupWhen),
        "D06" => ("attr", GroupNoCondition),
        "D07" => ("attr", GroupUnless),
        "D08" => ("attr", GroupUnless),
        "D09" => ("attr", GroupWhen),
        "D10" => ("attr", GroupWhen),
        "D11" => ("row", GroupNoCondition),
        "D12" => ("row", GroupUnless),
        "D13" => ("row", GroupWhen),
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };

    private static (bool Skip, string? Reason) Expected(string suffix, string code)
    {
        // The unconditional-Skip theory short-circuits every row, so all 14 skip with reason "theory".
        if (suffix == "TheorySkipUnconditional")
        {
            return (true, "theory");
        }

        if (code == "D00")
        {
            return suffix is "TheorySkipUnlessFalse" or "TheorySkipWhenTrue"
                ? (true, "theory")
                : (false, null);
        }

        return code switch
        {
            "D01" => (true, "row"),
            "D02" => (false, null),
            "D03" => (true, "row"),
            "D04" => (true, "row"),
            "D05" => (false, null),
            "D06" => (true, "attr"),
            "D07" => (false, null),
            "D08" => (true, "attr"),
            "D09" => (true, "attr"),
            "D10" => (false, null),
            "D11" => (true, "row"),
            "D12" => (true, "row"),
            "D13" => (true, "row"),
            _ => throw new ArgumentOutOfRangeException(nameof(code)),
        };
    }

    private static (string Suffix, Type TheoryType)[] TheoryTypes() =>
    [
        ("NoTheoryCondition", typeof(ComposeDataTheorySkipMatrix_NoTheoryCondition)),
        ("TheorySkipUnlessTrue", typeof(ComposeDataTheorySkipMatrix_TheorySkipUnlessTrue)),
        ("TheorySkipUnlessFalse", typeof(ComposeDataTheorySkipMatrix_TheorySkipUnlessFalse)),
        ("TheorySkipWhenTrue", typeof(ComposeDataTheorySkipMatrix_TheorySkipWhenTrue)),
        ("TheorySkipWhenFalse", typeof(ComposeDataTheorySkipMatrix_TheorySkipWhenFalse)),
        ("TheorySkipUnconditional", typeof(ComposeDataTheorySkipMatrix_TheorySkipUnconditional)),
    ];

    private static Dictionary<string, MethodInfo> CollectCases(Type theoryType)
    {
        var cases = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);

        foreach (var method in theoryType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (method.GetCustomAttribute<ComposeDataAttribute>() is null)
            {
                continue;
            }

            var name = method.Name;
            if (name.Length >= 3 && name[0] == 'D' && char.IsDigit(name[1]) && char.IsDigit(name[2]))
            {
                cases[name.Substring(0, 3)] = method;
            }
        }

        Assert.Equal(14, cases.Count);
        return cases;
    }
}
