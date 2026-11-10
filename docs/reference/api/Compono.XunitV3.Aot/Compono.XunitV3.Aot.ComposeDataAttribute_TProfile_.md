#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot')

## ComposeDataAttribute\<TProfile\> Class

Marks a theory method's rows for Compono composition under xUnit v3's Native AOT pipeline with
[TProfile](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.md#Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.TProfile 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile\>\.TProfile') applied \- the AOT counterpart to
`Compono.XunitV3.ComposeDataAttribute<TProfile>`\. Usage is identical:

```csharp
[Theory]
[ComposeData<MyProfile>(nameof(EmailCases))]
public void My_test(string address, DateTimeOffset sentAt) { ... }
```

```csharp
public sealed class ComposeDataAttribute<TProfile> : Xunit.v3.DataAttribute
    where TProfile : Compono.ICompositionProfile, new()
```
#### Type parameters

<a name='Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.TProfile'></a>

`TProfile`

The profile to apply\. The `new()` constraint mirrors
`Compono.XunitV3.ComposeDataAttribute<TProfile>`'s own designed shape\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → `Xunit.v3.DataAttribute` → ComposeDataAttribute\<TProfile\>

### Remarks
Marker\-only, and an independent sealed sibling of [ComposeDataAttribute](Compono.XunitV3.Aot.ComposeDataAttribute.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute') \- it derives
directly from `Xunit.v3.DataAttribute`, \<b\>not\</b\> from the non\-generic marker, and repeats the
[MemberName](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.MemberName.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile\>\.MemberName')/[MemberType](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.MemberType.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile\>\.MemberType')/[Seed](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.Seed.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile\>\.Seed') shape so the two engines expose
the same consumer\-facing declarations \(ADR\-0067 Amendment 1, extended by ADR\-0070\)\.

| Constructors | |
| :--- | :--- |
| [ComposeDataAttribute\(string\)](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.ComposeDataAttribute(string).md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile\>\.ComposeDataAttribute\(string\)') | Creates a [ComposeDataAttribute&lt;TProfile&gt;](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile\>')\. |

| Properties | |
| :--- | :--- |
| [MemberName](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.MemberName.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile\>\.MemberName') | The name of the public static member that supplies this theory's rows\. |
| [MemberType](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.MemberType.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile\>\.MemberType') | The type that declares [MemberName](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.MemberName.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile\>\.MemberName')\. [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') \(the default\) means the test class itself, matching xUnit's own `MemberData` convention\. |
| [Seed](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.Seed.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile\>\.Seed') | An explicit root seed pinned to every row this attribute produces \- the same contract as `Compono.XunitV3.ComposeDataAttribute.Seed`\. |
