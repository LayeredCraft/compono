#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot')

## ComposeDataAttribute\<TProfile,TConfig\> Class

Marks a theory method's rows for Compono composition under xUnit v3's Native AOT pipeline with a
profile built from configuration supplied by each row \- the AOT counterpart to
`Compono.XunitV3.ComposeDataAttribute<TProfile, TConfig>`\. Usage is identical:

```csharp
[Theory]
[ComposeData<PricingProfile, ClockConfig>(nameof(PricingCases))]
public void My_test(decimal price) { ... }
```

```csharp
public sealed class ComposeDataAttribute<TProfile,TConfig> : Xunit.v3.DataAttribute
    where TProfile : Compono.ICompositionProfile
```
#### Type parameters

<a name='Compono.XunitV3.Aot.ComposeDataAttribute_TProfile,TConfig_.TProfile'></a>

`TProfile`

The profile to construct and apply\. Must have exactly one public constructor accepting exactly one
[TConfig](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile,TConfig_.md#Compono.XunitV3.Aot.ComposeDataAttribute_TProfile,TConfig_.TConfig 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile,TConfig\>\.TConfig')\-typed parameter \- no `new()` constraint, matching
`Compono.XunitV3.ComposeDataAttribute<TProfile, TConfig>`\.

<a name='Compono.XunitV3.Aot.ComposeDataAttribute_TProfile,TConfig_.TConfig'></a>

`TConfig`

The profile's configuration type, supplied per row rather than bound positionally from attribute
arguments\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → `Xunit.v3.DataAttribute` → ComposeDataAttribute\<TProfile,TConfig\>

### Remarks
Marker\-only, and an independent sealed sibling of [ComposeDataAttribute](Compono.XunitV3.Aot.ComposeDataAttribute.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute') \- it derives
directly from `Xunit.v3.DataAttribute`, \<b\>not\</b\> from the non\-generic marker, and repeats the
[MemberName](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile,TConfig_.MemberName.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile,TConfig\>\.MemberName')/[MemberType](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile,TConfig_.MemberType.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile,TConfig\>\.MemberType')/[Seed](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile,TConfig_.Seed.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile,TConfig\>\.Seed') shape so the two engines expose
the same consumer\-facing declarations \(ADR\-0067 Amendment 1, extended by ADR\-0070\)\.

| Constructors | |
| :--- | :--- |
| [ComposeDataAttribute\(string\)](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile,TConfig_.ComposeDataAttribute(string).md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile,TConfig\>\.ComposeDataAttribute\(string\)') | Creates a [ComposeDataAttribute&lt;TProfile,TConfig&gt;](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile,TConfig_.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile,TConfig\>')\. |

| Properties | |
| :--- | :--- |
| [MemberName](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile,TConfig_.MemberName.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile,TConfig\>\.MemberName') | The name of the public static member that supplies this theory's rows\. |
| [MemberType](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile,TConfig_.MemberType.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile,TConfig\>\.MemberType') | The type that declares [MemberName](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile,TConfig_.MemberName.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile,TConfig\>\.MemberName')\. [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') \(the default\) means the test class itself, matching xUnit's own `MemberData` convention\. |
| [Seed](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile,TConfig_.Seed.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile,TConfig\>\.Seed') | An explicit root seed pinned to every row this attribute produces \- the same contract as `Compono.XunitV3.ComposeDataAttribute.Seed`\. |
