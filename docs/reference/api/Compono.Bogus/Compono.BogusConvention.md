#### [Compono\.Bogus](index.md 'index')
### [Compono](Compono.md 'Compono')

## BogusConvention Enum

One of Compono\.Bogus's fixed set of built\-in, conservative member\-name conventions \- see
`docs/adr/0027-compono-bogus-package-design.md`'s Model 1\. Deliberately not extensible: a new
built\-in convention requires a new enum member, a generator mapping, documentation, and tests, not
a value a consumer can define themselves \- custom behavior belongs in
[AddConvention\(string, Func&lt;Faker,string&gt;\)](Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).md 'Compono\.BogusOptions\.AddConvention\(string, System\.Func\<Bogus\.Faker,string\>\)'), not in this enum\. See
`docs/adr/0028-configurable-bogus-member-name-conventions.md`\.

```csharp
public enum BogusConvention
```
### Fields

<a name='Compono.BogusConvention.FirstName'></a>

`FirstName` 0

Maps to `faker.Name.FirstName()`\.

<a name='Compono.BogusConvention.LastName'></a>

`LastName` 1

Maps to `faker.Name.LastName()`\.

<a name='Compono.BogusConvention.FullName'></a>

`FullName` 2

Maps to `faker.Name.FullName()`\.

<a name='Compono.BogusConvention.Email'></a>

`Email` 3

Maps to `faker.Internet.Email()`\.

<a name='Compono.BogusConvention.PhoneNumber'></a>

`PhoneNumber` 4

Maps to `faker.Phone.PhoneNumber()`\.

<a name='Compono.BogusConvention.StreetAddress'></a>

`StreetAddress` 5

Maps to `faker.Address.StreetAddress()`\.

<a name='Compono.BogusConvention.City'></a>

`City` 6

Maps to `faker.Address.City()`\.

<a name='Compono.BogusConvention.State'></a>

`State` 7

Maps to `faker.Address.State()`\.

<a name='Compono.BogusConvention.PostalCode'></a>

`PostalCode` 8

Maps to `faker.Address.ZipCode()`\.

<a name='Compono.BogusConvention.CompanyName'></a>

`CompanyName` 9

Maps to `faker.Company.CompanyName()`\.