using Xunit.v3;

namespace Compono.XunitV3.Aot;

/// <summary>
/// Composes an xUnit v3 theory row's parameters through Compono under xUnit v3's Native AOT pipeline,
/// applying a profile built from <em>profile configuration arguments</em> known at this attribute's
/// call site - the AOT counterpart to
/// <c>Compono.XunitV3.ComposeAttribute&lt;TProfile, TConfig&gt;</c>. Usage is identical:
/// <code>
/// [Theory]
/// [Compose&lt;MyProfile, MyConfig&gt;(MyConfigValue.Foo)]
/// public void My_test(Widget widget) { ... }
/// </code>
/// </summary>
/// <typeparam name="TProfile">
/// The profile to construct and apply. Must have exactly one public constructor accepting exactly one
/// <typeparamref name="TConfig"/>-typed parameter - no <c>new()</c> constraint, matching
/// <c>Compono.XunitV3.ComposeAttribute&lt;TProfile, TConfig&gt;</c>'s own type-level remarks.
/// </typeparam>
/// <typeparam name="TConfig">
/// The type this attribute's constructor arguments bind to, positionally, against its own single
/// public constructor.
/// </typeparam>
/// <remarks>
/// Marker-only, like the non-generic <see cref="ComposeAttribute"/> - discovered by
/// <c>Compono.Generators</c> at compile time (matched on this type's own arity-suffixed fully
/// qualified metadata name, <c>Compono.XunitV3.Aot.ComposeAttribute`2</c>, per ADR-0067/PLAN-0067) and
/// never invoked at runtime. Unlike
/// <c>Compono.XunitV3.ComposeAttribute&lt;TProfile, TConfig&gt;</c>, the shape checks that attribute
/// performs at runtime via <c>ConfigProfileBinder</c> reflection (<typeparamref name="TConfig"/> has
/// exactly one public constructor; <typeparamref name="TProfile"/> has exactly one public constructor
/// accepting exactly one <typeparamref name="TConfig"/>-typed parameter; the supplied constructor
/// arguments match that constructor's parameters) are performed by <c>Compono.Generators</c> at
/// <em>compile time</em> instead, against the real declared symbols and this attribute's own
/// compile-time-constant constructor arguments (C#'s attribute-argument rule guarantees they're
/// constants) - see <c>CMP0041</c>/<c>CMP0042</c>/<c>CMP0043</c>, plus further shape/safety
/// restrictions found by implementation review (<c>CMP0044</c>-<c>CMP0048</c> - accessibility, stacked
/// attributes, required members, compiler-error-on-use attributes, and overload-resolution-priority
/// hijacking; see ADR-0067 Amendment 2). On success, the generated
/// registration constructs <typeparamref name="TConfig"/> and <typeparamref name="TProfile"/> via
/// direct <c>new</c> calls with the literal argument values rendered back into source - no
/// <see cref="Type.GetConstructors()"/>, no <see cref="System.Reflection.ConstructorInfo.Invoke(object?[])"/>,
/// no <see cref="System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute"/> annotation
/// anywhere in this path (ADR-0067).
/// <para>
/// This constructor arguments never bind to the test method's own parameters - every parameter is
/// composed in full, identical to <c>Compono.XunitV3.ComposeAttribute&lt;TProfile, TConfig&gt;</c>'s
/// own binding contract.
/// </para>
/// <para>
/// Derives directly from <see cref="DataAttribute"/>, <b>not</b> from the non-generic
/// <see cref="ComposeAttribute"/> - see <see cref="ComposeAttribute{TProfile}"/>'s identical remarks
/// for why (ADR-0067 Amendment 1).
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeAttribute<TProfile, TConfig> : DataAttribute
    where TProfile : ICompositionProfile
{
    /// <summary>
    /// Creates a <see cref="ComposeAttribute{TProfile, TConfig}"/>.
    /// </summary>
    /// <param name="configArguments">
    /// Profile configuration arguments, bound positionally to <typeparamref name="TConfig"/>'s single
    /// public constructor at compile time by <c>Compono.Generators</c> - never read by this attribute
    /// itself at runtime (it carries no <c>GetData</c> override; xUnit's AOT pipeline never invokes
    /// it).
    /// </param>
    public ComposeAttribute(params object?[] configArguments)
    {
    }
}
