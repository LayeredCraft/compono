#### [Compono\.MSTest](index.md 'index')
### [Compono\.MSTest](Compono.MSTest.md 'Compono\.MSTest').[ComposeAttribute](Compono.MSTest.ComposeAttribute.md 'Compono\.MSTest\.ComposeAttribute')

## ComposeAttribute\.GetData\(MethodInfo\) Method

Composes \(or applies inline values to\) one test method's parameters into a single row\. Called
by MSTest once per discovered/executed test case \- possibly more than once for the same
eventual test case, per ADR\-0057 §9\. Returns exactly one [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')?\`] row, wrapped
in a single-element sequence: this attribute owns the entire row, mirroring
`Compono.XunitV3`/`Compono.TUnit`'s "one Compose-family attribute per test method"
design (enforced by [Compono.MSTest.Binding.BindingPlan`'s own signature validation\)\.

```csharp
public System.Collections.Generic.IEnumerable<object?[]> GetData(System.Reflection.MethodInfo methodInfo);
```
#### Parameters

<a name='Compono.MSTest.ComposeAttribute.GetData(System.Reflection.MethodInfo).methodInfo'></a>

`methodInfo` [System\.Reflection\.MethodInfo](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.methodinfo 'System\.Reflection\.MethodInfo')

Implements [GetData\(MethodInfo\)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.testtools.unittesting.itestdatasource.getdata#microsoft-visualstudio-testtools-unittesting-itestdatasource-getdata(system-reflection-methodinfo) 'Microsoft\.VisualStudio\.TestTools\.UnitTesting\.ITestDataSource\.GetData\(System\.Reflection\.MethodInfo\)')

#### Returns
[System\.Collections\.Generic\.IEnumerable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1 'System\.Collections\.Generic\.IEnumerable\`1')[System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1 'System\.Collections\.Generic\.IEnumerable\`1')

#### Exceptions

[CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')  
This attribute's configured seed \(or a profile\-configured one\) is negative; the test method's
signature is unsupported \(a generic method, a `ref`/`out`/`in`/`params`
parameter, a `ref struct`/pointer\-typed parameter, more than one Compose\-family
attribute, or more than one `[Shared]` parameter of the same type\); too many inline
values were supplied; a supplied inline value is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a non\-nullable
parameter or has a type not assignable to its parameter; or composition itself fails for a
parameter\.