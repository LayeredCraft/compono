namespace Compono;

/// <summary>
/// One of Compono.Bogus's fixed set of built-in, conservative member-name conventions - see
/// <c>docs/adr/0027-compono-bogus-package-design.md</c>'s Model 1. Deliberately not extensible: a new
/// built-in convention requires a new enum member, a generator mapping, documentation, and tests, not
/// a value a consumer can define themselves - custom behavior belongs in
/// <see cref="BogusOptions.AddConvention"/>, not in this enum. See
/// <c>docs/adr/0028-configurable-bogus-member-name-conventions.md</c>.
/// </summary>
public enum BogusConvention
{
    /// <summary>Maps to <c>faker.Name.FirstName()</c>.</summary>
    FirstName,

    /// <summary>Maps to <c>faker.Name.LastName()</c>.</summary>
    LastName,

    /// <summary>Maps to <c>faker.Name.FullName()</c>.</summary>
    FullName,

    /// <summary>Maps to <c>faker.Internet.Email()</c>.</summary>
    Email,

    /// <summary>Maps to <c>faker.Phone.PhoneNumber()</c>.</summary>
    PhoneNumber,

    /// <summary>Maps to <c>faker.Address.StreetAddress()</c>.</summary>
    StreetAddress,

    /// <summary>Maps to <c>faker.Address.City()</c>.</summary>
    City,

    /// <summary>Maps to <c>faker.Address.State()</c>.</summary>
    State,

    /// <summary>Maps to <c>faker.Address.ZipCode()</c>.</summary>
    PostalCode,

    /// <summary>Maps to <c>faker.Company.CompanyName()</c>.</summary>
    CompanyName,
}
