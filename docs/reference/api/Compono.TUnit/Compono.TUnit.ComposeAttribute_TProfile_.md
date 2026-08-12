#### [Compono\.TUnit](index.md 'index')
### [Compono\.TUnit](Compono.TUnit.md 'Compono\.TUnit')

## ComposeAttribute\<TProfile\> Class

Composes a TUnit test method's parameters through Compono, with [TProfile](Compono.TUnit.ComposeAttribute_TProfile_.md#Compono.TUnit.ComposeAttribute_TProfile_.TProfile 'Compono\.TUnit\.ComposeAttribute\<TProfile\>\.TProfile')
applied to the underlying [Composer](../Compono/Compono.Composer.md 'Compono\.Composer') \- equivalent to
`Composer.Create(builder => builder.AddProfile<TProfile>())`\. See
[ComposeAttribute](Compono.TUnit.ComposeAttribute.md 'Compono\.TUnit\.ComposeAttribute') for the full binding algorithm\.

```csharp
public sealed class ComposeAttribute<TProfile> : Compono.TUnit.ComposeAttribute
    where TProfile : Compono.ICompositionProfile, new()
```
#### Type parameters

<a name='Compono.TUnit.ComposeAttribute_TProfile_.TProfile'></a>

`TProfile`

The profile to apply\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → `TUnit.Core.AsyncUntypedDataSourceGeneratorAttribute` → `TUnit.Core.UntypedDataSourceGeneratorAttribute` → [ComposeAttribute](Compono.TUnit.ComposeAttribute.md 'Compono\.TUnit\.ComposeAttribute') → ComposeAttribute\<TProfile\>

### Remarks
A profile type that doesn't implement [ICompositionProfile](../Compono/Compono.ICompositionProfile.md 'Compono\.ICompositionProfile') or lacks a public
parameterless constructor is a compile error at the `[Compose<TProfile>]` use site
\(C\# enforces generic\-attribute constraints there like any other generic type\) \- there is no
runtime "invalid profile type" diagnostic to design\. Mirrors
`Compono.XunitV3.ComposeAttribute{TProfile}` exactly\.

| Constructors | |
| :--- | :--- |
| [ComposeAttribute\(object\[\]\)](Compono.TUnit.ComposeAttribute_TProfile_.ComposeAttribute(object[]).md 'Compono\.TUnit\.ComposeAttribute\<TProfile\>\.ComposeAttribute\(object\[\]\)') | Creates a [ComposeAttribute&lt;TProfile&gt;](Compono.TUnit.ComposeAttribute_TProfile_.md 'Compono\.TUnit\.ComposeAttribute\<TProfile\>')\. |
