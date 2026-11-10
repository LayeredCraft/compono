#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.UnsupportedReturnType\(string, string, Type\) Method

Builds the D4 unsupported\-return\-type failure \(no seed line\)\.

```csharp
public static Compono.CompositionException UnsupportedReturnType(string member, string methodDisplayName, System.Type returnedType);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.UnsupportedReturnType(string,string,System.Type).member'></a>

`member` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The `Namespace.Type.Member` the attribute named\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.UnsupportedReturnType(string,string,System.Type).methodDisplayName'></a>

`methodDisplayName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The test method display name\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.UnsupportedReturnType(string,string,System.Type).returnedType'></a>

`returnedType` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

The unsupported member return type\.

#### Returns
[CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')  
The failure to throw\.