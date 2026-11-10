using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Compono;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Compono.XunitV3.ComposedDataParity;

/// <summary>
/// B6.1 cross-engine parity (managed JIT side): error text, seed lines, and member/method naming for
/// <c>[ComposeData]</c> failures, exercised through the real <see cref="ComposeDataAttribute.GetData"/>
/// path. Every fixture in this file shares its namespace, type names, and member names with the
/// mirrored <c>Compono.XunitV3.Aot.Tests</c> file so the expected strings are identical literals on
/// both engines (PLAN-0071 B6.1 / SQ2). Expected names are pinned as literals, not derived from
/// <c>typeof(...).FullName</c>, so a naming regression is caught rather than mirrored. See ADR-0070.
/// </summary>
public sealed class ComposeDataParityTests
{
    // Literal CLR names of the non-generic fixtures. These are the exact strings production emits in
    // {M}/{T}; they are pinned here so the test fails if the emitted naming ever changes.
    private const string FailureTargetsName = "Compono.XunitV3.ComposedDataParity.ParityFailureTargets";
    private const string NestedTargetName = "Compono.XunitV3.ComposedDataParity.ParityNestedContainer+ParityNestedTarget";
    private const string GenericHandoffTargetName = "Compono.XunitV3.ComposedDataParity.ParityGenericHandoffTarget";
    private const string InheritedFailureBaseName = "Compono.XunitV3.ComposedDataParity.ParityInheritedFailureBase";
    private const string InheritedFailureDerivedName = "Compono.XunitV3.ComposedDataParity.ParityInheritedFailureDerived";
    private const string WidgetConfigName = "Compono.XunitV3.ComposedDataParity.ParityWidgetConfig";

    private static string Member(string member) => $"{FailureTargetsName}.{member}";

    private static string Test(string method) => $"{FailureTargetsName}.{method}";

    private const string PreRowUnassigned =
        "\n\nSeed: not assigned (the failure occurred before the row's composition started)";

    private static string PreRowSeed(int seed) => $"\n\nSeed: {seed}";

    private static string RowSeed(int seed) => $"\n\nSeed: {seed}";

    private static string RowFailure(int index, string member, string method, string cause, string seedLine) =>
        $"ComposeData row {index} from '{member}' for '{method}' failed: {cause}{seedLine}";

    private static async Task<CompositionException> FailureAsync(
        DataAttribute attribute,
        Type declaringType,
        string methodName)
    {
        var method = declaringType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!;
        Func<Task> act = () => attribute.GetData(method, new DisposalTracker()).AsTask();
        var exception = await act.Should().ThrowAsync<CompositionException>();
        return exception.Which;
    }

