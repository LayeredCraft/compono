#### [Compono\.XunitV3](index.md 'index')
### [Compono\.XunitV3](Compono.XunitV3.md 'Compono\.XunitV3')

## ComposeDataAttribute\<TProfile,TConfig\> Class

Composes an xUnit v3 theory row's parameters through Compono with a profile built, once per row,
from that row's own configuration \- the Composed theory rows counterpart to
[ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.XunitV3.ComposeAttribute_TProfile,TConfig_.md 'Compono\.XunitV3\.ComposeAttribute\<TProfile,TConfig\>')\. Rows are read from the same public static member
shape [ComposeDataAttribute](Compono.XunitV3.ComposeDataAttribute.md 'Compono\.XunitV3\.ComposeDataAttribute') documents\. Unlike
[ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.XunitV3.ComposeAttribute_TProfile,TConfig_.md 'Compono\.XunitV3\.ComposeAttribute\<TProfile,TConfig\>'), this attribute's own constructor argument is the
member name, not a config\-argument array: the configuration comes from the row itself \- element 0
of \<em\>every\</em\> row is that row's [TConfig](Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.md#Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.TConfig 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile,TConfig\>\.TConfig') \(a non\-null instance assignable
to [TConfig](Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.md#Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.TConfig 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile,TConfig\>\.TConfig')\), and elements 1\.\.n are that row's values\. See
`docs/adr/0070-xunitv3-composedata-composed-theory-rows.md`\.

```csharp
public sealed class ComposeDataAttribute<TProfile,TConfig> : Compono.XunitV3.ComposeDataAttribute
    where TProfile : Compono.ICompositionProfile
```
#### Type parameters

<a name='Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.TProfile'></a>

`TProfile`

The profile to construct and apply once per row from that row's [TConfig](Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.md#Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.TConfig 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile,TConfig\>\.TConfig')\. Must
have exactly one public constructor accepting exactly one [TConfig](Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.md#Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.TConfig 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile,TConfig\>\.TConfig')\-typed
parameter \- no `new()` constraint, matching [ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.XunitV3.ComposeAttribute_TProfile,TConfig_.md 'Compono\.XunitV3\.ComposeAttribute\<TProfile,TConfig\>')\.

<a name='Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.TConfig'></a>

`TConfig`

The configuration type each row supplies as its element 0\. Compono neither constructs it nor
resolves it from the profile; the row provides an already\-built, non\-null instance assignable to
[TConfig](Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.md#Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.TConfig 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile,TConfig\>\.TConfig')\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → `Xunit.v3.DataAttribute` → [ComposeDataAttribute](Compono.XunitV3.ComposeDataAttribute.md 'Compono\.XunitV3\.ComposeDataAttribute') → ComposeDataAttribute\<TProfile,TConfig\>

### Remarks
[TProfile](Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.md#Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.TProfile 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile,TConfig\>\.TProfile') and [TConfig](Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.md#Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.TConfig 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile,TConfig\>\.TConfig') both carry
            [System\.Diagnostics\.CodeAnalysis\.DynamicallyAccessedMembersAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.dynamicallyaccessedmembersattribute 'System\.Diagnostics\.CodeAnalysis\.DynamicallyAccessedMembersAttribute')\([System\.Diagnostics\.CodeAnalysis\.DynamicallyAccessedMemberTypes\.PublicConstructors](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.dynamicallyaccessedmembertypes.publicconstructors 'System\.Diagnostics\.CodeAnalysis\.DynamicallyAccessedMemberTypes\.PublicConstructors')\),
            matching [ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.XunitV3.ComposeAttribute_TProfile,TConfig_.md 'Compono\.XunitV3\.ComposeAttribute\<TProfile,TConfig\>')'s shape and the ADR\-0070 public API\. The
            annotation on [TProfile](Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.md#Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.TProfile 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile,TConfig\>\.TProfile') is load\-bearing: the per\-row profile is built by
            reflection \(`new TProfile(rowConfig)`\) and the trimmer strips a closed generic argument's
            public constructors without it\. [TConfig](Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.md#Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.TConfig 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile,TConfig\>\.TConfig') is never constructed by Compono \- the
            row supplies the instance \- so its annotation is carried for shape parity, not because Compono
            reflects over it\.

| Constructors | |
| :--- | :--- |
| [ComposeDataAttribute\(string\)](Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.ComposeDataAttribute(string).md 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile,TConfig\>\.ComposeDataAttribute\(string\)') | Creates a [ComposeDataAttribute&lt;TProfile,TConfig&gt;](Compono.XunitV3.ComposeDataAttribute_TProfile,TConfig_.md 'Compono\.XunitV3\.ComposeDataAttribute\<TProfile,TConfig\>')\. |
