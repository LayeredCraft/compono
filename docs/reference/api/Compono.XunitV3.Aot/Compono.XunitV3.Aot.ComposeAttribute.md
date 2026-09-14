#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot')

## ComposeAttribute Class

Marks a theory method's parameters for Compono composition under xUnit v3's Native AOT pipeline \-
the AOT counterpart to `Compono.XunitV3.ComposeAttribute`\. Usage is identical:

```csharp
[Theory]
[Compose]
public void My_test(Widget widget, string leaf) { ... }
```

```csharp
public sealed class ComposeAttribute : Xunit.v3.DataAttribute
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → `Xunit.v3.DataAttribute` → ComposeAttribute

### Remarks
Unlike `Compono.XunitV3.ComposeAttribute`, this attribute carries no runtime composition
logic of its own \- it is a thin marker, discovered by `Compono.Generators` at compile time
\(matched on this type's own fully qualified metadata name, per ADR\-0066/PLAN\-0066\) and never
invoked at runtime\. xUnit's Native AOT pipeline supplies theory data through a generator\-emitted
`RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)` registration instead of calling
`Xunit.v3.DataAttribute.GetData(System.Reflection.MethodInfo,Xunit.Sdk.DisposalTracker)` \- the same pattern xUnit's own official
`AotCsvDataSource`/`AotRetryFact`/`AotTraitExtensibility` samples use for custom
data\-attribute extensibility under Native AOT \(RESEARCH\-0032 §2\)\. Requires
`Compono.Generators` \(shipped inside the `Compono` package this package depends on\) to
actually produce that registration \- referencing this package alone, without its generator
dependency, leaves a `[Compose]`\-attributed method undiscovered by xUnit's AOT pipeline with
no compile error, since a marker attribute with no matching registration is, from the compiler's
perspective, indistinguishable from a marker attribute nobody generates anything for\.

Phase 1 scope (ADR-0066's Decision Outcome): this non-generic form itself supports plain
parameters only - no inline values, no `[Shared]`.

Deliberately [sealed](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/sealed 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/sealed') - unlike `Compono.XunitV3.ComposeAttribute` (whose
generic siblings genuinely extend its shared runtime state: a cached [Composer](../Compono/Compono.Composer.md 'Compono\.Composer'),
cached binding delegates, a real `GetData` override), this type carries no functional state
or behavior at all for a subtype to extend, and AOT discovery matches purely on each closed
attribute type's own fully qualified metadata name (never on assignability/inheritance - see
`AotComposeMethodDiscovery`'s remarks). Inheriting from this type would buy a consumer
nothing functionally while creating a real hazard specific to this marker-only attribute family:
a consumer-authored subclass would compile without error but never be discovered by
`Compono.Generators` (its own metadata name wouldn't match any registered discovery
provider), silently never running - exactly the failure mode ADR-0066's `CMP0040` exists to
prevent for every other unsupported shape. [ComposeAttribute&lt;TProfile&gt;](Compono.XunitV3.Aot.ComposeAttribute_TProfile_.md 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile\>') and
[ComposeAttribute&lt;TProfile,TConfig&gt;](Compono.XunitV3.Aot.ComposeAttribute_TProfile,TConfig_.md 'Compono\.XunitV3\.Aot\.ComposeAttribute\<TProfile,TConfig\>') (ADR-0067/PLAN-0067) are independent marker
siblings instead - same short type name and `[AttributeUsage]` convention, own direct
`Xunit.v3.DataAttribute` base, no inheritance relationship to this type (ADR-0067 Amendment 1).