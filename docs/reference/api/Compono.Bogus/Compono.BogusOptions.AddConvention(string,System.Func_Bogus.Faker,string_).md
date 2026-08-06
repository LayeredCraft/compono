#### [Compono\.Bogus](index.md 'index')
### [Compono](Compono.md 'Compono').[BogusOptions](Compono.BogusOptions.md 'Compono\.BogusOptions')

## BogusOptions\.AddConvention\(string, Func\<Faker,string\>\) Method

Adds a custom exact\-name convention: a member named exactly [memberName](Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).md#Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).memberName 'Compono\.BogusOptions\.AddConvention\(string, System\.Func\<Bogus\.Faker,string\>\)\.memberName')
resolves to [generate](Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).md#Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).generate 'Compono\.BogusOptions\.AddConvention\(string, System\.Func\<Bogus\.Faker,string\>\)\.generate')'s result, called against a
`context.DeriveSeed()`\-seeded `Bogus.Faker` \- the same determinism contract every
other value in this package follows\. Validated and applied eagerly, immediately, against this
[BogusOptions](Compono.BogusOptions.md 'Compono\.BogusOptions') instance's own accumulated state \- not deferred to
`UseBogus(...)` returning, and not detected across separate `UseBogus(...)` calls\.

```csharp
public void AddConvention(string memberName, System.Func<Bogus.Faker,string> generate);
```
#### Parameters

<a name='Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).memberName'></a>

`memberName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The exact member name to match\.

<a name='Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).generate'></a>

`generate` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')`Bogus.Faker`[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')

Produces this member's value from a seeded `Bogus.Faker`\. The `Bogus.Faker`
instance is reused across later, unrelated requests on the same thread \(per
[BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider')'s own performance design\) \- always freshly reseeded
before [generate](Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).md#Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).generate 'Compono\.BogusOptions\.AddConvention\(string, System\.Func\<Bogus\.Faker,string\>\)\.generate') is called, so read its randomness normally, but never
retain the instance past this call or rely on any state it carries beyond
`Faker.Random`\.

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[memberName](Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).md#Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).memberName 'Compono\.BogusOptions\.AddConvention\(string, System\.Func\<Bogus\.Faker,string\>\)\.memberName') or [generate](Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).md#Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).generate 'Compono\.BogusOptions\.AddConvention\(string, System\.Func\<Bogus\.Faker,string\>\)\.generate') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[memberName](Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).md#Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).memberName 'Compono\.BogusOptions\.AddConvention\(string, System\.Func\<Bogus\.Faker,string\>\)\.memberName') is empty or all\-whitespace; or already configured as a built\-in
            convention name, an existing alias, or an existing custom convention\.