    // -------------------------------------------------------------------------------------------------
    // R0 exact error text - one scenario per runtime R0 failure class reachable on both engines.
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Null_row_failure_text_is_exact()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.NullRowRows)),
            Test(nameof(ParityFailureTargets.NullRowTarget)), "row is null", PreRowUnassigned);

        var exception = await FailureAsync(
            new ComposeDataAttribute(nameof(ParityFailureTargets.NullRowRows)),
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.NullRowTarget));

        exception.Message.Should().Be(expected);
    }

    [Fact]
    public async Task Unsupported_row_shape_failure_text_is_exact()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.UnsupportedShapeRows)),
            Test(nameof(ParityFailureTargets.UnsupportedShapeTarget)),
            "row has unsupported type 'System.String'; expected ITheoryDataRow, object?[], or ITuple",
            PreRowUnassigned);

        var exception = await FailureAsync(
            new ComposeDataAttribute(nameof(ParityFailureTargets.UnsupportedShapeRows)),
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.UnsupportedShapeTarget));

        exception.Message.Should().Be(expected);
    }

    [Fact]
    public async Task Excess_row_values_failure_text_is_exact()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.TooManyRows)),
            Test(nameof(ParityFailureTargets.TooManyTarget)),
            "too many values: 3 supplied, but the method has 2 parameter(s)", RowSeed(7));

        var exception = await FailureAsync(
            new ComposeDataAttribute(nameof(ParityFailureTargets.TooManyRows)) { Seed = 7 },
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.TooManyTarget));

        exception.Message.Should().Be(expected);
    }

    [Fact]
    public async Task Null_for_non_nullable_parameter_failure_text_is_exact()
    {
        var method = Test(nameof(ParityFailureTargets.NullValueTarget));
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.NullValueRows)), method,
            $"Row value for parameter 'label' on '{method}' is null, but the parameter is not nullable.",
            RowSeed(8));

        var exception = await FailureAsync(
            new ComposeDataAttribute(nameof(ParityFailureTargets.NullValueRows)) { Seed = 8 },
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.NullValueTarget));

        exception.Message.Should().Be(expected);
    }

    [Fact]
    public async Task Type_mismatch_failure_text_is_exact()
    {
        var method = Test(nameof(ParityFailureTargets.TypeMismatchTarget));
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.TypeMismatchRows)), method,
            $"Row value for parameter 'label' on '{method}' has type 'System.Int32', which is not assignable to 'System.String'.",
            RowSeed(9));

        var exception = await FailureAsync(
            new ComposeDataAttribute(nameof(ParityFailureTargets.TypeMismatchRows)) { Seed = 9 },
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.TypeMismatchTarget));

        exception.Message.Should().Be(expected);
    }

    [Fact]
    public async Task Config_extraction_failure_text_is_exact()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.ConfigRows)),
            Test(nameof(ParityFailureTargets.ConfigTarget)),
            $"the first row element must be a non-null '{WidgetConfigName}' profile configuration value",
            PreRowSeed(42));

        var exception = await FailureAsync(
            new ComposeDataAttribute<ParityConfigProfile, ParityWidgetConfig>(nameof(ParityFailureTargets.ConfigRows)) { Seed = 42 },
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.ConfigTarget));

        exception.Message.Should().Be(expected);
    }

    [Fact]
    public async Task Member_read_failure_text_is_exact()
    {
        var expected =
            $"ComposeData member '{Member(nameof(ParityFailureTargets.ThrowingRows))}' "
            + $"for '{Test(nameof(ParityFailureTargets.ThrowingTarget))}' failed while reading its value: parity read boom";

        var exception = await FailureAsync(
            new ComposeDataAttribute(nameof(ParityFailureTargets.ThrowingRows)),
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.ThrowingTarget));

        exception.Message.Should().Be(expected);
        // The unwrapped read failure is preserved as the InnerException (ComposeDataRows.MemberReadFailure).
        exception.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("parity read boom");
    }

    [Fact]
    public async Task Profile_constructor_failure_text_is_exact()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.CtorRows)),
            Test(nameof(ParityFailureTargets.CtorTarget)), "parity ctor boom", PreRowUnassigned);

        var exception = await FailureAsync(
            new ComposeDataAttribute<ParityThrowingCtorProfile, ParityWidgetConfig>(nameof(ParityFailureTargets.CtorRows)),
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.CtorTarget));

        exception.Message.Should().Be(expected);
        // The arity-2 profile is constructed by ConfigProfileBinder.InvokeProfile through a cached
        // ConstructorInfo (not a direct `new TProfile(config)`); ConfigProfileBinder.Invoke unwraps
        // ConstructorInfo.Invoke's TargetInvocationException with ExceptionDispatchInfo, so the ctor's
        // own exception message survives here instead of the generic reflection wrapper - and is
        // preserved as the InnerException.
        exception.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("parity ctor boom");
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

        var exception = await FailureAsync(
            new ComposeDataAttribute<ParityThrowingParameterlessCtorProfile>(nameof(ParityFailureTargets.Ctor1Rows)),
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.Ctor1Target));

        exception.Message.Should().Be(expected);
        exception.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("parity ctor1 boom");
        exception.InnerException.Should().NotBeOfType<TargetInvocationException>();
    }

    [Fact]
    public async Task Profile_parameterless_constructor_diagnostic_is_preserved_by_reference()
    {
        // ADR-0070 Amendment 6: the arity-1 constructor's CompositionException reaches the R0 row-failure
        // wrapper unwrapped, so RowFailure carries its Diagnostic by reference - the same guarantee the
        // arity-2 path already had.
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.DiagnosticCtorRows)),
            Test(nameof(ParityFailureTargets.DiagnosticCtorTarget)), "parity diagnostic boom", PreRowUnassigned);

        var exception = await FailureAsync(
            new ComposeDataAttribute<ParityDiagnosticCtorProfile>(nameof(ParityFailureTargets.DiagnosticCtorRows)),
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.DiagnosticCtorTarget));

        exception.Message.Should().Be(expected);
        exception.Diagnostic.Should().BeSameAs(ParityDiagnosticCtorProfile.Diagnostic);
        exception.InnerException.Should().BeSameAs(ParityDiagnosticCtorProfile.Captured);
    }

    // -------------------------------------------------------------------------------------------------
    // Seed-line parity - every variant B3 defines, with fixture names identical to the AOT file.
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Seed_line_attribute_seed_uses_the_assigned_value()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.NullRowSeed42Rows)),
            Test(nameof(ParityFailureTargets.NullRowSeed42Target)), "row is null", PreRowSeed(42));

        var exception = await FailureAsync(
            new ComposeDataAttribute(nameof(ParityFailureTargets.NullRowSeed42Rows)) { Seed = 42 },
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.NullRowSeed42Target));

        exception.Message.Should().Be(expected);
    }

    [Fact]
    public async Task Seed_line_explicit_zero_is_reported_as_zero()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.NullRowSeed0Rows)),
            Test(nameof(ParityFailureTargets.NullRowSeed0Target)), "row is null", PreRowSeed(0));

        var exception = await FailureAsync(
            new ComposeDataAttribute(nameof(ParityFailureTargets.NullRowSeed0Rows)) { Seed = 0 },
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.NullRowSeed0Target));

        exception.Message.Should().Be(expected);
    }

    [Fact]
    public async Task Seed_line_profile_with_seed_is_used_for_row_seed_failures()
    {
        var method = Test(nameof(ParityFailureTargets.ProfileSeededTypeMismatchTarget));
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.TypeMismatchRows)), method,
            $"Row value for parameter 'label' on '{method}' has type 'System.Int32', which is not assignable to 'System.String'.",
            RowSeed(1234));

        var exception = await FailureAsync(
            new ComposeDataAttribute<ParitySeededProfile>(nameof(ParityFailureTargets.TypeMismatchRows)),
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.ProfileSeededTypeMismatchTarget));

        exception.Message.Should().Be(expected);
    }

    [Fact]
    public async Task Seed_line_negative_profile_seed_failure_text_is_exact()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.SimpleRows)),
            Test(nameof(ParityFailureTargets.NegativeSeedTarget)),
            "Compono.XunitV3 requires a non-negative seed, but the configured seed was -5.", RowSeed(-5));

        var exception = await FailureAsync(
            new ComposeDataAttribute<ParityNegativeSeedProfile>(nameof(ParityFailureTargets.SimpleRows)),
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.NegativeSeedTarget));

        exception.Message.Should().Be(expected);
    }

    [Fact]
    public async Task Seed_line_duplicate_seed_conflict_text_is_exact()
    {
        var expected = RowFailure(0, Member(nameof(ParityFailureTargets.SimpleRows)),
            Test(nameof(ParityFailureTargets.DuplicateSeedTarget)),
            "'WithSeed' was configured more than once (ParitySeededProfile, direct).", PreRowSeed(9));

        var exception = await FailureAsync(
            new ComposeDataAttribute<ParitySeededProfile>(nameof(ParityFailureTargets.SimpleRows)) { Seed = 9 },
            typeof(ParityFailureTargets), nameof(ParityFailureTargets.DuplicateSeedTarget));

        exception.Message.Should().Be(expected);
    }

    // -------------------------------------------------------------------------------------------------
    // {M}/{T} name parity - nested test class and closed-generic MemberType holder.
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Name_parity_nested_test_class_uses_clr_plus_form()
    {
        // The {M}/{T} display uses Type.FullName, which joins a nested type with the CLR '+' separator
        // (NestedTargetName). The AOT registration index separately joins the chain with '.' - see the
        // mirrored AOT test's NestedTargetIndex constant.
        var member = $"{NestedTargetName}.{nameof(ParityNestedContainer.ParityNestedTarget.NestedRows)}";
        var method = $"{NestedTargetName}.{nameof(ParityNestedContainer.ParityNestedTarget.NestedTargetMethod)}";
        var expected = RowFailure(0, member, method, "row is null", PreRowUnassigned);

        var exception = await FailureAsync(
            new ComposeDataAttribute(nameof(ParityNestedContainer.ParityNestedTarget.NestedRows)),
            typeof(ParityNestedContainer.ParityNestedTarget),
            nameof(ParityNestedContainer.ParityNestedTarget.NestedTargetMethod));

        exception.Message.Should().Be(expected);
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
        holder.Should().StartWith("Compono.XunitV3.ComposedDataParity.ParityGenericHolder`1[[System.Int32, ");
        holder.Should().EndWith("]]");

        var member = $"{holder}.{nameof(ParityGenericHolder<int>.GenericRows)}";
        var method = $"{GenericHandoffTargetName}.{nameof(ParityGenericHandoffTarget.HandoffTarget)}";
        var expected = RowFailure(0, member, method, "row is null", PreRowUnassigned);

        var exception = await FailureAsync(
            new ComposeDataAttribute(nameof(ParityGenericHolder<int>.GenericRows)) { MemberType = typeof(ParityGenericHolder<int>) },
            typeof(ParityGenericHandoffTarget), nameof(ParityGenericHandoffTarget.HandoffTarget));

        exception.Message.Should().Be(expected);
    }

    // -------------------------------------------------------------------------------------------------
    // ADR-0070 Amendment 2 / C-1 documenting test (managed JIT side).
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task C1_inherited_member_name_uses_the_reflected_class()
    {
        // C-1 (ADR-0070 Amendment 2): JIT resolves the member against the concrete reflected test class
        // (MemberType ?? ReflectedType ?? DeclaringType), so an inherited method reflects the derived
        // class in {M}. AOT binds the member to the method's declaring class instead - see the mirrored
        // AOT test. This is the documented inherited-method difference; inherited methods without an
        // explicit MemberType are excluded from {M} byte parity.
        var member = $"{InheritedFailureDerivedName}.{nameof(ParityInheritedFailureBase.InheritedFailureRows)}";
        var method = $"{InheritedFailureBaseName}.{nameof(ParityInheritedFailureBase.InheritedFailureTarget)}";
        var expected = RowFailure(0, member, method, "row is null", PreRowUnassigned);

        var exception = await FailureAsync(
            new ComposeDataAttribute(nameof(ParityInheritedFailureBase.InheritedFailureRows)),
            typeof(ParityInheritedFailureDerived), nameof(ParityInheritedFailureBase.InheritedFailureTarget));

        exception.Message.Should().Be(expected);
    }

    // -------------------------------------------------------------------------------------------------
    // ADR-0070 Amendment 2 documenting tests (a)-(c) - runtime side.
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Amendment2_a_inherited_member_without_MemberType_resolves_the_reflected_member()
    {
        // Amendment 2(a): without an explicit MemberType, JIT reflects the derived class and reads its
        // hiding member; the row value is "derived". AOT reads the declaring class's member ("base").
        var attribute = new ComposeDataAttribute(nameof(ParityInheritedBase.InheritedRows));
        var method = typeof(ParityInheritedDerived).GetMethod(
            nameof(ParityInheritedBase.InheritedTarget), BindingFlags.Public | BindingFlags.Instance)!;

        var rows = await attribute.GetData(method, new DisposalTracker());

        rows.Should().ContainSingle().Which.GetData().Should().ContainSingle().Which.Should().Be("derived");
    }

    [Fact]
    public async Task Amendment2_b_inherited_member_with_MemberType_pins_the_declaring_holder()
    {
        // Amendment 2(b): an explicit MemberType pins the holder, so both engines read the same member
        // and produce identical rows ("base") - the supported workaround for the C-1 difference.
        var attribute = new ComposeDataAttribute(nameof(ParityInheritedBase.InheritedRows))
        {
            MemberType = typeof(ParityInheritedBase),
        };
        var method = typeof(ParityInheritedDerived).GetMethod(
            nameof(ParityInheritedBase.PinnedInheritedTarget), BindingFlags.Public | BindingFlags.Instance)!;

        var rows = await attribute.GetData(method, new DisposalTracker());

        rows.Should().ContainSingle().Which.GetData().Should().ContainSingle().Which.Should().Be("base");
    }

    [Fact]
    public async Task Amendment2_c_private_nested_holder_runs_on_jit()
    {
        // Amendment 2(c): JIT reflection ignores the accessibility of a nested MemberType holder, so a
        // genuinely private nested holder (PrivateNestedHolder) still supplies rows. AOT cannot access
        // it (CMP0044) - this scenario is therefore JIT-only and has no AOT counterpart.
        var attribute = new ComposeDataAttribute(nameof(PrivateNestedHolder.Rows))
        {
            MemberType = typeof(PrivateNestedHolder),
        };

        var rows = await attribute.GetData(
            typeof(ParityPrivateNestedTarget).GetMethod(
                nameof(ParityPrivateNestedTarget.PrivateHolderTarget), BindingFlags.Public | BindingFlags.Instance)!,
            new DisposalTracker());

        rows.Should().ContainSingle().Which.GetData().Should().ContainSingle().Which.Should().Be("private");
    }

    // A genuinely private nested holder: only reachable through JIT reflection (Amendment 2(c)).
    private static class PrivateNestedHolder
    {
        public static IEnumerable<object?[]> Rows() => [new object?[] { "private" }];
    }
}

