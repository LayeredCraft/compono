using System.Globalization;
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
    /// <summary>
    /// The trait key every returned row carries its seed under, unconditionally - see
    /// <see cref="GetData"/>'s Seed Policy and Reporting behavior (ADR-0022).
    /// </summary>
    internal const string SeedTraitName = "Compono.Seed";

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
    /// array length alone. A single <see langword="null"/> argument (e.g. <c>[Compose(null)]</c>) binds
    /// in the C# compiler's non-expanded <c>params</c> form - the whole array, not a one-element array
    /// containing <see langword="null"/> - so <paramref name="inlineValues"/> itself arrives
    /// <see langword="null"/> in exactly that case (PR #23 review); treated as a one-element array
    /// containing that <see langword="null"/> value, matching what the author's single-argument syntax
    /// actually means rather than surfacing the compiler's array-vs-element ambiguity as a thrown
    /// exception.
    /// </param>
    public ComposeAttribute(params object?[] inlineValues)
    {
        _inlineValues = inlineValues ?? [null];
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

    /// <summary>
    /// Composes (or applies inline values to) one theory row's parameters. See ADR-0022's
    /// "Inline/composed binding algorithm" section for the full step-by-step behavior this
    /// implements.
    /// </summary>
    /// <exception cref="CompositionException">
    /// This attribute's configured seed (or a profile-configured one) is negative; the test method's
    /// signature is unsupported (a generic method, a <c>ref</c>/<c>out</c>/<c>in</c>/<c>params</c>
    /// parameter, more than one Compose-family attribute, or more than one <c>[Shared]</c> parameter
    /// of the same type); too many inline values were supplied; a supplied inline value is
    /// <see langword="null"/> for a non-nullable parameter or has a type not assignable to its
    /// parameter; or composition itself fails for a parameter (propagated un-wrapped from the
    /// pipeline).
    /// </exception>
    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        ArgumentNullException.ThrowIfNull(testMethod);

        var composer = _composer.Value;
        var bindingPlan = EnsureBindingPlan(testMethod);

        // A test method is always declared on a type - there is no "global" test method shape.
        var row = composer.CreateRow(testMethod.DeclaringType!);

        // Read from row.Seed, not SeedAsNullable alone (PR #23 review Open Item: a TProfile.Configure
        // that itself calls builder.WithSeed(...) reaches the composer's configuration independently
        // of SeedAsNullable, which stays null whenever this attribute's own Seed property is unset).
        // row.Seed reflects whichever source actually configured the row's seed - this attribute's
        // property, a profile, or the auto-generated fallback - so checking it here is what makes
        // every row Compono.Xunit creates have a non-negative seed unconditionally, not just one
        // configured through this attribute's own property.
        if (row.Seed < 0)
        {
            throw new CompositionException(AppendSeed(
                $"Compono.Xunit requires a non-negative seed, but the configured seed was {row.Seed}.",
                row.Seed));
        }

        if (bindingPlan.SignatureError is not null)
            throw new CompositionException(AppendSeed(bindingPlan.SignatureError, row.Seed));

        var parameters = bindingPlan.Parameters;
        var methodDisplayName = BindingPlan.MethodDisplayName(testMethod);

        if (_inlineValues.Length > parameters.Count)
        {
            throw new CompositionException(AppendSeed(
                $"Too many inline values supplied to '{methodDisplayName}': {_inlineValues.Length} supplied, but the method has {parameters.Count} parameter(s).",
                row.Seed));
        }

        // Every supplied inline value is validated before any parameter is bound, shared, or
        // composed - an inline mismatch always fails as its own category, never surfacing indirectly
        // through a later ShareExplicit/Resolve call.
        for (var i = 0; i < _inlineValues.Length; i++)
        {
            var parameter = parameters[i];
            var value = _inlineValues[i];

            if (value is null)
            {
                if (parameter.Descriptor.Nullability != Nullability.Nullable)
                {
                    throw new CompositionException(AppendSeed(
                        $"Inline value for parameter '{parameter.Name}' on '{methodDisplayName}' is null, but the parameter is not nullable.",
                        row.Seed));
                }

                continue;
            }

            // A non-null Nullable<T> boxes as a boxed T, not a boxed Nullable<T> (a CLR
            // nullable-boxing rule) - unwrapping first is a no-op for a non-nullable parameter
            // (Nullable.GetUnderlyingType returns null, so ?? falls back to the declared type
            // unchanged) and is what makes e.g. [Compose(42)] valid for an int? parameter.
            var underlyingType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;

            if (!underlyingType.IsInstanceOfType(value))
            {
                throw new CompositionException(AppendSeed(
                    $"Inline value for parameter '{parameter.Name}' on '{methodDisplayName}' has type '{value.GetType()}', which is not assignable to '{parameter.ParameterType}'.",
                    row.Seed));
            }
        }

        var values = new object?[parameters.Count];

        // [Shared] parameters compose (or share their inline value) first, in declaration order among
        // themselves, regardless of where they sit among non-shared parameters - so a shared value is
        // always in scope before any parameter that might structurally depend on it composes.
        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];

            if (!parameter.IsShared)
                continue;

            if (i < _inlineValues.Length)
            {
                var value = _inlineValues[i];
                parameter.ShareExplicitInvoker(row, parameter.Descriptor, value);
                values[i] = value;
            }
            else
            {
                // Registered as soon as it's composed, not after the whole row is assembled - a
                // later parameter that throws (composition failure, or the too-many-inline/inline-
                // mismatch checks above) must not leak a disposable value this row already produced.
                // DisposalTracker.Add no-ops for a value that isn't IDisposable/IAsyncDisposable, so
                // this is safe to call unconditionally for every composed value.
                var value = parameter.ResolveSharedInvoker(row, parameter.Descriptor);
                disposalTracker.Add(value);
                values[i] = value;
            }
        }

        // Every remaining (non-inline, non-shared) parameter composes next, in declaration order.
        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];

            if (parameter.IsShared)
                continue;

            if (i < _inlineValues.Length)
            {
                values[i] = _inlineValues[i];
                continue;
            }

            var value = parameter.ResolveInvoker(row, parameter.Descriptor);
            disposalTracker.Add(value);
            values[i] = value;
        }

        // Assembled in method declaration order - binding order (shared first, then the rest) and
        // output order are intentionally different.
        var dataRow = new TheoryDataRow(values);

        // Set unconditionally, on every row regardless of whether it will pass or fail - GetData
        // cannot know at this point whether the theory body will later throw its own assertion
        // failure, so the trait can't be applied only-on-failure (ADR-0022's Seed Policy and
        // Reporting).
        dataRow.Traits[SeedTraitName] = [row.Seed.ToString(CultureInfo.InvariantCulture)];

        return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>([dataRow]);
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

    // The "\n\nSeed: {value}" convention every Compono.Xunit-authored pre-composition failure uses,
    // matching the same trailing text a propagated pipeline CompositionDiagnostic already renders
    // (ADR-0022's Seed Policy and Reporting) - so every failure category ends the same way, whether
    // Compono.Xunit constructed the message or the pipeline did.
    private static string AppendSeed(string message, int seed) => $"{message}\n\nSeed: {seed}";
}
