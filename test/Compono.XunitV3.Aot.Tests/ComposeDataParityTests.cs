using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Compono;
using Compono.XunitV3.Aot;
using Compono.XunitV3.Aot.Tests.Support;
using Xunit;
using Xunit.Sdk;

namespace Compono.XunitV3.ComposedDataParity;

/// <summary>
/// B6.1 cross-engine parity (managed AOT and Native AOT side): error text, seed lines, and
/// member/method naming for <c>[ComposeData]</c> failures, exercised through the generated AOT
/// factories via the <see cref="RegisteredComposeDataFactory"/> harness. Every fixture in this file
/// shares its namespace, type names, and member names with the mirrored <c>Compono.XunitV3.Tests</c>
/// file so the expected strings are identical literals on both engines (PLAN-0071 B6.1 / SQ2). This
/// project deliberately does not reference any assertion helper library (it is trimmed by Native AOT),
/// so assertions use xUnit's <see cref="Assert"/> with exact-equality semantics matching the JIT file.
/// See ADR-0070.
/// </summary>
public sealed class ComposeDataParityTests
{
    private const string FailureTargetsIndex =
        "global::Compono.XunitV3.ComposedDataParity.ParityFailureTargets";

    // The registration index joins a nested type to its container with '.', distinct from the CLR '+'
    // form Type.FullName uses in the {M}/{T} message - both representations are pinned here and in the
    // mirrored JIT test.
    private const string NestedTargetIndex =
        "global::Compono.XunitV3.ComposedDataParity.ParityNestedContainer.ParityNestedTarget";

    private const string GenericHandoffIndex =
        "global::Compono.XunitV3.ComposedDataParity.ParityGenericHandoffTarget";

    private const string InheritedFailureBaseIndex =
        "global::Compono.XunitV3.ComposedDataParity.ParityInheritedFailureBase";

    private const string InheritedBaseIndex =
        "global::Compono.XunitV3.ComposedDataParity.ParityInheritedBase";

    // Literal CLR names of the non-generic fixtures, identical to the mirrored JIT file's constants.
    private const string FailureTargetsName = "Compono.XunitV3.ComposedDataParity.ParityFailureTargets";
    private const string NestedTargetName = "Compono.XunitV3.ComposedDataParity.ParityNestedContainer+ParityNestedTarget";
    private const string GenericHandoffTargetName = "Compono.XunitV3.ComposedDataParity.ParityGenericHandoffTarget";
    private const string InheritedFailureBaseName = "Compono.XunitV3.ComposedDataParity.ParityInheritedFailureBase";
    private const string WidgetConfigName = "Compono.XunitV3.ComposedDataParity.ParityWidgetConfig";

    private static string Member(string member) => $"{FailureTargetsName}.{member}";

    private static string Test(string method) => $"{FailureTargetsName}.{method}";

    private const string PreRowUnassigned =
        "\n\nSeed: not assigned (the failure occurred before the row's composition started)";

    private static string PreRowSeed(int seed) => $"\n\nSeed: {seed}";

    private static string RowSeed(int seed) => $"\n\nSeed: {seed}";

    private static string RowFailure(int index, string member, string method, string cause, string seedLine) =>
        $"ComposeData row {index} from '{member}' for '{method}' failed: {cause}{seedLine}";

    private static async Task<CompositionException> FailureAsync(string testClassIndex, string methodName)
    {
        Func<Task> act = () => RegisteredComposeDataFactory.InvokeAsync(testClassIndex, methodName).AsTask();
        return await Assert.ThrowsAsync<CompositionException>(act);
    }

    private static async Task<IReadOnlyCollection<ITheoryDataRow>> RowsAsync(string testClassIndex, string methodName) =>
        await RegisteredComposeDataFactory.InvokeAsync(testClassIndex, methodName);

