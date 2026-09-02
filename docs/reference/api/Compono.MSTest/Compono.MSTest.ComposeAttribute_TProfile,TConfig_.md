#### [Compono\.MSTest](index.md 'index')
### [Compono\.MSTest](Compono.MSTest.md 'Compono\.MSTest')

## ComposeAttribute\<TProfile,TConfig\> Class

Composes an MSTest data\-driven test method's parameters through Compono, applying a profile
built from \<em\>profile configuration arguments\</em\> known at this attribute's call site \- a
distinct concept from this attribute family's ordinary inline values
\([ComposeAttribute\(object\[\]\)](Compono.MSTest.ComposeAttribute.ComposeAttribute(object[]).md 'Compono\.MSTest\.ComposeAttribute\.ComposeAttribute\(object\[\]\)')\), which bind to the test method's own parameters
instead\. This constructor never binds to the test method's parameters at all; every one of them
is composed in full\. [TConfig](Compono.MSTest.ComposeAttribute_TProfile,TConfig_.md#Compono.MSTest.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.MSTest\.ComposeAttribute\<TProfile,TConfig\>\.TConfig') is constructed positionally from this
attribute's own constructor arguments, then [TProfile](Compono.MSTest.ComposeAttribute_TProfile,TConfig_.md#Compono.MSTest.ComposeAttribute_TProfile,TConfig_.TProfile 'Compono\.MSTest\.ComposeAttribute\<TProfile,TConfig\>\.TProfile') is constructed from
that [TConfig](Compono.MSTest.ComposeAttribute_TProfile,TConfig_.md#Compono.MSTest.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.MSTest\.ComposeAttribute\<TProfile,TConfig\>\.TConfig') instance and applied via
[AddProfile\(ICompositionProfile\)](../Compono/Compono.CompositionBuilder.AddProfile.md#Compono.CompositionBuilder.AddProfile(Compono.ICompositionProfile) 'Compono\.CompositionBuilder\.AddProfile\(Compono\.ICompositionProfile\)') \- the same Compono\-facing
attribute family and semantics as `Compono.XunitV3.ComposeAttribute{TProfile,TConfig}`/
`Compono.TUnit`'s own equivalent overload \(ADR\-0036\); no MSTest\-specific profile/
configuration shape is introduced\.

```csharp
public sealed class ComposeAttribute<TProfile,TConfig> : Compono.MSTest.ComposeAttribute
    where TProfile : Compono.ICompositionProfile
```
#### Type parameters

<a name='Compono.MSTest.ComposeAttribute_TProfile,TConfig_.TProfile'></a>

`TProfile`

The profile to construct and apply\. Must have exactly one public constructor accepting exactly
one [TConfig](Compono.MSTest.ComposeAttribute_TProfile,TConfig_.md#Compono.MSTest.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.MSTest\.ComposeAttribute\<TProfile,TConfig\>\.TConfig')\-typed parameter \- no `new()` constraint, unlike
[ComposeAttribute&lt;TProfile&gt;](Compono.MSTest.ComposeAttribute_TProfile_.md 'Compono\.MSTest\.ComposeAttribute\<TProfile\>'), since this form is never default\-constructed\.

<a name='Compono.MSTest.ComposeAttribute_TProfile,TConfig_.TConfig'></a>

`TConfig`

The type this attribute's constructor arguments bind to, positionally, against its own single
public constructor\. Prefer strongly\-typed, attribute\-legal values for its constructor
parameters over loosely\-typed primitives standing in for something more specific\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → [ComposeAttribute](Compono.MSTest.ComposeAttribute.md 'Compono\.MSTest\.ComposeAttribute') → ComposeAttribute\<TProfile,TConfig\>

### Remarks
Unlike [ComposeAttribute&lt;TProfile&gt;](Compono.MSTest.ComposeAttribute_TProfile_.md 'Compono\.MSTest\.ComposeAttribute\<TProfile\>')'s compile\-time\-enforced `new()` constraint,
an unsupported [TConfig](Compono.MSTest.ComposeAttribute_TProfile,TConfig_.md#Compono.MSTest.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.MSTest\.ComposeAttribute\<TProfile,TConfig\>\.TConfig')/[TProfile](Compono.MSTest.ComposeAttribute_TProfile,TConfig_.md#Compono.MSTest.ComposeAttribute_TProfile,TConfig_.TProfile 'Compono\.MSTest\.ComposeAttribute\<TProfile,TConfig\>\.TProfile') constructor shape
is a deterministic runtime [CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException'), not a compile error\. Both
constructor lookups, and the actual construction, are reflection \(`Compono.MSTest.Binding.ConfigProfileBinder`\)
\- bounded and cached to once per attribute instance by this attribute family's existing
[System\.Lazy&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.lazy-1 'System\.Lazy\`1')\-backed [Composer](../Compono/Compono.Composer.md 'Compono\.Composer') caching \(`Compono.MSTest.ComposeAttribute&lt;&gt;.ApplyProfile(Compono.CompositionBuilder)` is only
ever invoked from inside that lazy initializer\), never on the repeated per\-row `GetData` path\.

| Constructors | |
| :--- | :--- |
| [ComposeAttribute\(object\[\]\)](Compono.MSTest.ComposeAttribute_TProfile,TConfig_.ComposeAttribute(object[]).md 'Compono\.MSTest\.ComposeAttribute\<TProfile,TConfig\>\.ComposeAttribute\(object\[\]\)') | Creates a [ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.MSTest.ComposeAttribute_TProfile,TConfig_.md 'Compono\.MSTest\.ComposeAttribute\<TProfile,TConfig\>')\. |
