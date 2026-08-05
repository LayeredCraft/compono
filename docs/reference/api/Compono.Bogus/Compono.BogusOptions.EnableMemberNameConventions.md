#### [Compono\.Bogus](index.md 'index')
### [Compono](Compono.md 'Compono').[BogusOptions](Compono.BogusOptions.md 'Compono\.BogusOptions')

## BogusOptions\.EnableMemberNameConventions Property

Whether the conservative member\-name convention provider is active\. Defaults to
[true](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool')\. Setting this to [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') means
[BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider') is not registered at all \- including any
[AddAlias\(string, BogusConvention\)](Compono.BogusOptions.AddAlias(string,Compono.BogusConvention).md 'Compono\.BogusOptions\.AddAlias\(string, Compono\.BogusConvention\)')/[AddConvention\(string, Func&lt;Faker,string&gt;\)](Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).md 'Compono\.BogusOptions\.AddConvention\(string, System\.Func\<Bogus\.Faker,string\>\)') entries configured in the same call; there
is no partial mode that disables only the built\-in conventions\.

```csharp
public bool EnableMemberNameConventions { get; set; }
```

#### Property Value
[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')