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
    private readonly string _locale;

    // Immutable, built once - a FrozenDictionary, not a plain Dictionary, since this is a fixed,
    // read-only lookup for the lifetime of this provider instance. Exact match, case-sensitive,
    // against CompositionProviderRequest.Name only - no substring/prefix/fuzzy matching, and no
    // attempt at pluralization or synonym handling. "Name" itself (ambiguous per docs/mvp.md's own
    // callout) is deliberately absent from the built-in allowlist this always contains at minimum.
    private readonly FrozenDictionary<string, Func<Faker, string>> _conventions;

    // One Faker per thread, reused only for built-in/alias conventions (ADR-0027's Amendment) -
    // never for a custom AddConvention delegate, which gets its own single-use Faker instead (see
    // TryProvide). Built-in conventions are Compono-authored and never touch Faker state beyond
    // Random, so reuse is safe; an arbitrary custom delegate could reassign DateTimeReference, a
    // sub-generator (Faker.Name, Faker.Address, ...), or any of Faker's other public settable
    // properties, and that mutation would otherwise leak into every later request on the same
    // thread if it shared this instance. Never shared across threads either - a thread-local
    // instance is never touched by more than one thread, which is what makes reuse safe under
    // concurrent composition in the first place.
    private readonly ThreadLocal<Faker> _threadFaker;

    /// <summary>Creates a <see cref="BogusMemberNameProvider"/> using <paramref name="locale"/>.</summary>
    /// <param name="locale">The Bogus locale each handled request's <see cref="Faker"/> uses.</param>
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
        _locale = locale;
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

        // Built-in/alias conventions reuse this thread's cached Faker (fast path); an arbitrary
        // custom AddConvention delegate gets a fresh, single-use Faker instead, since we can't
        // guarantee it won't mutate state that would otherwise leak into a later, unrelated
        // request on the same thread - see _threadFaker's declaration comment and ADR-0027's
        // Amendment. ThreadLocal<T>.Value is annotated T? in the BCL, but _threadFaker was
        // constructed with a valueFactory (never the parameterless constructor), so every access -
        // first or subsequent, on any thread - is guaranteed non-null by ThreadLocal<T>'s own
        // contract.
        var faker = BogusConventions.IsBuiltIn(generate) ? _threadFaker.Value! : new Faker(_locale);
        faker.Random = new Randomizer(context.DeriveSeed());
        return CompositionProviderResult.Handled(generate(faker));
    }
}
