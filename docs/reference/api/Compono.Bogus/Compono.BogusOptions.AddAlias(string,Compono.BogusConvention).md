#### [Compono\.Bogus](index.md 'index')
### [Compono](Compono.md 'Compono').[BogusOptions](Compono.BogusOptions.md 'Compono\.BogusOptions')

## BogusOptions\.AddAlias\(string, BogusConvention\) Method

Adds an additional exact member name that resolves to the same generator as a built\-in
[BogusConvention](Compono.BogusConvention.md 'Compono\.BogusConvention') \- e\.g\. `AddAlias("GivenName", BogusConvention.FirstName)`
lets a domain that calls first names "GivenName" still get realistic values from
`UseBogus()` alone\. Validated and applied eagerly, immediately, against this
[BogusOptions](Compono.BogusOptions.md 'Compono\.BogusOptions') instance's own accumulated state \- not deferred to
`UseBogus(...)` returning, and not detected across separate `UseBogus(...)` calls\.

```csharp
public void AddAlias(string aliasName, Compono.BogusConvention target);
```
#### Parameters

<a name='Compono.BogusOptions.AddAlias(string,Compono.BogusConvention).aliasName'></a>

`aliasName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The additional exact member name to match\.

<a name='Compono.BogusOptions.AddAlias(string,Compono.BogusConvention).target'></a>

`target` [BogusConvention](Compono.BogusConvention.md 'Compono\.BogusConvention')

The built\-in convention this alias's matched requests should generate\.

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[aliasName](Compono.BogusOptions.AddAlias(string,Compono.BogusConvention).md#Compono.BogusOptions.AddAlias(string,Compono.BogusConvention).aliasName 'Compono\.BogusOptions\.AddAlias\(string, Compono\.BogusConvention\)\.aliasName') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[aliasName](Compono.BogusOptions.AddAlias(string,Compono.BogusConvention).md#Compono.BogusOptions.AddAlias(string,Compono.BogusConvention).aliasName 'Compono\.BogusOptions\.AddAlias\(string, Compono\.BogusConvention\)\.aliasName') is empty or all\-whitespace; or already configured as a built\-in
            convention name, an existing alias, or an existing custom convention\.

[System\.ArgumentOutOfRangeException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception 'System\.ArgumentOutOfRangeException')  
[target](Compono.BogusOptions.AddAlias(string,Compono.BogusConvention).md#Compono.BogusOptions.AddAlias(string,Compono.BogusConvention).target 'Compono\.BogusOptions\.AddAlias\(string, Compono\.BogusConvention\)\.target') is not a defined [BogusConvention](Compono.BogusConvention.md 'Compono\.BogusConvention') value\.