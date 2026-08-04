#### [Compono\.XunitV3](index.md 'index')
### [Compono\.XunitV3](Compono.XunitV3.md 'Compono\.XunitV3')

## ComposeAttribute\<TProfile\> Class

Composes an xUnit v3 theory row's parameters through Compono, with [TProfile](Compono.XunitV3.ComposeAttribute_TProfile_.md#Compono.XunitV3.ComposeAttribute_TProfile_.TProfile 'Compono\.XunitV3\.ComposeAttribute\<TProfile\>\.TProfile')
applied to the underlying [Composer](../Compono/Compono.Composer.md 'Compono\.Composer') \- equivalent to
`Composer.Create(builder => builder.AddProfile<TProfile>())`\. See
[ComposeAttribute](Compono.XunitV3.ComposeAttribute.md 'Compono\.XunitV3\.ComposeAttribute') for the full binding algorithm\.

```csharp
public sealed class ComposeAttribute<TProfile> : Compono.XunitV3.ComposeAttribute
    where TProfile : Compono.ICompositionProfile, new()
```
#### Type parameters

<a name='Compono.XunitV3.ComposeAttribute_TProfile_.TProfile'></a>

`TProfile`

The profile to apply\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → [Xunit\.v3\.DataAttribute](https://learn.microsoft.com/en-us/dotnet/api/xunit.v3.dataattribute 'Xunit\.v3\.DataAttribute') → [ComposeAttribute](Compono.XunitV3.ComposeAttribute.md 'Compono\.XunitV3\.ComposeAttribute') → ComposeAttribute\<TProfile\>

### Remarks
A profile type that doesn't implement [ICompositionProfile](../Compono/Compono.ICompositionProfile.md 'Compono\.ICompositionProfile') or lacks a public
parameterless constructor is a compile error at the `[Compose<TProfile>]` use site
\(C\# enforces generic\-attribute constraints there like any other generic type\) \- there is no
runtime "invalid profile type" diagnostic to design\.

| Constructors | |
| :--- | :--- |
| [ComposeAttribute\(object\[\]\)](Compono.XunitV3.ComposeAttribute_TProfile_.ComposeAttribute(object[]).md 'Compono\.XunitV3\.ComposeAttribute\<TProfile\>\.ComposeAttribute\(object\[\]\)') | Creates a [ComposeAttribute&lt;TProfile&gt;](Compono.XunitV3.ComposeAttribute_TProfile_.md 'Compono\.XunitV3\.ComposeAttribute\<TProfile\>')\. |
