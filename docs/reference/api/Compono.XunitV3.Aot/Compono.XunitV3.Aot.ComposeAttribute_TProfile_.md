#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot')

## ComposeAttribute\<TProfile\> Class

Composes an xUnit v3 theory row's parameters through Compono under xUnit v3's Native AOT pipeline,
with [TProfile](Compono.XunitV3.Aot.ComposeAttribute_TProfile_.md#Compono.XunitV3.Aot.ComposeAttribute_TProfile_.TProfile 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile\>\.TProfile') applied to the underlying [Composer](../Compono/Compono.Composer.md 'Compono\.Composer') \- the AOT
counterpart to `Compono.XunitV3.ComposeAttribute<TProfile>`\. Usage is identical:

```csharp
[Theory]
[Compose<MyProfile>]
public void My_test(Widget widget) { ... }
```

```csharp
public sealed class ComposeAttribute<TProfile> : Xunit.v3.DataAttribute
    where TProfile : Compono.ICompositionProfile, new()
```
#### Type parameters

<a name='Compono.XunitV3.Aot.ComposeAttribute_TProfile_.TProfile'></a>

`TProfile`

The profile to apply\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → `Xunit.v3.DataAttribute` → ComposeAttribute\<TProfile\>

### Remarks
Marker\-only, like the non\-generic [ComposeAttribute](Compono.XunitV3.Aot.ComposeAttribute.md 'Compono\.XunitV3\.Aot\.ComposeAttribute') \- discovered by
`Compono.Generators` at compile time \(matched on this type's own arity\-suffixed fully
qualified metadata name, `Compono.XunitV3.Aot.ComposeAttribute`1`, per
ADR\-0067/PLAN\-0067\) and never invoked at runtime\. The generated
`RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)` registration constructs
[TProfile](Compono.XunitV3.Aot.ComposeAttribute_TProfile_.md#Compono.XunitV3.Aot.ComposeAttribute_TProfile_.TProfile 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile\>\.TProfile') via a direct, compile\-time\-closed
`global::Compono.Composer.Create(b => b.AddProfile<TProfile>())` call \- the identical
reflection\-free construction [AddProfile&lt;TProfile&gt;\(\)](../Compono/Compono.CompositionBuilder.AddProfile.md#Compono.CompositionBuilder.AddProfile_TProfile_() 'Compono\.CompositionBuilder\.AddProfile\`\`1') already uses in
both JIT and AOT modes today \(a bare `new TProfile()` against a compile\-time\-closed generic
argument, never `Activator`/reflection\)\.

A profile type that doesn't implement [ICompositionProfile](../Compono/Compono.ICompositionProfile.md 'Compono\.ICompositionProfile') or lacks a public
parameterless constructor is a compile error at the `[Compose<TProfile>]` use site (C#
enforces generic-attribute constraints there like any other generic type) - there is no compile-time
diagnostic or runtime check to design for that case, identical to
`Compono.XunitV3.ComposeAttribute<TProfile>`'s own remarks.

The `new()` constraint guarantees [TProfile](Compono.XunitV3.Aot.ComposeAttribute_TProfile_.md#Compono.XunitV3.Aot.ComposeAttribute_TProfile_.TProfile 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile\>\.TProfile') has a public parameterless
constructor, but not that calling it is Native-AOT/trim-safe: `CMP0049` rejects a
[TProfile](Compono.XunitV3.Aot.ComposeAttribute_TProfile_.md#Compono.XunitV3.Aot.ComposeAttribute_TProfile_.TProfile 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile\>\.TProfile') whose parameterless constructor is marked
`[RequiresDynamicCode]`, `[RequiresUnreferencedCode]`, or `[RequiresAssemblyFiles]` -
the generated `AddProfile<TProfile>()` call closes that generic `new TProfile()`
construction over the real type at the generated call site, which would otherwise surface the
corresponding `IL3050`/`IL2026`/`IL3002` warning for any consumer with trim/AOT
analysis enabled (ADR-0067 Amendment 2).

Derives directly from `Xunit.v3.DataAttribute`, <b>not</b> from the non-generic
[ComposeAttribute](Compono.XunitV3.Aot.ComposeAttribute.md 'Compono\.XunitV3\.Aot\.ComposeAttribute') - that type is deliberately [sealed](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/sealed 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/sealed') (see its own
remarks) since AOT discovery matches purely by metadata name, never by inheritance, so sharing a
base class here would buy nothing while opening an undiscovered-subclass hazard specific to this
marker-only attribute family (ADR-0067 Amendment 1).