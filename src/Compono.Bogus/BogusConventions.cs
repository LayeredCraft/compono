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

    /// <summary>Built-in name -&gt; generator, for collision checks and the default lookup.</summary>
    internal static IReadOnlyDictionary<string, Func<Faker, string>> ByName => ByNameCore;

    /// <summary>Built-in convention -&gt; generator, for resolving an alias's target.</summary>
    internal static IReadOnlyDictionary<BogusConvention, Func<Faker, string>> ByConvention => ByConventionCore;
}
