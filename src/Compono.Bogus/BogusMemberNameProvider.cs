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
    // read-only lookup table shared across every request this provider ever handles. Exact match,
    // case-sensitive, against CompositionProviderRequest.Name only - no substring/prefix/fuzzy
    // matching, and no attempt at pluralization or synonym handling. "Name" itself (ambiguous per
    // docs/mvp.md's own callout) is deliberately absent from this allowlist.
    private static readonly FrozenDictionary<string, Func<Faker, string>> Conventions =
        new Dictionary<string, Func<Faker, string>>
        {
            ["FirstName"] = f => f.Name.FirstName(),
            ["LastName"] = f => f.Name.LastName(),
            ["FullName"] = f => f.Name.FullName(),
            ["Email"] = f => f.Internet.Email(),
            ["PhoneNumber"] = f => f.Phone.PhoneNumber(),
            ["StreetAddress"] = f => f.Address.StreetAddress(),
            ["City"] = f => f.Address.City(),
            ["State"] = f => f.Address.State(),
            ["PostalCode"] = f => f.Address.ZipCode(),
            ["CompanyName"] = f => f.Company.CompanyName(),
        }.ToFrozenDictionary();

    private readonly string _locale;

    /// <summary>Creates a <see cref="BogusMemberNameProvider"/> using <paramref name="locale"/>.</summary>
    /// <param name="locale">The Bogus locale each handled request's own <see cref="Faker"/> uses.</param>
    public BogusMemberNameProvider(string locale)
    {
        ArgumentNullException.ThrowIfNull(locale);
        _locale = locale;
    }

    /// <inheritdoc />
    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
    {
        // Type-gated to string only - every convention in the allowlist above produces a string, so
        // this provider can never claim an interface, delegate, or abstract-class request. That's
        // what makes coexistence with Compono.NSubstitute's stage-6 provider automatic: the two
        // providers claim disjoint request shapes by construction, with no coordination needed.
        if (request.RequestedType != typeof(string) || request.Name is not { } name)
            return CompositionProviderResult.NotHandled;

        if (!Conventions.TryGetValue(name, out var generate))
            return CompositionProviderResult.NotHandled;

        // A fresh Faker/Randomizer per handled request, seeded from context.DeriveSeed() - never a
        // package-lifetime-shared instance. This is what keeps every produced value both
        // deterministic (same seed, same request path -> same value) and safe under concurrent
        // composition (no instance is ever touched from more than one request).
        var faker = new Faker(_locale) { Random = new Randomizer(context.DeriveSeed()) };
        return CompositionProviderResult.Handled(generate(faker));
    }
}
