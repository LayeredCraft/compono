#### [Compono\.XunitV3](index.md 'index')
### [Compono\.XunitV3](Compono.XunitV3.md 'Compono\.XunitV3')

## ComposeDataAttribute\<TProfile\> Class

Composes an xUnit v3 theory row's parameters through Compono with [TProfile](Compono.XunitV3.ComposeDataAttribute_TProfile_.md#Compono.XunitV3.ComposeDataAttribute_TProfile_.TProfile 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile\>\.TProfile')
applied \- the Composed theory rows counterpart to [ComposeAttribute&lt;TProfile&gt;](Compono.XunitV3.ComposeAttribute_TProfile_.md 'Compono\.XunitV3\.ComposeAttribute\<TProfile\>')\. Rows are
read from the same public static member shape [ComposeDataAttribute](Compono.XunitV3.ComposeDataAttribute.md 'Compono\.XunitV3\.ComposeDataAttribute') documents; each row
is composed against a fresh [TProfile](Compono.XunitV3.ComposeDataAttribute_TProfile_.md#Compono.XunitV3.ComposeDataAttribute_TProfile_.TProfile 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile\>\.TProfile') instance\. See
`docs/adr/0070-xunitv3-composedata-composed-theory-rows.md`\.

```csharp
public sealed class ComposeDataAttribute<TProfile> : Compono.XunitV3.ComposeDataAttribute
    where TProfile : Compono.ICompositionProfile, new()
```
#### Type parameters

<a name='Compono.XunitV3.ComposeDataAttribute_TProfile_.TProfile'></a>

`TProfile`

The profile to apply\. The `new()` constraint mirrors
[ComposeAttribute&lt;TProfile&gt;](Compono.XunitV3.ComposeAttribute_TProfile_.md 'Compono\.XunitV3\.ComposeAttribute\<TProfile\>')'s own designed shape \(a fixed, default\-constructed profile\)\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → `Xunit.v3.DataAttribute` → [ComposeDataAttribute](Compono.XunitV3.ComposeDataAttribute.md 'Compono\.XunitV3\.ComposeDataAttribute') → ComposeDataAttribute\<TProfile\>

### Remarks
Construction of the profile is reflection\-free \(`new TProfile()`\), identical to
[ComposeAttribute&lt;TProfile&gt;](Compono.XunitV3.ComposeAttribute_TProfile_.md 'Compono\.XunitV3\.ComposeAttribute\<TProfile\>') \- [System\.Diagnostics\.CodeAnalysis\.DynamicallyAccessedMembersAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.dynamicallyaccessedmembersattribute 'System\.Diagnostics\.CodeAnalysis\.DynamicallyAccessedMembersAttribute') is
carried on [TProfile](Compono.XunitV3.ComposeDataAttribute_TProfile_.md#Compono.XunitV3.ComposeDataAttribute_TProfile_.TProfile 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile\>\.TProfile') for parity with
[ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.XunitV3.ComposeAttribute_TProfile,TConfig_.md 'Compono\.XunitV3\.ComposeAttribute\<TProfile,TConfig\>')'s generic parameters even though the closed
`new()` constraint would not itself strip the public constructors\.

| Constructors | |
| :--- | :--- |
| [ComposeDataAttribute\(string\)](Compono.XunitV3.ComposeDataAttribute_TProfile_.ComposeDataAttribute(string).md 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile\>\.ComposeDataAttribute\(string\)') | Creates a [ComposeDataAttribute&lt;TProfile&gt;](Compono.XunitV3.ComposeDataAttribute_TProfile_.md 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile\>')\. |
