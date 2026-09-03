#### [Compono\.NUnit](index.md 'index')
### [Compono\.NUnit](Compono.NUnit.md 'Compono\.NUnit').[ComposeAttribute](Compono.NUnit.ComposeAttribute.md 'Compono\.NUnit\.ComposeAttribute')

## ComposeAttribute\.BuildFrom\(IMethodInfo, Test\) Method

Composes \(or applies inline values to\) one test method's parameters into a single row and
constructs the resulting `NUnit.Framework.Internal.TestMethod` NUnit expects back\. Called by NUnit once per
discovered/executed test case \- possibly more than once for the same eventual test case, per
ADR\-0059 §12\. Yields exactly one `NUnit.Framework.Internal.TestMethod`: this attribute owns the entire row,
mirroring `Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest`'s "one Compose\-family
attribute per test method" design \(enforced by `Compono.NUnit.Binding.BindingPlan`'s own signature
validation\) \- independently confirmed by spike \(ADR\-0059 §8\) to coexist with NUnit's own
`[TestCase]`/`[Values]`/`[Range]`/custom `NUnit.Framework.Interfaces.IParameterDataSource` as
independent rows, never merged\.

```csharp
public System.Collections.Generic.IEnumerable<NUnit.Framework.Internal.TestMethod> BuildFrom(NUnit.Framework.Interfaces.IMethodInfo method, NUnit.Framework.Internal.Test? suite);
```
#### Parameters

<a name='Compono.NUnit.ComposeAttribute.BuildFrom(NUnit.Framework.Interfaces.IMethodInfo,NUnit.Framework.Internal.Test).method'></a>

`method` `NUnit.Framework.Interfaces.IMethodInfo`

<a name='Compono.NUnit.ComposeAttribute.BuildFrom(NUnit.Framework.Interfaces.IMethodInfo,NUnit.Framework.Internal.Test).suite'></a>

`suite` `NUnit.Framework.Internal.Test`

Implements `BuildFrom(IMethodInfo, Test)`, `BuildFrom(IMethodInfo, Test)`

#### Returns
[System\.Collections\.Generic\.IEnumerable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1 'System\.Collections\.Generic\.IEnumerable\`1')`NUnit.Framework.Internal.TestMethod`[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1 'System\.Collections\.Generic\.IEnumerable\`1')

#### Exceptions

[CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')  
This attribute's configured seed \(or a profile\-configured one\) is negative; the test method's
signature is unsupported \(a generic method, a `ref`/`out`/`in`/`params`
parameter, a `ref struct`/pointer\-typed parameter, more than one Compose\-family
attribute, or more than one `[Shared]` parameter of the same type\); too many inline
values were supplied; a supplied inline value is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a non\-nullable
parameter or has a type not assignable to its parameter; or composition itself fails for a
parameter\.