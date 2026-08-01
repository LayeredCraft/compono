using Bogus;

namespace Compono;

/// <summary>
/// Configuration for <see cref="BogusMemberNameProvider"/>, set via
/// <c>CompositionBuilderExtensions.UseBogus(Action{BogusOptions})</c>. See
/// <c>docs/adr/0027-compono-bogus-package-design.md</c> and
/// <c>docs/adr/0028-configurable-bogus-member-name-conventions.md</c>.
/// </summary>
public sealed class BogusOptions
{
    // Eager, Dictionary.Add-style accumulator (ADR-0028) - AddAlias/AddConvention validate and
    // record into this immediately, rather than deferring validation to when UseBogus(...) reads it.
    // Default string comparer (ordinal, case-sensitive) - never a case-insensitive one, matching this
    // package's exact-match requirement throughout.
    private readonly Dictionary<string, Func<Faker, string>> _customConventions = new();

    /// <summary>
    /// The Bogus locale used by the package-wide member-name convention provider
    /// (<see cref="BogusMemberNameProvider"/>) only. <c>UseBogus&lt;T&gt;()</c> is independent of this
    /// option and does not read it - it defaults to <c>"en"</c> on its own, or takes an explicit
    /// <c>locale</c> parameter. Defaults to Bogus's own default (<c>"en"</c>).
    /// </summary>
    public string Locale { get; set; } = "en";

    /// <summary>
    /// Whether the conservative member-name convention provider is active. Defaults to
    /// <see langword="true"/>. Setting this to <see langword="false"/> means
    /// <see cref="BogusMemberNameProvider"/> is not registered at all - including any
    /// <see cref="AddAlias"/>/<see cref="AddConvention"/> entries configured in the same call; there
    /// is no partial mode that disables only the built-in conventions.
    /// </summary>
    public bool EnableMemberNameConventions { get; set; } = true;

    /// <summary>
    /// Adds an additional exact member name that resolves to the same generator as a built-in
    /// <see cref="BogusConvention"/> - e.g. <c>AddAlias("GivenName", BogusConvention.FirstName)</c>
    /// lets a domain that calls first names "GivenName" still get realistic values from
    /// <c>UseBogus()</c> alone. Validated and applied eagerly, immediately, against this
    /// <see cref="BogusOptions"/> instance's own accumulated state - not deferred to
    /// <c>UseBogus(...)</c> returning, and not detected across separate <c>UseBogus(...)</c> calls.
    /// </summary>
    /// <param name="aliasName">The additional exact member name to match.</param>
    /// <param name="target">The built-in convention this alias's matched requests should generate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="aliasName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="aliasName"/> is empty or all-whitespace; or already configured as a built-in
    /// convention name, an existing alias, or an existing custom convention.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="target"/> is not a defined <see cref="BogusConvention"/> value.</exception>
    public void AddAlias(string aliasName, BogusConvention target)
    {
        if (!Enum.IsDefined(target))
        {
            throw new ArgumentOutOfRangeException(
                nameof(target), target, $"'{target}' is not a defined {nameof(BogusConvention)} value.");
        }

        AddCore(aliasName, nameof(aliasName), BogusConventions.ByConvention[target]);
    }

    /// <summary>
    /// Adds a custom exact-name convention: a member named exactly <paramref name="memberName"/>
    /// resolves to <paramref name="generate"/>'s result, called against a request-local,
    /// <c>context.DeriveSeed()</c>-seeded <see cref="Faker"/> - the same determinism contract every
    /// other value in this package follows. Validated and applied eagerly, immediately, against this
    /// <see cref="BogusOptions"/> instance's own accumulated state - not deferred to
    /// <c>UseBogus(...)</c> returning, and not detected across separate <c>UseBogus(...)</c> calls.
    /// </summary>
    /// <param name="memberName">The exact member name to match.</param>
    /// <param name="generate">Produces this member's value from a seeded <see cref="Faker"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="memberName"/> or <paramref name="generate"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="memberName"/> is empty or all-whitespace; or already configured as a built-in
    /// convention name, an existing alias, or an existing custom convention.
    /// </exception>
    public void AddConvention(string memberName, Func<Faker, string> generate)
    {
        ArgumentNullException.ThrowIfNull(generate);

        AddCore(memberName, nameof(memberName), generate);
    }

    /// <summary>Reads this instance's accumulated aliases/custom conventions - <c>internal</c>, for <c>CompositionBuilderExtensions.UseBogus</c> to merge with <see cref="BogusConventions.ByName"/>.</summary>
    internal IReadOnlyDictionary<string, Func<Faker, string>> CustomConventions => _customConventions;

    // Shared by AddAlias/AddConvention - both reduce to "record this name -> generate mapping,
    // eagerly validated the same way." Dictionary.Add-style: the exact call that introduces a
    // problem throws immediately, naming it, rather than deferring to a later batch report.
    private void AddCore(string name, string paramName, Func<Faker, string> generate)
    {
        ArgumentNullException.ThrowIfNull(name, paramName);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A member name cannot be empty or all-whitespace.", paramName);

        if (BogusConventions.ByName.ContainsKey(name))
        {
            throw new ArgumentException(
                $"A Bogus convention for member name '{name}' is already registered as a built-in " +
                "convention and cannot be added again.",
                paramName);
        }

        if (!_customConventions.TryAdd(name, generate))
        {
            throw new ArgumentException(
                $"A Bogus convention for member name '{name}' has already been configured in this " +
                "UseBogus(...) call and cannot be added again.",
                paramName);
        }
    }
}
