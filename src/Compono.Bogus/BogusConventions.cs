using System.Collections.Frozen;
using Bogus;

namespace Compono;

/// <summary>
/// The shared, built-in source of truth <see cref="BogusOptions.AddAlias"/>/<see cref="BogusOptions.AddConvention"/>
/// validate against, and <c>CompositionBuilderExtensions.UseBogus()</c> merges into
/// <see cref="BogusMemberNameProvider"/>'s own lookup. See
/// <c>docs/adr/0028-configurable-bogus-member-name-conventions.md</c>.
/// </summary>
internal static class BogusConventions
{
    // One canonical (name, convention, generator) list - ByName/ByConvention below are both derived
    // from it, so the ten generator delegates are never written out twice.
    private static readonly (string Name, BogusConvention Convention, Func<Faker, string> Generate)[] Entries =
    [
        ("FirstName", BogusConvention.FirstName, f => f.Name.FirstName()),
        ("LastName", BogusConvention.LastName, f => f.Name.LastName()),
        ("FullName", BogusConvention.FullName, f => f.Name.FullName()),
        ("Email", BogusConvention.Email, f => f.Internet.Email()),
        ("PhoneNumber", BogusConvention.PhoneNumber, f => f.Phone.PhoneNumber()),
        ("StreetAddress", BogusConvention.StreetAddress, f => f.Address.StreetAddress()),
        ("City", BogusConvention.City, f => f.Address.City()),
        ("State", BogusConvention.State, f => f.Address.State()),
        ("PostalCode", BogusConvention.PostalCode, f => f.Address.ZipCode()),
        ("CompanyName", BogusConvention.CompanyName, f => f.Company.CompanyName()),
    ];

    // Concrete FrozenDictionary as the private backing field, per coding-standards.md's
    // collection-surface rule (applies to internal surfaces too, not just public ones) - the
    // internal-facing members below expose IReadOnlyDictionary, never the concrete type itself.
    private static readonly FrozenDictionary<string, Func<Faker, string>> ByNameCore =
        Entries.ToFrozenDictionary(entry => entry.Name, entry => entry.Generate);

    private static readonly FrozenDictionary<BogusConvention, Func<Faker, string>> ByConventionCore =
        Entries.ToFrozenDictionary(entry => entry.Convention, entry => entry.Generate);

    // Reference-equality set of the ten built-in delegate instances above - AddAlias resolves to
    // one of these exact delegates (BogusConventions.ByConvention[target]), so an alias counts as
    // "built-in" here too, correctly, without needing its own separate tracking. Explicit
    // ReferenceEqualityComparer.Instance, not the default comparer FrozenSet<T> would otherwise
    // use - Delegate.Equals compares Method+Target, not object identity, so two independently
    // written (even textually-identical) lambdas are never equal by it, but relying on that
    // distinction rather than requiring literal reference identity would be the wrong contract
    // for a trust boundary this load-bearing: the only delegates that should ever count as
    // "built-in" are the exact ten instances this class itself constructed, never a
    // coincidentally-equivalent one from anywhere else.
    private static readonly FrozenSet<Func<Faker, string>> BuiltInDelegates =
        Entries.Select(static entry => entry.Generate).ToFrozenSet<Func<Faker, string>>(ReferenceEqualityComparer.Instance);

    /// <summary>Built-in name -&gt; generator, for collision checks and the default lookup.</summary>
    internal static IReadOnlyDictionary<string, Func<Faker, string>> ByName => ByNameCore;

    /// <summary>Built-in convention -&gt; generator, for resolving an alias's target.</summary>
    internal static IReadOnlyDictionary<BogusConvention, Func<Faker, string>> ByConvention => ByConventionCore;

    /// <summary>
    /// Whether <paramref name="generate"/> is one of this package's own built-in convention
    /// delegates (including one reached via an alias) - <see cref="BogusMemberNameProvider"/>
    /// uses this to decide whether a request can safely reuse its per-thread <c>Faker</c> (a
    /// built-in delegate never mutates <c>Faker</c> state beyond <c>Random</c>) or needs its own
    /// single-use instance (an arbitrary <c>AddConvention</c> delegate might).
    /// </summary>
    internal static bool IsBuiltIn(Func<Faker, string> generate) => BuiltInDelegates.Contains(generate);
}
