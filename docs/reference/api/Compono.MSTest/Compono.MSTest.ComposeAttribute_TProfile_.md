#### [Compono\.MSTest](index.md 'index')
### [Compono\.MSTest](Compono.MSTest.md 'Compono\.MSTest')

## ComposeAttribute\<TProfile\> Class

Composes an MSTest data\-driven test method's parameters through Compono, applying
[TProfile](Compono.MSTest.ComposeAttribute_TProfile_.md#Compono.MSTest.ComposeAttribute_TProfile_.TProfile 'Compono\.MSTest\.ComposeAttribute\<TProfile\>\.TProfile') \- matching `Compono.XunitV3`/`Compono.TUnit`'s own
identical generic form exactly \(same Compono\-facing attribute family and semantics\)\.

```csharp
public sealed class ComposeAttribute<TProfile> : Compono.MSTest.ComposeAttribute
    where TProfile : Compono.ICompositionProfile, new()
```
#### Type parameters

<a name='Compono.MSTest.ComposeAttribute_TProfile_.TProfile'></a>

`TProfile`

The profile to apply, via [AddProfile&lt;TProfile&gt;\(\)](../Compono/Compono.CompositionBuilder.AddProfile.md#Compono.CompositionBuilder.AddProfile_TProfile_() 'Compono\.CompositionBuilder\.AddProfile\`\`1')\. Default\-
constructed \- see [ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.MSTest.ComposeAttribute_TProfile,TConfig_.md 'Compono\.MSTest\.ComposeAttribute\<TProfile,TConfig\>') for the profile\-
configuration\-argument form instead\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → [ComposeAttribute](Compono.MSTest.ComposeAttribute.md 'Compono\.MSTest\.ComposeAttribute') → ComposeAttribute\<TProfile\>

| Constructors | |
| :--- | :--- |
| [ComposeAttribute\(object\[\]\)](Compono.MSTest.ComposeAttribute_TProfile_.ComposeAttribute(object[]).md 'Compono\.MSTest\.ComposeAttribute\<TProfile\>\.ComposeAttribute\(object\[\]\)') | Creates a [ComposeAttribute&lt;TProfile&gt;](Compono.MSTest.ComposeAttribute_TProfile_.md 'Compono\.MSTest\.ComposeAttribute\<TProfile\>')\. |
