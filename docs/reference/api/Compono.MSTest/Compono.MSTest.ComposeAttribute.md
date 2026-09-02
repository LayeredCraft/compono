#### [Compono\.MSTest](index.md 'index')
### [Compono\.MSTest](Compono.MSTest.md 'Compono\.MSTest')

## ComposeAttribute Class

Composes an MSTest data\-driven test method's parameters through Compono \- the default \(no
explicit profile\) entry point\. Every parameter not supplied inline is composed; a parameter
targeted by a supplied inline value takes that value instead, taking precedence over
composition\. See `docs/adr/0057-compono-mstest-package-design.md` for the full binding
algorithm, discovery/execution behavioral contract, seed policy, and diagnostics\.

```csharp
public class ComposeAttribute : System.Attribute, Microsoft.VisualStudio.TestTools.UnitTesting.ITestDataSource
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → ComposeAttribute

Derived  
↳ [ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.MSTest.ComposeAttribute_TProfile,TConfig_.md 'Compono\.MSTest\.ComposeAttribute\<TProfile,TConfig\>')  
↳ [ComposeAttribute&lt;TProfile&gt;](Compono.MSTest.ComposeAttribute_TProfile_.md 'Compono\.MSTest\.ComposeAttribute\<TProfile\>')

Implements [Microsoft\.VisualStudio\.TestTools\.UnitTesting\.ITestDataSource](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.testtools.unittesting.itestdatasource 'Microsoft\.VisualStudio\.TestTools\.UnitTesting\.ITestDataSource')

### Remarks
Deliberately unsealed \- [ComposeAttribute&lt;TProfile&gt;](Compono.MSTest.ComposeAttribute_TProfile_.md 'Compono\.MSTest\.ComposeAttribute\<TProfile\>') and
[ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.MSTest.ComposeAttribute_TProfile,TConfig_.md 'Compono\.MSTest\.ComposeAttribute\<TProfile,TConfig\>') are the two designed extension points, matching
`Compono.XunitV3`/`Compono.TUnit`'s own family shape\. Implements
[Microsoft\.VisualStudio\.TestTools\.UnitTesting\.ITestDataSource](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.testtools.unittesting.itestdatasource 'Microsoft\.VisualStudio\.TestTools\.UnitTesting\.ITestDataSource') directly on a plain [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') \- never derives from
[Microsoft\.VisualStudio\.TestTools\.UnitTesting\.DataTestMethodAttribute](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.testtools.unittesting.datatestmethodattribute 'Microsoft\.VisualStudio\.TestTools\.UnitTesting\.DataTestMethodAttribute') or any other MSTest attribute base type \(ADR\-0057 §3\)\.
\<b\>One \<see cref="T:Compono\.CompositionRow" /\> per \<see cref="M:Compono\.MSTest\.ComposeAttribute\.GetData\(System\.Reflection\.MethodInfo\)" /\> invocation\.\</b\> MSTest may invoke
[GetData\(MethodInfo\)](Compono.MSTest.ComposeAttribute.GetData(System.Reflection.MethodInfo).md 'Compono\.MSTest\.ComposeAttribute\.GetData\(System\.Reflection\.MethodInfo\)') more than once across separately\-invoked discovery and execution sessions
\(ADR\-0057 §9\) \- consequently, composition \(including any side\-effecting registration factory or
[ICompositionValueProvider](../Compono/Compono.ICompositionValueProvider.md 'Compono\.ICompositionValueProvider')\) may also run more than once for one eventual test case\.
Each invocation's row is independent: `[Shared]`/`Share<T>()` are never split
across calls\.

| Constructors | |
| :--- | :--- |
| [ComposeAttribute\(object\[\]\)](Compono.MSTest.ComposeAttribute.ComposeAttribute(object[]).md 'Compono\.MSTest\.ComposeAttribute\.ComposeAttribute\(object\[\]\)') | Creates a [ComposeAttribute](Compono.MSTest.ComposeAttribute.md 'Compono\.MSTest\.ComposeAttribute')\. |

| Properties | |
| :--- | :--- |
| [Seed](Compono.MSTest.ComposeAttribute.Seed.md 'Compono\.MSTest\.ComposeAttribute\.Seed') | An explicit root seed for this row \- the same underlying contract as [WithSeed\(int\)](../Compono/Compono.CompositionBuilder.WithSeed(int).md 'Compono\.CompositionBuilder\.WithSeed\(System\.Int32\)'), but restricted to non\-negative values so a seed reported in a failure message or [GetDisplayName\(MethodInfo, object\[\]\)](Compono.MSTest.ComposeAttribute.GetDisplayName(System.Reflection.MethodInfo,object[]).md 'Compono\.MSTest\.ComposeAttribute\.GetDisplayName\(System\.Reflection\.MethodInfo, object\[\]\)')'s output is always pasteable back into this property unchanged\. Unset: a fresh, non\-negative seed is generated on every [GetData\(MethodInfo\)](Compono.MSTest.ComposeAttribute.GetData(System.Reflection.MethodInfo).md 'Compono\.MSTest\.ComposeAttribute\.GetData\(System\.Reflection\.MethodInfo\)') call, matching `Compono.XunitV3`/`Compono.TUnit`'s own `Seed` contract exactly\. |

| Methods | |
| :--- | :--- |
| [GetData\(MethodInfo\)](Compono.MSTest.ComposeAttribute.GetData(System.Reflection.MethodInfo).md 'Compono\.MSTest\.ComposeAttribute\.GetData\(System\.Reflection\.MethodInfo\)') | Composes \(or applies inline values to\) one test method's parameters into a single row\. Called by MSTest once per discovered/executed test case \- possibly more than once for the same eventual test case, per ADR\-0057 §9\. Returns exactly one [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')?\`] row, wrapped in a single-element sequence: this attribute owns the entire row, mirroring `Compono.XunitV3`/`Compono.TUnit`'s "one Compose-family attribute per test method" design (enforced by [Compono.MSTest.Binding.BindingPlan`'s own signature validation\)\. |
| [GetDisplayName\(MethodInfo, object\[\]\)](Compono.MSTest.ComposeAttribute.GetDisplayName(System.Reflection.MethodInfo,object[]).md 'Compono\.MSTest\.ComposeAttribute\.GetDisplayName\(System\.Reflection\.MethodInfo, object\[\]\)') | Produces a stable, non\-huge\-object\-dump display name of the form `{methodName} (Compono, seed: {seed})` \- the primary and only supported seed\-reporting path \(ADR\-0057 §15\)\. Recomputes the row's binding plan/composer the same way [GetData\(MethodInfo\)](Compono.MSTest.ComposeAttribute.GetData(System.Reflection.MethodInfo).md 'Compono\.MSTest\.ComposeAttribute\.GetData\(System\.Reflection\.MethodInfo\)') does rather than caching a value across calls, so the reported seed always matches whichever row this specific invocation's [data](Compono.MSTest.ComposeAttribute.GetDisplayName(System.Reflection.MethodInfo,object[]).md#Compono.MSTest.ComposeAttribute.GetDisplayName(System.Reflection.MethodInfo,object[]).data 'Compono\.MSTest\.ComposeAttribute\.GetDisplayName\(System\.Reflection\.MethodInfo, object\[\]\)\.data') came from\. |
