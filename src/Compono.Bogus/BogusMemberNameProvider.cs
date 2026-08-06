using System.Collections.Frozen;
using Bogus;

namespace Compono;

/// <summary>
/// A stage-5 semantic value provider that matches an exact, conservative allowlist of
/// <c>string</c>-typed member names (<c>FirstName</c>, <c>Email</c>, etc.) against a real,
/// deterministically-seeded <see cref="Faker"/> value. Registered via
/// <c>CompositionBuilderExtensions.UseBogus()</c>. See
/// <c>docs/adr/0027-compono-bogus-package-design.md</c>.
/// </summary>
public sealed class BogusMemberNameProvider : ICompositionValueProvider
{
    // Immutable, built once - a FrozenDictionary, not a plain Dictionary, since this is a fixed,
    // read-only lookup for the lifetime of this provider instance. Exact match, case-sensitive,
    // against CompositionProviderRequest.Name only - no substring/prefix/fuzzy matching, and no
    // attempt at pluralization or synonym handling. "Name" itself (ambiguous per docs/mvp.md's own
    // callout) is deliberately absent from the built-in allowlist this always contains at minimum.
    private readonly FrozenDictionary<string, Func<Faker, string>> _conventions;

    // One Faker per thread, reused across every request that thread handles, reseeded immediately
    // before each use - never a request-lifetime instance (constructing Faker's full set of
    // category generators is genuinely expensive, per ADR-0034's benchmark suite), and never a
    // single instance shared across threads (the exact hazard ADR-0027's Amendment distinguishes
    // this from - see that Amendment for why per-thread reuse doesn't reintroduce the
    // shared-mutable-state race a package-lifetime-shared Faker would).
    private readonly ThreadLocal<Faker> _threadFaker;

    /// <summary>Creates a <see cref="BogusMemberNameProvider"/> using <paramref name="locale"/>.</summary>
    /// <param name="locale">The Bogus locale each handled request's own <see cref="Faker"/> uses.</param>
    public BogusMemberNameProvider(string locale)
        : this(locale, BogusConventions.ByName)
    {
    }

    // New, internal only (ADR-0028) - the merged-conventions path CompositionBuilderExtensions.UseBogus
    // uses. Deliberately not public: a public overload would let a caller construct the provider with
    // an arbitrary dictionary that omits or remaps a built-in name, silently supporting the
    // replace/remove-a-built-in capability ADR-0028 declares a Non-Goal, and bypassing
    // BogusOptions.AddAlias/AddConvention's own eager validation entirely. Keeping this overload
    // internal means the only way to reach it is through UseBogus(...), which always starts from
    // BogusConventions.ByName and only ever adds to it via the validated AddAlias/AddConvention path.
    internal BogusMemberNameProvider(string locale, IReadOnlyDictionary<string, Func<Faker, string>> conventions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        ArgumentNullException.ThrowIfNull(conventions);
        _conventions = conventions as FrozenDictionary<string, Func<Faker, string>>
            ?? conventions.ToFrozenDictionary();
        // trackAllValues: false - nothing here ever enumerates ThreadLocal<T>.Values across
        // threads, so there's no reason to pay for that bookkeeping.
        _threadFaker = new ThreadLocal<Faker>(() => new Faker(locale), trackAllValues: false);
    }

    /// <inheritdoc />
    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
    {
        // Type-gated to string only - every convention (built-in, alias, or custom) produces a
        // string, so this provider can never claim an interface, delegate, or abstract-class
        // request. That's what makes coexistence with Compono.NSubstitute's stage-6 provider
        // automatic: the two providers claim disjoint request shapes by construction, with no
        // coordination needed.
        if (request.RequestedType != typeof(string) || request.Name is not { } name)
            return CompositionProviderResult.NotHandled;

        if (!_conventions.TryGetValue(name, out var generate))
            return CompositionProviderResult.NotHandled;

        // This thread's own Faker, reseeded from context.DeriveSeed() before every use - see
        // _threadFaker's declaration comment and ADR-0027's Amendment for why reuse is safe here.
        // ThreadLocal<T>.Value is annotated T? in the BCL, but _threadFaker was constructed with a
        // valueFactory (never the parameterless constructor), so every access - first or
        // subsequent, on any thread - is guaranteed non-null by ThreadLocal<T>'s own contract.
        var faker = _threadFaker.Value!;
        faker.Random = new Randomizer(context.DeriveSeed());
        return CompositionProviderResult.Handled(generate(faker));
    }
}