    // -------------------------------------------------------------------------------------------------
    // R0 exact error text - one scenario per runtime R0 failure class reachable on both engines.
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Null_row_failure_text_is_exact()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.NullRowRows)),
            Test(nameof(ParityFailureTargets.NullRowTarget)), "row is null", PreRowUnassigned);

        var exception = await FailureAsync(FailureTargetsIndex, nameof(ParityFailureTargets.NullRowTarget));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task Unsupported_row_shape_failure_text_is_exact()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.UnsupportedShapeRows)),
            Test(nameof(ParityFailureTargets.UnsupportedShapeTarget)),
            "row has unsupported type 'System.String'; expected ITheoryDataRow, object?[], or ITuple",
            PreRowUnassigned);

        var exception = await FailureAsync(FailureTargetsIndex, nameof(ParityFailureTargets.UnsupportedShapeTarget));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task Excess_row_values_failure_text_is_exact()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.TooManyRows)),
            Test(nameof(ParityFailureTargets.TooManyTarget)),
            "too many values: 3 supplied, but the method has 2 parameter(s)", RowSeed(7));

        var exception = await FailureAsync(FailureTargetsIndex, nameof(ParityFailureTargets.TooManyTarget));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task Null_for_non_nullable_parameter_failure_text_is_exact()
    {
        var method = Test(nameof(ParityFailureTargets.NullValueTarget));
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.NullValueRows)), method,
            $"Row value for parameter 'label' on '{method}' is null, but the parameter is not nullable.",
            RowSeed(8));

        var exception = await FailureAsync(FailureTargetsIndex, nameof(ParityFailureTargets.NullValueTarget));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task Type_mismatch_failure_text_is_exact()
    {
        var method = Test(nameof(ParityFailureTargets.TypeMismatchTarget));
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.TypeMismatchRows)), method,
            $"Row value for parameter 'label' on '{method}' has type 'System.Int32', which is not assignable to 'System.String'.",
            RowSeed(9));

        var exception = await FailureAsync(FailureTargetsIndex, nameof(ParityFailureTargets.TypeMismatchTarget));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task Config_extraction_failure_text_is_exact()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.ConfigRows)),
            Test(nameof(ParityFailureTargets.ConfigTarget)),
            $"the first row element must be a non-null '{WidgetConfigName}' profile configuration value",
            PreRowSeed(42));

        var exception = await FailureAsync(FailureTargetsIndex, nameof(ParityFailureTargets.ConfigTarget));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task Member_read_failure_text_is_exact()
    {
        var expected =
            $"ComposeData member '{Member(nameof(ParityFailureTargets.ThrowingRows))}' "
            + $"for '{Test(nameof(ParityFailureTargets.ThrowingTarget))}' failed while reading its value: parity read boom";

        var exception = await FailureAsync(FailureTargetsIndex, nameof(ParityFailureTargets.ThrowingTarget));

        Assert.Equal(expected, exception.Message);
        // The unwrapped read failure is preserved as the InnerException (ComposeDataRows.MemberReadFailure).
        var inner = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("parity read boom", inner.Message);
    }

    [Fact]
    public async Task Profile_constructor_failure_text_is_exact()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.CtorRows)),
            Test(nameof(ParityFailureTargets.CtorTarget)), "parity ctor boom", PreRowUnassigned);

        var exception = await FailureAsync(FailureTargetsIndex, nameof(ParityFailureTargets.CtorTarget));

        Assert.Equal(expected, exception.Message);
        // The arity-2 profile is constructed with a direct `new TProfile(config)`, so the ctor's own
        // exception propagates unwrapped and is preserved as the InnerException.
        var inner = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("parity ctor boom", inner.Message);
    }

    [Fact]
    public async Task Profile_parameterless_constructor_failure_text_is_exact()
    {
        // ADR-0070 Amendment 6: an arity-1 profile is constructed by core AddProfile<TProfile>()'s
        // `new TProfile()`, which lowers to Activator.CreateInstance<TProfile>(). Before the fix a
        // throwing constructor surfaced as the generic "Exception has been thrown by the target of an
        // invocation." with a TargetInvocationException InnerException; the fix rethrows the original.
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.Ctor1Rows)),
            Test(nameof(ParityFailureTargets.Ctor1Target)), "parity ctor1 boom", PreRowUnassigned);

        var exception = await FailureAsync(FailureTargetsIndex, nameof(ParityFailureTargets.Ctor1Target));

        Assert.Equal(expected, exception.Message);
        var inner = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("parity ctor1 boom", inner.Message);
        Assert.IsNotType<TargetInvocationException>(exception.InnerException);
    }

    [Fact]
    public async Task Profile_parameterless_constructor_diagnostic_is_preserved_by_reference()
    {
        // ADR-0070 Amendment 6: the arity-1 constructor's CompositionException reaches the R0 row-failure
        // wrapper unwrapped, so RowFailure carries its Diagnostic by reference - the same guarantee the
        // arity-2 path already had.
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.DiagnosticCtorRows)),
            Test(nameof(ParityFailureTargets.DiagnosticCtorTarget)), "parity diagnostic boom", PreRowUnassigned);

        var exception = await FailureAsync(FailureTargetsIndex, nameof(ParityFailureTargets.DiagnosticCtorTarget));

        Assert.Equal(expected, exception.Message);
        Assert.Same(ParityDiagnosticCtorProfile.Diagnostic, exception.Diagnostic);
        Assert.Same(ParityDiagnosticCtorProfile.Captured, exception.InnerException);
    }

    // -------------------------------------------------------------------------------------------------
    // Seed-line parity - every variant B3 defines, with fixture names identical to the JIT file.
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Seed_line_attribute_seed_uses_the_assigned_value()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.NullRowSeed42Rows)),
            Test(nameof(ParityFailureTargets.NullRowSeed42Target)), "row is null", PreRowSeed(42));

        var exception = await FailureAsync(FailureTargetsIndex, nameof(ParityFailureTargets.NullRowSeed42Target));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task Seed_line_explicit_zero_is_reported_as_zero()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.NullRowSeed0Rows)),
            Test(nameof(ParityFailureTargets.NullRowSeed0Target)), "row is null", PreRowSeed(0));

        var exception = await FailureAsync(FailureTargetsIndex, nameof(ParityFailureTargets.NullRowSeed0Target));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task Seed_line_profile_with_seed_is_used_for_row_seed_failures()
    {
        var method = Test(nameof(ParityFailureTargets.ProfileSeededTypeMismatchTarget));
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.TypeMismatchRows)), method,
            $"Row value for parameter 'label' on '{method}' has type 'System.Int32', which is not assignable to 'System.String'.",
            RowSeed(1234));

        var exception = await FailureAsync(
            FailureTargetsIndex, nameof(ParityFailureTargets.ProfileSeededTypeMismatchTarget));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task Seed_line_negative_profile_seed_failure_text_is_exact()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.SimpleRows)),
            Test(nameof(ParityFailureTargets.NegativeSeedTarget)),
            "Compono.XunitV3 requires a non-negative seed, but the configured seed was -5.", RowSeed(-5));

        var exception = await FailureAsync(FailureTargetsIndex, nameof(ParityFailureTargets.NegativeSeedTarget));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task Seed_line_duplicate_seed_conflict_text_is_exact()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.SimpleRows)),
            Test(nameof(ParityFailureTargets.DuplicateSeedTarget)),
            "'WithSeed' was configured more than once (ParitySeededProfile, direct).", PreRowSeed(9));

        var exception = await FailureAsync(FailureTargetsIndex, nameof(ParityFailureTargets.DuplicateSeedTarget));

        Assert.Equal(expected, exception.Message);
    }

    // -------------------------------------------------------------------------------------------------
    // {M}/{T} name parity - nested test class and closed-generic MemberType holder.
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Name_parity_nested_test_class_uses_clr_plus_form()
    {
        // The {M}/{T} display uses Type.FullName, which joins a nested type with the CLR '+' separator
        // (NestedTargetName); the registration index (NestedTargetIndex) joins the chain with '.' - both
        // representations are pinned.
        var member = $"{NestedTargetName}.{nameof(ParityNestedContainer.ParityNestedTarget.NestedRows)}";
        var method = $"{NestedTargetName}.{nameof(ParityNestedContainer.ParityNestedTarget.NestedTargetMethod)}";
        var expected = RowFailure(0, member, method, "row is null", PreRowUnassigned);

        var exception = await FailureAsync(
            NestedTargetIndex, nameof(ParityNestedContainer.ParityNestedTarget.NestedTargetMethod));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task Name_parity_closed_generic_membertype_holder_matches_fullname_rule()
    {
        // {M} is the lookup type's FullName plus the member. A closed generic's FullName is TFM
        // dependent only in its assembly-qualified type argument (whose CoreLib version differs per
        // TFM), so that portion is taken from typeof(...).FullName here - never normalized. The
        // generic holder's literal namespace, name, arity, bracket structure and formatting are pinned
        // independently below so a regression in the emitted identity is still caught.
        var holder = typeof(ParityGenericHolder<int>).FullName!;
        Assert.StartsWith("Compono.XunitV3.ComposedDataParity.ParityGenericHolder`1[[System.Int32, ", holder);
        Assert.EndsWith("]]", holder);

        var member = $"{holder}.{nameof(ParityGenericHolder<int>.GenericRows)}";
        var method = $"{GenericHandoffTargetName}.{nameof(ParityGenericHandoffTarget.HandoffTarget)}";
        var expected = RowFailure(0, member, method, "row is null", PreRowUnassigned);

        var exception = await FailureAsync(GenericHandoffIndex, nameof(ParityGenericHandoffTarget.HandoffTarget));

        Assert.Equal(expected, exception.Message);
    }

    // -------------------------------------------------------------------------------------------------
    // ADR-0070 Amendment 2 / C-1 documenting test (managed AOT and Native AOT side).
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task C1_inherited_member_name_uses_the_declaring_class()
    {
        // C-1 (ADR-0070 Amendment 2): AOT binds the member to the inherited method's declaring class, so
        // an inherited method reflects the base class in {M} - unlike the JIT path, which reflects the
        // concrete derived class (see the mirrored JIT test). This is the documented inherited-method
        // difference; inherited methods without an explicit MemberType are excluded from {M} byte parity.
        var member = $"{InheritedFailureBaseName}.{nameof(ParityInheritedFailureBase.InheritedFailureRows)}";
        var method = $"{InheritedFailureBaseName}.{nameof(ParityInheritedFailureBase.InheritedFailureTarget)}";
        var expected = RowFailure(0, member, method, "row is null", PreRowUnassigned);

        var exception = await FailureAsync(
            InheritedFailureBaseIndex, nameof(ParityInheritedFailureBase.InheritedFailureTarget));

        Assert.Equal(expected, exception.Message);
    }

    // -------------------------------------------------------------------------------------------------
    // ADR-0070 Amendment 2 documenting tests (a)-(b) - runtime side.
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Amendment2_a_inherited_member_without_MemberType_resolves_the_declaring_member()
    {
        // Amendment 2(a): without an explicit MemberType, AOT reads the inherited method's declaring
        // class member ("base") - the JIT path reads the derived class's hiding member ("derived"). The
        // behavior differs by design; each engine's test documents its own resolution.
        var rows = await RowsAsync(InheritedBaseIndex, nameof(ParityInheritedBase.InheritedTarget));

        Assert.Equal(new object?[] { "base" }, Assert.Single(rows).GetData());
    }

    [Fact]
    public async Task Amendment2_b_inherited_member_with_MemberType_pins_the_declaring_holder()
    {
        // Amendment 2(b): an explicit MemberType pins the holder, so both engines read the same member
        // and produce identical rows ("base") - the supported workaround for the C-1 difference.
        var rows = await RowsAsync(InheritedBaseIndex, nameof(ParityInheritedBase.PinnedInheritedTarget));

        Assert.Equal(new object?[] { "base" }, Assert.Single(rows).GetData());
    }
}

