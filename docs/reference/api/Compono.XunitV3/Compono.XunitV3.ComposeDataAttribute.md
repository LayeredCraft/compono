#### [Compono\.XunitV3](index.md 'index')
### [Compono\.XunitV3](Compono.XunitV3.md 'Compono\.XunitV3')

## ComposeDataAttribute Class

Composes an xUnit v3 theory row's parameters through Compono, reading the rows from a public static
field, property, or parameterless method \- the Composed theory rows counterpart to
[ComposeAttribute](Compono.XunitV3.ComposeAttribute.md 'Compono\.XunitV3\.ComposeAttribute')\. Each row is a separate theory row whose parameters are composed the
same way [ComposeAttribute](Compono.XunitV3.ComposeAttribute.md 'Compono\.XunitV3\.ComposeAttribute') composes a single row\. See
`docs/adr/0070-xunitv3-composedata-composed-theory-rows.md` for the full collection and row
shapes, the leading\-positional binding algorithm, the seed policy, and the diagnostics\.

```csharp
public class ComposeDataAttribute : Xunit.v3.DataAttribute
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → `Xunit.v3.DataAttribute` → ComposeDataAttribute

Derived  
↳ [ComposeDataAttribute&lt;TProfile,TConfig&gt;](Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.md 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile,TConfig\>')  
↳ [ComposeDataAttribute&lt;TProfile&gt;](Compono.XunitV3.ComposeDataAttribute_TProfile_.md 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile\>')

### Remarks
Deliberately unsealed \- [ComposeDataAttribute&lt;TProfile&gt;](Compono.XunitV3.ComposeDataAttribute_TProfile_.md 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile\>') and
[ComposeDataAttribute&lt;TProfile,TConfig&gt;](Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.md 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile,TConfig\>') are the two designed extension points,
mirroring [ComposeAttribute](Compono.XunitV3.ComposeAttribute.md 'Compono\.XunitV3\.ComposeAttribute')'s own generic siblings\. Any other subclass is rejected by
[GetData\(MethodInfo, DisposalTracker\)](Compono.XunitV3.ComposeDataAttribute.GetData(System.Reflection.MethodInfo,Xunit.Sdk.DisposalTracker).md 'Compono\.XunitV3\.ComposeDataAttribute\.GetData\(System\.Reflection\.MethodInfo, Xunit\.Sdk\.DisposalTracker\)') with a [CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException') rather than being silently ignored\.

| Constructors | |
| :--- | :--- |
| [ComposeDataAttribute\(string\)](Compono.XunitV3.ComposeDataAttribute.ComposeDataAttribute(string).md 'Compono\.XunitV3\.ComposeDataAttribute\.ComposeDataAttribute\(string\)') | Creates a [ComposeDataAttribute](Compono.XunitV3.ComposeDataAttribute.md 'Compono\.XunitV3\.ComposeDataAttribute')\. |

| Properties | |
| :--- | :--- |
| [MemberName](Compono.XunitV3.ComposeDataAttribute.MemberName.md 'Compono\.XunitV3\.ComposeDataAttribute\.MemberName') | The name of the public static member that supplies this theory's rows\. |
| [MemberType](Compono.XunitV3.ComposeDataAttribute.MemberType.md 'Compono\.XunitV3\.ComposeDataAttribute\.MemberType') | The type that declares [MemberName](Compono.XunitV3.ComposeDataAttribute.MemberName.md 'Compono\.XunitV3\.ComposeDataAttribute\.MemberName')\. [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') \(the default\) means the test class itself, matching xUnit's own `MemberData` convention\. The public fields, properties, and methods are preserved for dynamic access, since the member is located by reflection\. |
| [Seed](Compono.XunitV3.ComposeDataAttribute.Seed.md 'Compono\.XunitV3\.ComposeDataAttribute\.Seed') | An explicit root seed pinned to every row this attribute produces \- the same underlying contract as [Seed](Compono.XunitV3.ComposeAttribute.Seed.md 'Compono\.XunitV3\.ComposeAttribute\.Seed'), but restricted to non\-negative values \(enforced by the execution algorithm, not here\)\. A plain [int](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/int'), not [int?](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int? 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/int?') \- an attribute named argument cannot target a [System\.Nullable&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1') property \(CS0655\); see `Compono.XunitV3.ComposeDataAttribute.SeedAsNullable` for the property the execution algorithm actually reads\. |

| Methods | |
| :--- | :--- |
| [GetData\(MethodInfo, DisposalTracker\)](Compono.XunitV3.ComposeDataAttribute.GetData(System.Reflection.MethodInfo,Xunit.Sdk.DisposalTracker).md 'Compono\.XunitV3\.ComposeDataAttribute\.GetData\(System\.Reflection\.MethodInfo, Xunit\.Sdk\.DisposalTracker\)') | Returns the data to be used to test the theory\. |
| [SupportsDiscoveryEnumeration\(\)](Compono.XunitV3.ComposeDataAttribute.SupportsDiscoveryEnumeration().md 'Compono\.XunitV3\.ComposeDataAttribute\.SupportsDiscoveryEnumeration\(\)') | Returns [true](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') if the data attribute supports enumeration during discovery; [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') otherwise\. Data attributes with expensive computational costs and/or randomized data sets should return [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool')\. |
