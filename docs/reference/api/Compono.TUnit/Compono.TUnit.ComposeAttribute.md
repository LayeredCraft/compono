#### [Compono\.TUnit](index.md 'index')
### [Compono\.TUnit](Compono.TUnit.md 'Compono\.TUnit')

## ComposeAttribute Class

Composes a TUnit test method's parameters through Compono \- the default \(no explicit profile\)
entry point\. Every parameter not supplied inline is composed; a parameter targeted by a supplied
inline value takes that value instead, taking precedence over composition\. See
`docs/adr/0040-compono-tunit-package-design.md` for the full binding algorithm, seed policy,
and diagnostics \- adapted from `Compono.XunitV3.ComposeAttribute`, not a byte\-for\-byte port
\(TUnit hands a data source `TUnit.Core.DataGeneratorMetadata`, not a `MethodInfo`\)\.

```csharp
public class ComposeAttribute : TUnit.Core.UntypedDataSourceGeneratorAttribute, TUnit.Core.Interfaces.ITestDiscoveryEventReceiver, TUnit.Core.Interfaces.IEventReceiver
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → `TUnit.Core.AsyncUntypedDataSourceGeneratorAttribute` → `TUnit.Core.UntypedDataSourceGeneratorAttribute` → ComposeAttribute

Implements `TUnit.Core.Interfaces.ITestDiscoveryEventReceiver`, `TUnit.Core.Interfaces.IEventReceiver`

### Remarks
Deliberately unsealed \- `ComposeAttribute<TProfile>` and
`ComposeAttribute<TProfile, TConfig>` \(Phase 1\) are the two designed extension points,
mirroring `Compono.XunitV3`'s own family exactly\. Composition is deferred entirely into the
factory `Compono.TUnit.ComposeAttribute.GenerateDataSources(TUnit.Core.DataGeneratorMetadata)` returns \- never before TUnit actually invokes it,
matching TUnit's own "defer real work into the Func" convention
\(`DependencyInjectionDataSourceAttribute` does the same\)\.

| Constructors | |
| :--- | :--- |
| [ComposeAttribute\(object\[\]\)](Compono.TUnit.ComposeAttribute.ComposeAttribute(object[]).md 'Compono\.TUnit\.ComposeAttribute\.ComposeAttribute\(object\[\]\)') | Creates a [ComposeAttribute](Compono.TUnit.ComposeAttribute.md 'Compono\.TUnit\.ComposeAttribute')\. |

| Properties | |
| :--- | :--- |
| [Seed](Compono.TUnit.ComposeAttribute.Seed.md 'Compono\.TUnit\.ComposeAttribute\.Seed') | An explicit root seed for this row \- the same underlying contract as [WithSeed\(int\)](../Compono/Compono.CompositionBuilder.WithSeed(int).md 'Compono\.CompositionBuilder\.WithSeed\(System\.Int32\)'), but restricted to non\-negative values \(enforced in `Compono.TUnit.ComposeAttribute.GenerateDataSources(TUnit.Core.DataGeneratorMetadata)`\) so a seed reported via [OnTestDiscovered\(DiscoveredTestContext\)](Compono.TUnit.ComposeAttribute.OnTestDiscovered(TUnit.Core.DiscoveredTestContext).md 'Compono\.TUnit\.ComposeAttribute\.OnTestDiscovered\(TUnit\.Core\.DiscoveredTestContext\)') is always pasteable back into this property unchanged\. Unset: a fresh, non\-negative seed is generated for every row\. |

| Methods | |
| :--- | :--- |
| [OnTestDiscovered\(DiscoveredTestContext\)](Compono.TUnit.ComposeAttribute.OnTestDiscovered(TUnit.Core.DiscoveredTestContext).md 'Compono\.TUnit\.ComposeAttribute\.OnTestDiscovered\(TUnit\.Core\.DiscoveredTestContext\)') | Called when a test is discovered during the test discovery phase\. |
