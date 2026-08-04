#### [Compono\.Bogus](index.md 'index')
### [Compono](Compono.md 'Compono')

## BogusOptions Class

Configuration for [BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider'), set via
`CompositionBuilderExtensions.UseBogus(Action{BogusOptions})`\. See
`docs/adr/0027-compono-bogus-package-design.md` and
`docs/adr/0028-configurable-bogus-member-name-conventions.md`\.

```csharp
public sealed class BogusOptions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → BogusOptions

| Properties | |
| :--- | :--- |
| [EnableMemberNameConventions](Compono.BogusOptions.EnableMemberNameConventions.md 'Compono\.BogusOptions\.EnableMemberNameConventions') | Whether the conservative member\-name convention provider is active\. Defaults to [true](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool')\. Setting this to [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') means [BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider') is not registered at all \- including any [AddAlias\(string, BogusConvention\)](Compono.BogusOptions.AddAlias(string,Compono.BogusConvention).md 'Compono\.BogusOptions\.AddAlias\(string, Compono\.BogusConvention\)')/[AddConvention\(string, Func&lt;Faker,string&gt;\)](Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).md 'Compono\.BogusOptions\.AddConvention\(string, System\.Func\<Bogus\.Faker,string\>\)') entries configured in the same call; there is no partial mode that disables only the built\-in conventions\. |
| [Locale](Compono.BogusOptions.Locale.md 'Compono\.BogusOptions\.Locale') | The Bogus locale used by the package\-wide member\-name convention provider \([BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider')\) only\. `UseBogus<T>()` is independent of this option and does not read it \- it defaults to `"en"` on its own, or takes an explicit `locale` parameter\. Defaults to Bogus's own default \(`"en"`\)\. |

| Methods | |
| :--- | :--- |
| [AddAlias\(string, BogusConvention\)](Compono.BogusOptions.AddAlias(string,Compono.BogusConvention).md 'Compono\.BogusOptions\.AddAlias\(string, Compono\.BogusConvention\)') | Adds an additional exact member name that resolves to the same generator as a built\-in [BogusConvention](Compono.BogusConvention.md 'Compono\.BogusConvention') \- e\.g\. `AddAlias("GivenName", BogusConvention.FirstName)` lets a domain that calls first names "GivenName" still get realistic values from `UseBogus()` alone\. Validated and applied eagerly, immediately, against this [BogusOptions](Compono.BogusOptions.md 'Compono\.BogusOptions') instance's own accumulated state \- not deferred to `UseBogus(...)` returning, and not detected across separate `UseBogus(...)` calls\. |
| [AddConvention\(string, Func&lt;Faker,string&gt;\)](Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).md 'Compono\.BogusOptions\.AddConvention\(string, System\.Func\<Bogus\.Faker,string\>\)') | Adds a custom exact\-name convention: a member named exactly [memberName](Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).md#Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).memberName 'Compono\.BogusOptions\.AddConvention\(string, System\.Func\<Bogus\.Faker,string\>\)\.memberName') resolves to [generate](Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).md#Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).generate 'Compono\.BogusOptions\.AddConvention\(string, System\.Func\<Bogus\.Faker,string\>\)\.generate')'s result, called against a request\-local, `context.DeriveSeed()`\-seeded [Bogus\.Faker](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker 'Bogus\.Faker') \- the same determinism contract every other value in this package follows\. Validated and applied eagerly, immediately, against this [BogusOptions](Compono.BogusOptions.md 'Compono\.BogusOptions') instance's own accumulated state \- not deferred to `UseBogus(...)` returning, and not detected across separate `UseBogus(...)` calls\. |