// -----------------------------------------------------------------------------------------------------
// Shared fixtures. Namespaces, type names, and member names are mirrored byte-for-byte in the JIT file.
// Target methods are plain [ComposeData] methods without [Theory] (B5.10-approved S2 deviation), driven
// only through the RegisteredComposeDataFactory harness.
// -----------------------------------------------------------------------------------------------------

public sealed class ParityFailureTargets
{
    public static IEnumerable<object?[]> NullRowRows() => [null!];

    public static IEnumerable<object?> UnsupportedShapeRows() => ["not-a-row"];

    public static IEnumerable<object?[]> TooManyRows() => [new object?[] { 1, 2, 3 }];

    public static IEnumerable<object?[]> NullValueRows() => [new object?[] { null! }];

    public static IEnumerable<object?[]> TypeMismatchRows() => [new object?[] { 42 }];

    public static IEnumerable<object?[]> ConfigRows() => [new object?[] { "not-a-config" }];

    public static IEnumerable<object?[]> ThrowingRows() => throw new InvalidOperationException("parity read boom");

    public static IEnumerable<object?[]> CtorRows() => [new object?[] { new ParityWidgetConfig("value") }];

    public static IEnumerable<object?[]> Ctor1Rows() => [new object?[] { "value" }];

    public static IEnumerable<object?[]> DiagnosticCtorRows() => [new object?[] { "value" }];

