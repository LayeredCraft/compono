using System.Reflection;
using Compono.Xunit.Binding;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Compono.Xunit;

/// <summary>
/// Composes an xUnit v3 theory row's parameters through Compono - the default (no explicit profile)
/// entry point. Every parameter not supplied inline is composed; a parameter targeted by a supplied
/// inline value takes that value instead, taking precedence over composition. See
/// <c>docs/adr/0022-compono-xunit-package-design.md</c> for the full binding algorithm, seed policy,
/// and diagnostics.
/// </summary>
/// <remarks>
/// Deliberately unsealed - <see cref="ComposeAttribute{TProfile}"/> is the one designed extension
/// point, mirroring <see cref="CompositionBuilder.AddProfile{TProfile}"/>'s own
/// <c>TProfile : ICompositionProfile, new()</c> constraint. <see cref="SupportsDiscoveryEnumeration"/>
/// returns <see langword="false"/>: composition is deferred entirely to execution time, so
/// <see cref="GetData"/> runs for real exactly once per test execution - there is no separate
/// discovery-time composition pass to keep synchronized with it.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ComposeAttribute : DataAttribute
{
    private readonly object?[] _inlineValues;
    private readonly Lazy<Composer> _composer;
    private int? _seed;
    private BindingPlan? _bindingPlan;
    private object? _bindingPlanLock;

    /// <summary>
    /// Creates a <see cref="ComposeAttribute"/>.
    /// </summary>
    /// <param name="inlineValues">
    /// Values supplied positionally, left-to-right from the test method's first parameter - every
    /// parameter at an index beyond this array's length is composed instead. An explicit
    /// <see langword="null"/> entry is a supplied value, not "not supplied": presence is determined by
    /// array length alone.
    /// </param>
    public ComposeAttribute(params object?[] inlineValues)
    {
        ArgumentNullException.ThrowIfNull(inlineValues);

        _inlineValues = inlineValues;
        _composer = new Lazy<Composer>(BuildComposer);
    }

    /// <summary>
    /// An explicit root seed for this row - the same underlying contract as
    /// <see cref="CompositionBuilder.WithSeed"/>, but restricted to non-negative values (enforced by
    /// Phase 2's binding algorithm, not here) so a seed reported in a failure message is always
    /// pasteable back into this property unchanged. Unset: a fresh, non-negative seed is generated on
    /// every <see cref="GetData"/> call. A plain <see langword="int"/>, not <see langword="int?"/> -
    /// an attribute named argument cannot target a <see cref="Nullable{T}"/> property (CS0655); see
    /// <see cref="SeedAsNullable"/> for the property the binding algorithm actually reads.
    /// </summary>
    public int Seed
    {
        get => _seed ?? default;
        set => _seed = value;
    }

    /// <summary>
    /// The inline values supplied to this attribute's constructor, in positional order - read by
    /// Phase 2's binding algorithm, not this phase's own <see cref="GetData"/> stub.
    /// </summary>
    internal IReadOnlyList<object?> InlineValues => _inlineValues;

    /// <summary>
    /// The value actually assigned to <see cref="Seed"/>, or <see langword="null"/> if it was never
    /// set - distinguishes "configured to 0" from "never configured," which <see cref="Seed"/> alone
    /// cannot (its getter falls back to <see langword="default"/>, i.e. <c>0</c>, when unset).
    /// </summary>
    internal int? SeedAsNullable => _seed;

    /// <inheritdoc />
    public override bool SupportsDiscoveryEnumeration() => false;

    /// <inheritdoc />
    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        ArgumentNullException.ThrowIfNull(testMethod);

        // Exercises this attribute instance's caching (Composer + binding plan, this phase's own
        // scope) even though the binding algorithm itself - creating the row, validating the seed,
        // binding inline/shared/composed parameters, and returning the assembled row - is Milestone 4
        // Phase 2. Reading _composer.Value here as well ensures both caches this attribute owns are
        // exercised identically on every call, matching how Phase 2 will actually use them.
        _ = _composer.Value;
        EnsureBindingPlan(testMethod);

        throw new NotImplementedException(
            "Compono.Xunit's inline/composed binding algorithm is implemented in Milestone 4 Phase 2.");
    }

    /// <summary>
    /// Applies this attribute's profile selection to <paramref name="builder"/> - a no-op for the
    /// non-generic <see cref="ComposeAttribute"/> (no profile), overridden by
    /// <see cref="ComposeAttribute{TProfile}"/> to add its <c>TProfile</c>.
    /// </summary>
    internal virtual void ApplyProfile(CompositionBuilder builder)
    {
    }

    // Internal test seam - lets Compono.Xunit.Tests assert the same BindingPlan instance (and the
    // same per-parameter invoker delegates on it) is returned across repeated calls with the same
    // testMethod, proving MakeGenericMethod ran exactly once per parameter, not once per GetData call.
    internal BindingPlan EnsureBindingPlan(MethodInfo testMethod) =>
        LazyInitializer.EnsureInitialized(ref _bindingPlan, ref _bindingPlanLock, () => BindingPlan.Build(testMethod))!;

    // Internal test seam - lets Compono.Xunit.Tests assert the same Composer instance is reused
    // across repeated GetData calls on one attribute instance.
    internal Composer GetComposer() => _composer.Value;

    private Composer BuildComposer() => Composer.Create(builder =>
    {
        ApplyProfile(builder);

        if (SeedAsNullable is { } seed)
            builder.WithSeed(seed);
    });
}