// -----------------------------------------------------------------------------------------------------
// Shared fixtures. Namespaces, type names, and member names are mirrored byte-for-byte in the AOT file.
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

    public void NullRowTarget(string value)
    {
    }

    public void UnsupportedShapeTarget(string value)
    {
    }

    public void TooManyTarget(int first, int second)
    {
    }

    public void NullValueTarget(string label)
    {
    }

    public void TypeMismatchTarget(string label)
    {
    }

    public void ConfigTarget(string value)
    {
    }

    public void ThrowingTarget(string value)
    {
    }

    public void CtorTarget(string value)
    {
    }

    public void Ctor1Target(string value)
    {
    }

    public void DiagnosticCtorTarget(string value)
    {
    }

    public void NullRowSeed42Target(string value)
    {
    }

    public void NullRowSeed0Target(string value)
    {
    }

    public void ProfileSeededTypeMismatchTarget(string label)
    {
    }

    public void NegativeSeedTarget(string value)
    {
    }

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
    public void HandoffTarget(string value)
    {
    }
}

public class ParityInheritedBase
{
    public static IEnumerable<object?[]> InheritedRows() => [new object?[] { "base" }];

    public void InheritedTarget(string value)
    {
    }

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

    public void InheritedFailureTarget(string value)
    {
    }
}

public sealed class ParityInheritedFailureDerived : ParityInheritedFailureBase
{
    public static new IEnumerable<object?[]> InheritedFailureRows() => [null!];
}

public sealed class ParityPrivateNestedTarget
{
    public void PrivateHolderTarget(string value)
    {
    }
}
