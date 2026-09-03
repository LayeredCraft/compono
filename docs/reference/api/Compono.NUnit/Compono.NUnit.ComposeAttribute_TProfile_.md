#### [Compono\.NUnit](index.md 'index')
### [Compono\.NUnit](Compono.NUnit.md 'Compono\.NUnit')

## ComposeAttribute\<TProfile\> Class

Composes an NUnit test method's parameters through Compono, applying
[TProfile](Compono.NUnit.ComposeAttribute_TProfile_.md#Compono.NUnit.ComposeAttribute_TProfile_.TProfile 'Compono\.NUnit\.ComposeAttribute\<TProfile\>\.TProfile') \- matching `Compono.XunitV3`/`Compono.TUnit`/
`Compono.MSTest`'s own identical generic form exactly \(same Compono\-facing attribute family
and semantics\)\.

```csharp
public sealed class ComposeAttribute<TProfile> : Compono.NUnit.ComposeAttribute
    where TProfile : Compono.ICompositionProfile, new()
```
#### Type parameters

<a name='Compono.NUnit.ComposeAttribute_TProfile_.TProfile'></a>

`TProfile`

The profile to apply, via [AddProfile&lt;TProfile&gt;\(\)](../Compono/Compono.CompositionBuilder.AddProfile.md#Compono.CompositionBuilder.AddProfile_TProfile_() 'Compono\.CompositionBuilder\.AddProfile\`\`1')\. Default\-
constructed \- see [ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.NUnit.ComposeAttribute_TProfile,TConfig_.md 'Compono\.NUnit\.ComposeAttribute\<TProfile,TConfig\>') for the profile\-
configuration\-argument form instead\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → `NUnit.Framework.NUnitAttribute` → `NUnit.Framework.TestAttribute` → [ComposeAttribute](Compono.NUnit.ComposeAttribute.md 'Compono\.NUnit\.ComposeAttribute') → ComposeAttribute\<TProfile\>

| Constructors | |
| :--- | :--- |
| [ComposeAttribute\(object\[\]\)](Compono.NUnit.ComposeAttribute_TProfile_.ComposeAttribute(object[]).md 'Compono\.NUnit\.ComposeAttribute\<TProfile\>\.ComposeAttribute\(object\[\]\)') | Creates a [ComposeAttribute&lt;TProfile&gt;](Compono.NUnit.ComposeAttribute_TProfile_.md 'Compono\.NUnit\.ComposeAttribute\<TProfile\>')\. |
