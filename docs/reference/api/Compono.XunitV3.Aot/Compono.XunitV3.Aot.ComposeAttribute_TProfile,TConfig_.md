#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot')

## ComposeAttribute\<TProfile,TConfig\> Class

Composes an xUnit v3 theory row's parameters through Compono under xUnit v3's Native AOT pipeline,
applying a profile built from \<em\>profile configuration arguments\</em\> known at this attribute's
call site \- the AOT counterpart to
`Compono.XunitV3.ComposeAttribute<TProfile, TConfig>`\. Usage is identical:

```csharp
[Theory]
[Compose<MyProfile, MyConfig>(MyConfigValue.Foo)]
public void My_test(Widget widget) { ... }
```

```csharp
public sealed class ComposeAttribute<TProfile,TConfig> : Xunit.v3.DataAttribute
    where TProfile : Compono.ICompositionProfile
```
#### Type parameters

<a name='Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.TProfile'></a>

`TProfile`

The profile to construct and apply\. Must have exactly one public constructor accepting exactly one
[TConfig](Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.md#Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile,TConfig\>\.TConfig')\-typed parameter \- no `new()` constraint, matching
`Compono.XunitV3.ComposeAttribute<TProfile, TConfig>`'s own type\-level remarks\.

<a name='Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.TConfig'></a>

`TConfig`

The type this attribute's constructor arguments bind to, positionally, against its own single
public constructor\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → `Xunit.v3.DataAttribute` → ComposeAttribute\<TProfile,TConfig\>

### Remarks
Marker\-only, like the non\-generic [ComposeAttribute](Compono.XunitV3.Aot.ComposeAttribute.md 'Compono\.XunitV3\.Aot\.ComposeAttribute') \- discovered by
`Compono.Generators` at compile time \(matched on this type's own arity\-suffixed fully
qualified metadata name, `Compono.XunitV3.Aot.ComposeAttribute`2`, per ADR\-0067/PLAN\-0067\) and
never invoked at runtime\. Unlike
`Compono.XunitV3.ComposeAttribute<TProfile, TConfig>`, the shape checks that attribute
performs at runtime via `ConfigProfileBinder` reflection \([TConfig](Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.md#Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile,TConfig\>\.TConfig') has
exactly one public constructor; [TProfile](Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.md#Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.TProfile 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile,TConfig\>\.TProfile') has exactly one public constructor
accepting exactly one [TConfig](Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.md#Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile,TConfig\>\.TConfig')\-typed parameter; the supplied constructor
arguments match that constructor's parameters\) are performed by `Compono.Generators` at
\<em\>compile time\</em\> instead, against the real declared symbols and this attribute's own
compile\-time\-constant constructor arguments \(C\#'s attribute\-argument rule guarantees they're
constants\) \- see `CMP0041`/`CMP0042`/`CMP0043`\. On success, the generated
registration constructs [TConfig](Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.md#Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.TConfig 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile,TConfig\>\.TConfig') and [TProfile](Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.md#Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.TProfile 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile,TConfig\>\.TProfile') via
direct `new` calls with the literal argument values rendered back into source \- no
[System\.Type\.GetConstructors](https://learn.microsoft.com/en-us/dotnet/api/system.type.getconstructors 'System\.Type\.GetConstructors'), no [System\.Reflection\.ConstructorInfo\.Invoke\(System\.Object\[\]\)](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.constructorinfo.invoke#system-reflection-constructorinfo-invoke(system-object[]) 'System\.Reflection\.ConstructorInfo\.Invoke\(System\.Object\[\]\)'),
no [System\.Diagnostics\.CodeAnalysis\.DynamicallyAccessedMembersAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.dynamicallyaccessedmembersattribute 'System\.Diagnostics\.CodeAnalysis\.DynamicallyAccessedMembersAttribute') annotation
anywhere in this path \(ADR\-0067\)\.

This constructor arguments never bind to the test method's own parameters - every parameter is
composed in full, identical to `Compono.XunitV3.ComposeAttribute<TProfile, TConfig>`'s
own binding contract.

Derives directly from `Xunit.v3.DataAttribute`, <b>not</b> from the non-generic
[ComposeAttribute](Compono.XunitV3.Aot.ComposeAttribute.md 'Compono\.XunitV3\.Aot\.ComposeAttribute') - see [ComposeAttribute&lt;TProfile&gt;](Compono.XunitV3.Aot.ComposeAttribute_TProfile_.md 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile\>')'s identical remarks
for why (ADR-0067 Amendment 1).

| Constructors | |
| :--- | :--- |
| [ComposeAttribute\(object\[\]\)](Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.ComposeAttribute(object[]).md 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile,TConfig\>\.ComposeAttribute\(object\[\]\)') | Creates a [ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.md 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile,TConfig\>')\. |
