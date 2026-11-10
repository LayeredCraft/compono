#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot')

## ComposeDataAttribute Class

Marks a theory method's rows for Compono composition under xUnit v3's Native AOT pipeline, reading
them from a public static field, property, or parameterless method \- the AOT counterpart to
`Compono.XunitV3.ComposeDataAttribute`\. Usage is identical:

```csharp
[Theory]
[ComposeData(nameof(EmailCases))]
public void My_test(string address, DateTimeOffset sentAt) { ... }
```

```csharp
public sealed class ComposeDataAttribute : Xunit.v3.DataAttribute
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → `Xunit.v3.DataAttribute` → ComposeDataAttribute

### Remarks
Marker\-only, like [ComposeAttribute](Compono.XunitV3.Aot.ComposeAttribute.md 'Compono\.XunitV3\.Aot\.ComposeAttribute') \- discovered by `Compono.Generators` at
compile time \(matched on this type's own fully qualified metadata name, per ADR\-0070\) and never
invoked at runtime\. xUnit's Native AOT pipeline supplies theory data through a generator\-emitted
`RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)` registration instead of calling
`Xunit.v3.DataAttribute.GetData(System.Reflection.MethodInfo,Xunit.Sdk.DisposalTracker)`\.

Deliberately [sealed](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/sealed 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/sealed'), and [ComposeDataAttribute&lt;TProfile&gt;](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile\>') /
[ComposeDataAttribute&lt;TProfile,TConfig&gt;](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile,TConfig_.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile,TConfig\>') are independent marker siblings deriving
directly from `Xunit.v3.DataAttribute` rather than from this type - the same ADR-0067 Amendment 1
pattern the [ComposeAttribute](Compono.XunitV3.Aot.ComposeAttribute.md 'Compono\.XunitV3\.Aot\.ComposeAttribute') family uses. AOT discovery matches purely on each closed
attribute type's own metadata name (never on inheritance), so deriving from this type would buy a
consumer nothing while opening an undiscovered-subclass hazard.

| Constructors | |
| :--- | :--- |
| [ComposeDataAttribute\(string\)](Compono.XunitV3.Aot.ComposeDataAttribute.ComposeDataAttribute(string).md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\.ComposeDataAttribute\(string\)') | Creates a [ComposeDataAttribute](Compono.XunitV3.Aot.ComposeDataAttribute.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute')\. |

| Properties | |
| :--- | :--- |
| [MemberName](Compono.XunitV3.Aot.ComposeDataAttribute.MemberName.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\.MemberName') | The name of the public static member that supplies this theory's rows\. |
| [MemberType](Compono.XunitV3.Aot.ComposeDataAttribute.MemberType.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\.MemberType') | The type that declares [MemberName](Compono.XunitV3.Aot.ComposeDataAttribute.MemberName.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\.MemberName')\. [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') \(the default\) means the test class itself, matching xUnit's own `MemberData` convention\. |
| [Seed](Compono.XunitV3.Aot.ComposeDataAttribute.Seed.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\.Seed') | An explicit root seed pinned to every row this attribute produces \- the same contract as `Compono.XunitV3.ComposeDataAttribute.Seed`\. Never read at runtime; the generator resolves it from this attribute's use site at compile time\. |
