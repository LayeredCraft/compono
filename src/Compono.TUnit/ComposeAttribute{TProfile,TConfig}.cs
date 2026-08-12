using System.Diagnostics.CodeAnalysis;
using Compono.TUnit.Binding;

namespace Compono.TUnit;

/// <summary>
/// Composes a TUnit test method's parameters through Compono, applying a profile built from
/// <em>profile configuration arguments</em> known at this attribute's call site - a distinct concept
/// from this attribute family's ordinary inline values (<see cref="ComposeAttribute(object?[])"/>),
/// which bind to the test method's own parameters instead. This constructor never binds to the test
/// method's parameters at all; every one of them is composed in full. <typeparamref name="TConfig"/> is
/// constructed positionally from this attribute's own constructor arguments, then
/// <typeparamref name="TProfile"/> is constructed from that <typeparamref name="TConfig"/> instance and
/// applied via <see cref="CompositionBuilder.AddProfile(ICompositionProfile)"/> - equivalent to
/// <c>Composer.Create(builder =&gt; builder.AddProfile(new TProfile(new TConfig(...))))</c>. See
/// <c>docs/adr/0036-parameterized-composition-profile-selection.md</c> for the full design, including
/// why this exists as a separate attribute rather than overloading
/// <see cref="ComposeAttribute{TProfile}"/>'s own inline-value constructor argument. Mirrors
/// <c>Compono.XunitV3.ComposeAttribute{TProfile, TConfig}</c> exactly.
/// </summary>
/// <typeparam name="TProfile">
/// The profile to construct and apply. Must have exactly one public constructor accepting exactly one
/// <typeparamref name="TConfig"/>-typed parameter - no <c>new()</c> constraint, unlike
/// <see cref="ComposeAttribute{TProfile}"/>, since this form is never default-constructed.
/// </typeparam>
/// <typeparam name="TConfig">
/// The type this attribute's constructor arguments bind to, positionally, against its own single
/// public constructor. Prefer strongly-typed, attribute-legal values for its constructor parameters -
/// an <see langword="enum"/> for a finite choice, <see cref="Type"/> via <c>typeof(...)</c> for a CLR
/// type, a plain <see langword="bool"/>/numeric/string value where that already carries the real
/// meaning - over loosely-typed primitives standing in for something more specific.
/// <c>params object?[]</c> is a binding mechanism forced by C#'s attribute-argument-must-be-a-
/// compile-time-constant rule, not a license to design <typeparamref name="TConfig"/> around magic
/// strings.
/// </typeparam>
/// <remarks>
/// Unlike <see cref="ComposeAttribute{TProfile}"/>'s compile-time-enforced <c>new()</c> constraint, an
/// unsupported <typeparamref name="TConfig"/>/<typeparamref name="TProfile"/> constructor shape (not
/// exactly one public constructor on <typeparamref name="TConfig"/>; no exactly-one-
/// <typeparamref name="TConfig"/>-parameter public constructor on <typeparamref name="TProfile"/>) is a
/// deterministic runtime <see cref="CompositionException"/>, not a compile error - there is no C#
/// generic constraint that expresses "has a constructor accepting exactly this type." Both constructor
/// lookups, and the actual construction, are reflection (<see cref="ConfigProfileBinder"/>) - bounded
/// and cached to once per attribute instance by this attribute family's existing
/// <see cref="Lazy{T}"/>-backed <see cref="Composer"/> caching (<see cref="ApplyProfile"/> is only ever
/// invoked from inside that lazy initializer), never on the repeated per-row data-source path.
/// <typeparamref name="TProfile"/> and <typeparamref name="TConfig"/> both carry
/// <see cref="DynamicallyAccessedMembersAttribute"/>(<see cref="DynamicallyAccessedMemberTypes.PublicConstructors"/>)
/// - required, not decorative: a real Native AOT publish-and-run proof (ADR-0041 Amendment 1) showed
/// the trimmer strips a closed generic argument's public constructors by default, since nothing in an
/// unannotated <c>Type.GetConstructors()</c> call site tells it they're reachable - <c>ConfigProfileBinder</c>
/// failed at runtime with "has 0" public constructors on a type that plainly has one, until these
/// annotations were added at every generic parameter/<c>Type</c>-typed parameter along the call chain.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeAttribute<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProfile,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TConfig> : ComposeAttribute
    where TProfile : ICompositionProfile
{
    private readonly object?[] _configArguments;

    /// <summary>
    /// Creates a <see cref="ComposeAttribute{TProfile, TConfig}"/>.
    /// </summary>
    /// <param name="configArguments">
    /// Profile configuration arguments, bound positionally to <typeparamref name="TConfig"/>'s single
    /// public constructor - an entirely separate binding target from this attribute family's ordinary
    /// inline values; every test method parameter is composed in full regardless of what's supplied
    /// here. See the type-level remarks for why each argument should use the strongest attribute-legal
    /// type available rather than a bare string.
    /// </param>
    public ComposeAttribute(params object?[] configArguments) : base()
    {
        _configArguments = NormalizeParamsArguments(configArguments);
    }

    internal override void ApplyProfile(CompositionBuilder builder)
    {
        // A negative configured seed must be rejected before any config/profile binding is
        // attempted - otherwise Seed = -1 combined with an invalid TConfig/TProfile shape would
        // report the binder failure below with "Seed: -1" embedded instead of the documented
        // negative-seed diagnostic the base class's own ComposeRow enforces. SeedAsNullable is
        // exactly what CompositionRow.Seed would resolve to if non-negative (Composer.CreateRow's
        // unseeded fallback only ever generates a non-negative value), so checking it here - before
        // a CompositionRow even exists - gives the identical guarantee ComposeRow's own row.Seed < 0
        // check gives, just earlier.
        if (SeedAsNullable is { } configuredSeed && configuredSeed < 0)
        {
            throw new CompositionException(AppendSeed(
                $"Compono.TUnit requires a non-negative seed, but the configured seed was {configuredSeed}.",
                configuredSeed));
        }

        try
        {
            var config = ConfigProfileBinder.BindConfig(typeof(TConfig), _configArguments);
            var profile = ConfigProfileBinder.BuildProfile<TProfile, TConfig>(config);

            builder.AddProfile(profile);
        }
        catch (CompositionException exception)
        {
            // ApplyProfile runs while the base class's Lazy<Composer> is still being built - before
            // ComposeRow ever calls Composer.CreateRow, so no CompositionRow/row.Seed exists yet at
            // this point. Report the seed this attribute is actually configured with (SeedAsNullable,
            // already proven non-negative by the check above) - the same seed row.Seed would resolve
            // to once composition succeeds - or a freshly generated one otherwise. Reproducibility
            // isn't actually the point for this specific failure category (a constructor-shape/
            // argument mismatch fails identically regardless of seed); this is purely the "every
            // Compono.TUnit-owned failure ends with Seed: {value}" convention, applied consistently
            // rather than as an exception for this one binding path.
            var seed = SeedAsNullable ?? Random.Shared.Next(0, int.MaxValue);
            throw CompositionException.WithSeedInMessage(exception, seed);
        }
    }
}
