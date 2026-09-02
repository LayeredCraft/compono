using System.Diagnostics.CodeAnalysis;
using Compono.MSTest.Binding;

namespace Compono.MSTest;

/// <summary>
/// Composes an MSTest data-driven test method's parameters through Compono, applying a profile
/// built from <em>profile configuration arguments</em> known at this attribute's call site - a
/// distinct concept from this attribute family's ordinary inline values
/// (<see cref="ComposeAttribute(object?[])"/>), which bind to the test method's own parameters
/// instead. This constructor never binds to the test method's parameters at all; every one of them
/// is composed in full. <typeparamref name="TConfig"/> is constructed positionally from this
/// attribute's own constructor arguments, then <typeparamref name="TProfile"/> is constructed from
/// that <typeparamref name="TConfig"/> instance and applied via
/// <see cref="CompositionBuilder.AddProfile(ICompositionProfile)"/> - the same Compono-facing
/// attribute family and semantics as <c>Compono.XunitV3.ComposeAttribute{TProfile,TConfig}</c>/
/// <c>Compono.TUnit</c>'s own equivalent overload (ADR-0036); no MSTest-specific profile/
/// configuration shape is introduced.
/// </summary>
/// <typeparam name="TProfile">
/// The profile to construct and apply. Must have exactly one public constructor accepting exactly
/// one <typeparamref name="TConfig"/>-typed parameter - no <c>new()</c> constraint, unlike
/// <see cref="ComposeAttribute{TProfile}"/>, since this form is never default-constructed.
/// </typeparam>
/// <typeparam name="TConfig">
/// The type this attribute's constructor arguments bind to, positionally, against its own single
/// public constructor. Prefer strongly-typed, attribute-legal values for its constructor
/// parameters over loosely-typed primitives standing in for something more specific.
/// </typeparam>
/// <remarks>
/// Unlike <see cref="ComposeAttribute{TProfile}"/>'s compile-time-enforced <c>new()</c> constraint,
/// an unsupported <typeparamref name="TConfig"/>/<typeparamref name="TProfile"/> constructor shape
/// is a deterministic runtime <see cref="CompositionException"/>, not a compile error. Both
/// constructor lookups, and the actual construction, are reflection (<see cref="ConfigProfileBinder"/>)
/// - bounded and cached to once per attribute instance by this attribute family's existing
/// <see cref="Lazy{T}"/>-backed <see cref="Composer"/> caching (<see cref="ApplyProfile"/> is only
/// ever invoked from inside that lazy initializer), never on the repeated per-row <c>GetData</c> path.
/// </remarks>
/// <remarks>
/// <typeparamref name="TProfile"/> and <typeparamref name="TConfig"/> both carry
/// <see cref="DynamicallyAccessedMembersAttribute"/>(<see cref="DynamicallyAccessedMemberTypes.PublicConstructors"/>)
/// - required, not decorative: a real Native AOT publish-and-run proof showed the trimmer strips a
/// closed generic argument's public constructors by default, since nothing in an unannotated
/// <see cref="Type.GetConstructors()"/> call site tells it they're reachable - <see cref="ConfigProfileBinder"/>
/// failed at runtime with "has 0" public constructors on a type that plainly has one, until these
/// annotations were added at every generic parameter/<see cref="Type"/>-typed parameter along the
/// call chain, mirroring the identical fix <c>Compono.TUnit</c>'s own AOT smoke test already found
/// and applied for this same binder shape.
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
    /// Profile configuration arguments, bound positionally to <typeparamref name="TConfig"/>'s
    /// single public constructor - an entirely separate binding target from this attribute
    /// family's ordinary inline values; every test method parameter is composed in full
    /// regardless of what's supplied here.
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
        // negative-seed diagnostic the base class's own GetData enforces. SeedAsNullable is
        // exactly what CompositionRow.Seed would resolve to if non-negative, so checking it here -
        // before a CompositionRow even exists - gives the identical guarantee GetData's own
        // row.Seed < 0 check gives, just earlier. Matches Compono.XunitV3's identical ordering
        // (PR #65 review).
        if (SeedAsNullable is { } configuredSeed && configuredSeed < 0)
        {
            throw new CompositionException(AppendSeed(
                $"Compono.MSTest requires a non-negative seed, but the configured seed was {configuredSeed}.",
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
            var seed = SeedAsNullable ?? Random.Shared.Next(0, int.MaxValue);
            throw CompositionException.WithSeedInMessage(exception, seed);
        }
    }
}
