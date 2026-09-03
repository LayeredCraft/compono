#### [Compono\.NUnit](index.md 'index')
### [Compono\.NUnit](Compono.NUnit.md 'Compono\.NUnit')

## ComposeAttribute Class

Composes an NUnit test method's parameters through Compono \- the default \(no explicit profile\)
entry point\. Every parameter not supplied inline is composed; a parameter targeted by a supplied
inline value takes that value instead, taking precedence over composition\. See
`docs/adr/0059-compono-nunit-package-design.md` for the full binding algorithm,
discovery/execution behavioral contract, seed policy, and diagnostics\.

```csharp
public class ComposeAttribute : NUnit.Framework.TestAttribute, NUnit.Framework.Interfaces.ITestBuilder
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → `NUnit.Framework.NUnitAttribute` → `NUnit.Framework.TestAttribute` → ComposeAttribute

Derived  
↳ [ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.NUnit.ComposeAttribute_TProfile,TConfig_.md 'Compono\.NUnit\.ComposeAttribute\<TProfile,TConfig\>')  
↳ [ComposeAttribute&lt;TProfile&gt;](Compono.NUnit.ComposeAttribute_TProfile_.md 'Compono\.NUnit\.ComposeAttribute\<TProfile\>')

Implements `NUnit.Framework.Interfaces.ITestBuilder`

### Remarks
Deliberately unsealed \- [ComposeAttribute&lt;TProfile&gt;](Compono.NUnit.ComposeAttribute_TProfile_.md 'Compono\.NUnit\.ComposeAttribute\<TProfile\>') and
[ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.NUnit.ComposeAttribute_TProfile,TConfig_.md 'Compono\.NUnit\.ComposeAttribute\<TProfile,TConfig\>') are the two designed extension points, matching
`Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest`'s own family shape\. Derives from
`NUnit.Framework.TestAttribute` \(NUnit's own native test\-identifying attribute\) and implements
`NUnit.Framework.Interfaces.ITestBuilder` directly \- the smallest seam found capable of both making
`[Compose]`\-decorated methods independently discoverable by NUnit \(no `[TestFixture]`
required on the containing class\) \<em\>and\</em\> owning one complete composed row per method
\(ADR\-0059 §4/§5/§7\)\. [BuildFrom\(IMethodInfo, Test\)](Compono.NUnit.ComposeAttribute.BuildFrom(NUnit.Framework.Interfaces.IMethodInfo,NUnit.Framework.Internal.Test).md 'Compono\.NUnit\.ComposeAttribute\.BuildFrom\(NUnit\.Framework\.Interfaces\.IMethodInfo, NUnit\.Framework\.Internal\.Test\)') is declared [new](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/new 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/new') \- an explicit,
intentional hiding of `NUnit.Framework.TestAttribute`'s own inherited `NUnit.Framework.Interfaces.ISimpleTestBuilder`
implementation; spike\-confirmed \(ADR\-0059 §4\) to change no observable behavior \(the
`NUnit.Framework.Interfaces.ITestBuilder` interface map always resolves to this type's own [BuildFrom\(IMethodInfo, Test\)](Compono.NUnit.ComposeAttribute.BuildFrom(NUnit.Framework.Interfaces.IMethodInfo,NUnit.Framework.Internal.Test).md 'Compono\.NUnit\.ComposeAttribute\.BuildFrom\(NUnit\.Framework\.Interfaces\.IMethodInfo, NUnit\.Framework\.Internal\.Test\)')
regardless of [new](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/new 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/new')\), but required to build without `CS0108`\.
\<b\>One \<see cref="T:Compono\.CompositionRow" /\> per \<see cref="M:Compono\.NUnit\.ComposeAttribute\.BuildFrom\(NUnit\.Framework\.Interfaces\.IMethodInfo,NUnit\.Framework\.Internal\.Test\)" /\> invocation\.\</b\> NUnit may invoke
[BuildFrom\(IMethodInfo, Test\)](Compono.NUnit.ComposeAttribute.BuildFrom(NUnit.Framework.Interfaces.IMethodInfo,NUnit.Framework.Internal.Test).md 'Compono\.NUnit\.ComposeAttribute\.BuildFrom\(NUnit\.Framework\.Interfaces\.IMethodInfo, NUnit\.Framework\.Internal\.Test\)') more than once across separately\-invoked discovery and execution sessions
under classic VSTest \(ADR\-0059 §12\) \- consequently, composition \(including any side\-effecting
registration factory or [ICompositionValueProvider](../Compono/Compono.ICompositionValueProvider.md 'Compono\.ICompositionValueProvider')\) may also run more than once for
one eventual test case\. Each invocation's row is independent: `[Shared]`/
`Share<T>()` are never split across calls\.

| Constructors | |
| :--- | :--- |
| [ComposeAttribute\(object\[\]\)](Compono.NUnit.ComposeAttribute.ComposeAttribute(object[]).md 'Compono\.NUnit\.ComposeAttribute\.ComposeAttribute\(object\[\]\)') | Creates a [ComposeAttribute](Compono.NUnit.ComposeAttribute.md 'Compono\.NUnit\.ComposeAttribute')\. |

| Properties | |
| :--- | :--- |
| [Seed](Compono.NUnit.ComposeAttribute.Seed.md 'Compono\.NUnit\.ComposeAttribute\.Seed') | An explicit root seed for this row \- the same underlying contract as [WithSeed\(int\)](../Compono/Compono.CompositionBuilder.WithSeed(int).md 'Compono\.CompositionBuilder\.WithSeed\(System\.Int32\)'), but restricted to non\-negative values so a seed reported in a failure message or the constructed test's display name is always pasteable back into this property unchanged\. Unset: a fresh, non\-negative seed is generated on every [BuildFrom\(IMethodInfo, Test\)](Compono.NUnit.ComposeAttribute.BuildFrom(NUnit.Framework.Interfaces.IMethodInfo,NUnit.Framework.Internal.Test).md 'Compono\.NUnit\.ComposeAttribute\.BuildFrom\(NUnit\.Framework\.Interfaces\.IMethodInfo, NUnit\.Framework\.Internal\.Test\)') call, matching `Compono.XunitV3`/`Compono.TUnit`/ `Compono.MSTest`'s own `Seed` contract exactly\. |

| Methods | |
| :--- | :--- |
| [BuildFrom\(IMethodInfo, Test\)](Compono.NUnit.ComposeAttribute.BuildFrom(NUnit.Framework.Interfaces.IMethodInfo,NUnit.Framework.Internal.Test).md 'Compono\.NUnit\.ComposeAttribute\.BuildFrom\(NUnit\.Framework\.Interfaces\.IMethodInfo, NUnit\.Framework\.Internal\.Test\)') | Composes \(or applies inline values to\) one test method's parameters into a single row and constructs the resulting `NUnit.Framework.Internal.TestMethod` NUnit expects back\. Called by NUnit once per discovered/executed test case \- possibly more than once for the same eventual test case, per ADR\-0059 §12\. Yields exactly one `NUnit.Framework.Internal.TestMethod`: this attribute owns the entire row, mirroring `Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest`'s "one Compose\-family attribute per test method" design \(enforced by `Compono.NUnit.Binding.BindingPlan`'s own signature validation\) \- independently confirmed by spike \(ADR\-0059 §8\) to coexist with NUnit's own `[TestCase]`/`[Values]`/`[Range]`/custom `NUnit.Framework.Interfaces.IParameterDataSource` as independent rows, never merged\. |
