#### [Compono\.MSTest](index.md 'index')
### [Compono\.MSTest](Compono.MSTest.md 'Compono\.MSTest').[ComposeAttribute](Compono.MSTest.ComposeAttribute.md 'Compono\.MSTest\.ComposeAttribute')

## ComposeAttribute\.GetDisplayName\(MethodInfo, object\[\]\) Method

Produces a stable, non\-huge\-object\-dump display name of the form
`{methodName} (Compono, seed: {seed})` \- the primary and only supported seed\-reporting
path \(ADR\-0057 §15\)\. Recomputes the row's binding plan/composer the same way [GetData\(MethodInfo\)](Compono.MSTest.ComposeAttribute.GetData(System.Reflection.MethodInfo).md 'Compono\.MSTest\.ComposeAttribute\.GetData\(System\.Reflection\.MethodInfo\)')
does rather than caching a value across calls, so the reported seed always matches whichever
row this specific invocation's [data](Compono.MSTest.ComposeAttribute.GetDisplayName(System.Reflection.MethodInfo,object[]).md#Compono.MSTest.ComposeAttribute.GetDisplayName(System.Reflection.MethodInfo,object[]).data 'Compono\.MSTest\.ComposeAttribute\.GetDisplayName\(System\.Reflection\.MethodInfo, object\[\]\)\.data') came from\.

```csharp
public string? GetDisplayName(System.Reflection.MethodInfo methodInfo, object?[]? data);
```
#### Parameters

<a name='Compono.MSTest.ComposeAttribute.GetDisplayName(System.Reflection.MethodInfo,object[]).methodInfo'></a>

`methodInfo` [System\.Reflection\.MethodInfo](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.methodinfo 'System\.Reflection\.MethodInfo')

<a name='Compono.MSTest.ComposeAttribute.GetDisplayName(System.Reflection.MethodInfo,object[]).data'></a>

`data` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

Implements [GetDisplayName\(MethodInfo, object\[\]\)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.testtools.unittesting.itestdatasource.getdisplayname#microsoft-visualstudio-testtools-unittesting-itestdatasource-getdisplayname(system-reflection-methodinfo-system-object[]) 'Microsoft\.VisualStudio\.TestTools\.UnitTesting\.ITestDataSource\.GetDisplayName\(System\.Reflection\.MethodInfo,System\.Object\[\]\)')

#### Returns
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')