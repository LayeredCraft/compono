#### [Compono\.XunitV3](index.md 'index')
### [Compono\.XunitV3](Compono.XunitV3.md 'Compono\.XunitV3')

## ComposeAttribute Class

Composes an xUnit v3 theory row's parameters through Compono \- the default \(no explicit profile\)
entry point\. Every parameter not supplied inline is composed; a parameter targeted by a supplied
inline value takes that value instead, taking precedence over composition\. See
`docs/adr/0022-compono-xunit-package-design.md` for the full binding algorithm, seed policy,
and diagnostics\.

```csharp
public class ComposeAttribute : Xunit.v3.DataAttribute
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → [Xunit\.v3\.DataAttribute](https://learn.microsoft.com/en-us/dotnet/api/xunit.v3.dataattribute 'Xunit\.v3\.DataAttribute') → ComposeAttribute

Derived  
↳ [ComposeAttribute&lt;TProfile&gt;](Compono.XunitV3.ComposeAttribute_TProfile_.md 'Compono\.XunitV3\.ComposeAttribute\<TProfile\>')

### Remarks
Deliberately unsealed \- [ComposeAttribute&lt;TProfile&gt;](Compono.XunitV3.ComposeAttribute_TProfile_.md 'Compono\.XunitV3\.ComposeAttribute\<TProfile\>') is the one designed extension
point, mirroring [AddProfile&lt;TProfile&gt;\(\)](../Compono/Compono.CompositionBuilder.AddProfile.md#Compono.CompositionBuilder.AddProfile_TProfile_() 'Compono\.CompositionBuilder\.AddProfile\`\`1')'s own
`TProfile : ICompositionProfile, new()` constraint\. [SupportsDiscoveryEnumeration\(\)](Compono.XunitV3.ComposeAttribute.SupportsDiscoveryEnumeration().md 'Compono\.XunitV3\.ComposeAttribute\.SupportsDiscoveryEnumeration\(\)')
returns [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool'): composition is deferred entirely to execution time, so
[GetData\(MethodInfo, DisposalTracker\)](Compono.XunitV3.ComposeAttribute.GetData(System.Reflection.MethodInfo,Xunit.Sdk.DisposalTracker).md 'Compono\.XunitV3\.ComposeAttribute\.GetData\(System\.Reflection\.MethodInfo, Xunit\.Sdk\.DisposalTracker\)') runs for real exactly once per test execution \- there is no separate
discovery\-time composition pass to keep synchronized with it\.

| Constructors | |
| :--- | :--- |
| [ComposeAttribute\(object\[\]\)](Compono.XunitV3.ComposeAttribute.ComposeAttribute(object[]).md 'Compono\.XunitV3\.ComposeAttribute\.ComposeAttribute\(object\[\]\)') | Creates a [ComposeAttribute](Compono.XunitV3.ComposeAttribute.md 'Compono\.XunitV3\.ComposeAttribute')\. |

| Properties | |
| :--- | :--- |
| [Seed](Compono.XunitV3.ComposeAttribute.Seed.md 'Compono\.XunitV3\.ComposeAttribute\.Seed') | An explicit root seed for this row \- the same underlying contract as [WithSeed\(int\)](../Compono/Compono.CompositionBuilder.WithSeed(int).md 'Compono\.CompositionBuilder\.WithSeed\(System\.Int32\)'), but restricted to non\-negative values \(enforced by Phase 2's binding algorithm, not here\) so a seed reported in a failure message is always pasteable back into this property unchanged\. Unset: a fresh, non\-negative seed is generated on every [GetData\(MethodInfo, DisposalTracker\)](Compono.XunitV3.ComposeAttribute.GetData(System.Reflection.MethodInfo,Xunit.Sdk.DisposalTracker).md 'Compono\.XunitV3\.ComposeAttribute\.GetData\(System\.Reflection\.MethodInfo, Xunit\.Sdk\.DisposalTracker\)') call \- \<b\>unless\</b\> a profile applied via [ComposeAttribute&lt;TProfile&gt;](Compono.XunitV3.ComposeAttribute_TProfile_.md 'Compono\.XunitV3\.ComposeAttribute\<TProfile\>')'s `TProfile.Configure` itself calls [WithSeed\(int\)](../Compono/Compono.CompositionBuilder.WithSeed(int).md 'Compono\.CompositionBuilder\.WithSeed\(System\.Int32\)'), in which case every row reuses that profile\-configured seed instead, even though this property itself was never set \(ADR\-0022 Amendment 3 \- a profile pinning a seed is a deliberate reproducibility choice, honored the same way a value set here would be\)\. A plain [int](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/int'), not [int?](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int? 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/int?') \- an attribute named argument cannot target a [System\.Nullable&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1') property \(CS0655\); see [Compono\.XunitV3\.ComposeAttribute\.SeedAsNullable](https://learn.microsoft.com/en-us/dotnet/api/compono.xunitv3.composeattribute.seedasnullable 'Compono\.XunitV3\.ComposeAttribute\.SeedAsNullable') for the property the binding algorithm actually reads\. |

| Methods | |
| :--- | :--- |
| [GetData\(MethodInfo, DisposalTracker\)](Compono.XunitV3.ComposeAttribute.GetData(System.Reflection.MethodInfo,Xunit.Sdk.DisposalTracker).md 'Compono\.XunitV3\.ComposeAttribute\.GetData\(System\.Reflection\.MethodInfo, Xunit\.Sdk\.DisposalTracker\)') | Composes \(or applies inline values to\) one theory row's parameters\. See ADR\-0022's "Inline/composed binding algorithm" section for the full step\-by\-step behavior this implements\. |
| [SupportsDiscoveryEnumeration\(\)](Compono.XunitV3.ComposeAttribute.SupportsDiscoveryEnumeration().md 'Compono\.XunitV3\.ComposeAttribute\.SupportsDiscoveryEnumeration\(\)') | Returns [true](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') if the data attribute supports enumeration during discovery; [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') otherwise\. Data attributes with expensive computational costs and/or randomized data sets should return [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool')\. |
