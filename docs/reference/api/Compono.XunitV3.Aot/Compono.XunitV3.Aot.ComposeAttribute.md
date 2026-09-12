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

Phase 1 scope (ADR-0066's Decision Outcome): plain parameters only - no inline values, no
`[Shared]`, no profile variants (`[Compose<TProfile>]`/
`[Compose<TProfile, TConfig>]`). This attribute's parameterless-only constructor and
lack of generic siblings enforce that scope structurally: there is no supported syntax to attempt
any of those forms with this package's current public surface.