    public static IEnumerable<object?[]> SimpleRows() => [new object?[] { "value" }];

    public static IEnumerable<object?[]> NullRowSeed42Rows() => [null!];

    public static IEnumerable<object?[]> NullRowSeed0Rows() => [null!];

    [ComposeData(nameof(NullRowRows))]
    public void NullRowTarget(string value)
    {
    }

    [ComposeData(nameof(UnsupportedShapeRows))]
    public void UnsupportedShapeTarget(string value)
    {
    }

    [ComposeData(nameof(TooManyRows), Seed = 7)]
    public void TooManyTarget(int first, int second)
    {
    }

    [ComposeData(nameof(NullValueRows), Seed = 8)]
    public void NullValueTarget(string label)
    {
    }

    [ComposeData(nameof(TypeMismatchRows), Seed = 9)]
    public void TypeMismatchTarget(string label)
    {
    }

    [ComposeData<ParityConfigProfile, ParityWidgetConfig>(nameof(ConfigRows), Seed = 42)]
    public void ConfigTarget(string value)
    {
    }

    [ComposeData(nameof(ThrowingRows))]
    public void ThrowingTarget(string value)
    {
    }

    [ComposeData<ParityThrowingCtorProfile, ParityWidgetConfig>(nameof(CtorRows))]
    public void CtorTarget(string value)
    {
    }

