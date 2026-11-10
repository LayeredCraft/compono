#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.NullResult\(string, string\) Method

Builds the D5 null\-result failure \(no seed line\)\.

```csharp
public static Compono.CompositionException NullResult(string member, string methodDisplayName);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.NullResult(string,string).member'></a>

`member` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The `Namespace.Type.Member` the attribute named\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.NullResult(string,string).methodDisplayName'></a>

`methodDisplayName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The test method display name\.

#### Returns
[CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')  
The failure to throw\.