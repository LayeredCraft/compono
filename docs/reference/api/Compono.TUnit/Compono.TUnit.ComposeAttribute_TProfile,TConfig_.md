#### [Compono\.TUnit](index.md 'index')
### [Compono\.TUnit](Compono.TUnit.md 'Compono\.TUnit')

## ComposeAttribute\<TProfile,TConfig\> Class

Composes a TUnit test method's parameters through Compono, applying a profile built from
\<em\>profile configuration arguments\</em\> known at this attribute's call site \- a distinct concept
from this attribute family's ordinary inline values \([ComposeAttribute\(object\[\]\)](Compono.TUnit.ComposeAttribute.ComposeAttribute(object[]).md 'Compono\.TUnit\.ComposeAttribute\.ComposeAttribute\(object\[\]\)')\),
which bind to the test method's own parameters instead\. This constructor never binds to the test
method's parameters at all; every one of them is composed in full\. [TConfig](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md#Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>\.TConfig') is
constructed positionally from this attribute's own constructor arguments, then
[TProfile](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md#Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TProfile 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>\.TProfile') is constructed from that [TConfig](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md#Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>\.TConfig') instance and
applied via [AddProfile\(ICompositionProfile\)](../Compono/Compono.CompositionBuilder.AddProfile.md#Compono.CompositionBuilder.AddProfile(Compono.ICompositionProfile) 'Compono\.CompositionBuilder\.AddProfile\(Compono\.ICompositionProfile\)') \- equivalent to
`Composer.Create(builder => builder.AddProfile(new TProfile(new TConfig(...))))`\. See
`docs/adr/0036-parameterized-composition-profile-selection.md` for the full design, including
why this exists as a separate attribute rather than overloading
[ComposeAttribute&lt;TProfile&gt;](Compono.TUnit.ComposeAttribute_TProfile_.md 'Compono\.TUnit\.ComposeAttribute\<TProfile\>')'s own inline\-value constructor argument\. Mirrors
`Compono.XunitV3.ComposeAttribute{TProfile, TConfig}` exactly\.

```csharp
public sealed class ComposeAttribute<TProfile,TConfig> : Compono.TUnit.ComposeAttribute
    where TProfile : Compono.ICompositionProfile
```
#### Type parameters

<a name='Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TProfile'></a>

`TProfile`

The profile to construct and apply\. Must have exactly one public constructor accepting exactly one
[TConfig](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md#Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>\.TConfig')\-typed parameter \- no `new()` constraint, unlike
[ComposeAttribute&lt;TProfile&gt;](Compono.TUnit.ComposeAttribute_TProfile_.md 'Compono\.TUnit\.ComposeAttribute\<TProfile\>'), since this form is never default\-constructed\.

<a name='Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TConfig'></a>

`TConfig`

The type this attribute's constructor arguments bind to, positionally, against its own single
public constructor\. Prefer strongly\-typed, attribute\-legal values for its constructor parameters \-
an [enum](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/enum 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/enum') for a finite choice, [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type') via `typeof(...)` for a CLR
type, a plain [bool](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/bool')/numeric/string value where that already carries the real
meaning \- over loosely\-typed primitives standing in for something more specific\.
`params object?[]` is a binding mechanism forced by C\#'s attribute\-argument\-must\-be\-a\-
compile\-time\-constant rule, not a license to design [TConfig](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md#Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>\.TConfig') around magic
strings\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → `TUnit.Core.AsyncUntypedDataSourceGeneratorAttribute` → `TUnit.Core.UntypedDataSourceGeneratorAttribute` → [ComposeAttribute](Compono.TUnit.ComposeAttribute.md 'Compono\.TUnit\.ComposeAttribute') → ComposeAttribute\<TProfile,TConfig\>

### Remarks
Unlike [ComposeAttribute&lt;TProfile&gt;](Compono.TUnit.ComposeAttribute_TProfile_.md 'Compono\.TUnit\.ComposeAttribute\<TProfile\>')'s compile\-time\-enforced `new()` constraint, an
unsupported [TConfig](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md#Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>\.TConfig')/[TProfile](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md#Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TProfile 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>\.TProfile') constructor shape \(not
exactly one public constructor on [TConfig](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md#Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>\.TConfig'); no exactly\-one\-
[TConfig](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md#Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>\.TConfig')\-parameter public constructor on [TProfile](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md#Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TProfile 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>\.TProfile')\) is a
deterministic runtime [CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException'), not a compile error \- there is no C\#
generic constraint that expresses "has a constructor accepting exactly this type\." Both constructor
lookups, and the actual construction, are reflection \(`Compono.TUnit.Binding.ConfigProfileBinder`\) \- bounded
and cached to once per attribute instance by this attribute family's existing
[System\.Lazy&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.lazy-1 'System\.Lazy\`1')\-backed [Composer](../Compono/Compono.Composer.md 'Compono\.Composer') caching \(`Compono.TUnit.ComposeAttribute&lt;&gt;.ApplyProfile(Compono.CompositionBuilder)` is only ever
invoked from inside that lazy initializer\), never on the repeated per\-row data\-source path\.
[TProfile](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md#Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TProfile 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>\.TProfile') and [TConfig](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md#Compono.TUnit.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>\.TConfig') both carry
[System\.Diagnostics\.CodeAnalysis\.DynamicallyAccessedMembersAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.dynamicallyaccessedmembersattribute 'System\.Diagnostics\.CodeAnalysis\.DynamicallyAccessedMembersAttribute')\([System\.Diagnostics\.CodeAnalysis\.DynamicallyAccessedMemberTypes\.PublicConstructors](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.dynamicallyaccessedmembertypes.publicconstructors 'System\.Diagnostics\.CodeAnalysis\.DynamicallyAccessedMemberTypes\.PublicConstructors')\)
\- required, not decorative: a real Native AOT publish\-and\-run proof \(ADR\-0041 Amendment 1\) showed
the trimmer strips a closed generic argument's public constructors by default, since nothing in an
unannotated `Type.GetConstructors()` call site tells it they're reachable \- `ConfigProfileBinder`
failed at runtime with "has 0" public constructors on a type that plainly has one, until these
annotations were added at every generic parameter/`Type`\-typed parameter along the call chain\.

| Constructors | |
| :--- | :--- |
| [ComposeAttribute\(object\[\]\)](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.ComposeAttribute(object[]).md 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>\.ComposeAttribute\(object\[\]\)') | Creates a [ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.TUnit.ComposeAttribute_TProfile,TConfig_.md 'Compono\.TUnit\.ComposeAttribute\<TProfile,TConfig\>')\. |