    [ComposeData<ParityThrowingParameterlessCtorProfile>(nameof(Ctor1Rows))]
    public void Ctor1Target(string value)
    {
    }

    [ComposeData<ParityDiagnosticCtorProfile>(nameof(DiagnosticCtorRows))]
    public void DiagnosticCtorTarget(string value)
    {
    }

    [ComposeData(nameof(NullRowSeed42Rows), Seed = 42)]
    public void NullRowSeed42Target(string value)
    {
    }

    [ComposeData(nameof(NullRowSeed0Rows), Seed = 0)]
    public void NullRowSeed0Target(string value)
    {
    }

    [ComposeData<ParitySeededProfile>(nameof(TypeMismatchRows))]
    public void ProfileSeededTypeMismatchTarget(string label)
    {
    }

    [ComposeData<ParityNegativeSeedProfile>(nameof(SimpleRows))]
    public void NegativeSeedTarget(string value)
    {
    }

    [ComposeData<ParitySeededProfile>(nameof(SimpleRows), Seed = 9)]
    public void DuplicateSeedTarget(string value)
    {
    }
}

public sealed class ParityWidgetConfig
{
    public ParityWidgetConfig(string value) => Value = value;

    public string Value { get; }
}

public sealed class ParityConfigProfile : ICompositionProfile
{
    private readonly ParityWidgetConfig _config;

    public ParityConfigProfile(ParityWidgetConfig config) => _config = config;

    public void Configure(CompositionBuilder builder) => builder.Register(() => _config.Value);
}

public sealed class ParityThrowingCtorProfile : ICompositionProfile
{
    public ParityThrowingCtorProfile(ParityWidgetConfig config) => throw new InvalidOperationException("parity ctor boom");

    public void Configure(CompositionBuilder builder)
    {
    }
}

public sealed class ParityThrowingParameterlessCtorProfile : ICompositionProfile
{
    public ParityThrowingParameterlessCtorProfile() => throw new InvalidOperationException("parity ctor1 boom");

    public void Configure(CompositionBuilder builder)
    {
    }
}

public sealed class ParityDiagnosticCtorProfile : ICompositionProfile
{
    public static readonly CompositionDiagnostic Diagnostic = new()
    {
        RootType = typeof(string),
        FailedType = typeof(int),
        Path = "parity-ctor",
        Trace = [],
        Seed = 7UL,
        Message = "parity diagnostic boom",
    };

    public static CompositionException? Captured;

    public ParityDiagnosticCtorProfile()
    {
        Captured = new CompositionException(Diagnostic);
        throw Captured;
    }

    public void Configure(CompositionBuilder builder)
    {
    }
}

public sealed class ParitySeededProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.WithSeed(1234);
}

public sealed class ParityNegativeSeedProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.WithSeed(-5);
}

public static class ParityNestedContainer
{
    public sealed class ParityNestedTarget
    {
        public static IEnumerable<object?[]> NestedRows() => [null!];

        [ComposeData(nameof(NestedRows))]
        public void NestedTargetMethod(string value)
        {
        }
    }
}

public sealed class ParityGenericHolder<T>
{
    public static IEnumerable<object?[]> GenericRows() => [null!];
}

public sealed class ParityGenericHandoffTarget
{
    [ComposeData(nameof(ParityGenericHolder<int>.GenericRows), MemberType = typeof(ParityGenericHolder<int>))]
    public void HandoffTarget(string value)
    {
    }
}

public class ParityInheritedBase
{
    public static IEnumerable<object?[]> InheritedRows() => [new object?[] { "base" }];

    [ComposeData(nameof(InheritedRows))]
    public void InheritedTarget(string value)
    {
    }

    [ComposeData(nameof(InheritedRows), MemberType = typeof(ParityInheritedBase))]
    public void PinnedInheritedTarget(string value)
    {
    }
}

public sealed class ParityInheritedDerived : ParityInheritedBase
{
    public static new IEnumerable<object?[]> InheritedRows() => [new object?[] { "derived" }];
}

public class ParityInheritedFailureBase
{
    public static IEnumerable<object?[]> InheritedFailureRows() => [null!];

    [ComposeData(nameof(InheritedFailureRows))]
    public void InheritedFailureTarget(string value)
    {
    }
}

public sealed class ParityInheritedFailureDerived : ParityInheritedFailureBase
{
    public static new IEnumerable<object?[]> InheritedFailureRows() => [null!];
}
