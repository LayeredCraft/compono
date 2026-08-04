#### [Compono\.XunitV3](index.md 'index')
### [Compono\.XunitV3](Compono.XunitV3.md 'Compono\.XunitV3').[ComposeAttribute](Compono.XunitV3.ComposeAttribute.md 'Compono\.XunitV3\.ComposeAttribute')

## ComposeAttribute\.GetData\(MethodInfo, DisposalTracker\) Method

Composes \(or applies inline values to\) one theory row's parameters\. See ADR\-0022's
"Inline/composed binding algorithm" section for the full step\-by\-step behavior this
implements\.

```csharp
public override System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyCollection<Xunit.ITheoryDataRow>> GetData(System.Reflection.MethodInfo testMethod, Xunit.Sdk.DisposalTracker disposalTracker);
```
#### Parameters

<a name='Compono.XunitV3.ComposeAttribute.GetData(System.Reflection.MethodInfo,Xunit.Sdk.DisposalTracker).testMethod'></a>

`testMethod` [System\.Reflection\.MethodInfo](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.methodinfo 'System\.Reflection\.MethodInfo')

<a name='Compono.XunitV3.ComposeAttribute.GetData(System.Reflection.MethodInfo,Xunit.Sdk.DisposalTracker).disposalTracker'></a>

`disposalTracker` [Xunit\.Sdk\.DisposalTracker](https://learn.microsoft.com/en-us/dotnet/api/xunit.sdk.disposaltracker 'Xunit\.Sdk\.DisposalTracker')

Implements [GetData\(MethodInfo, DisposalTracker\)](https://learn.microsoft.com/en-us/dotnet/api/xunit.v3.idataattribute.getdata#xunit-v3-idataattribute-getdata(system-reflection-methodinfo-xunit-sdk-disposaltracker) 'Xunit\.v3\.IDataAttribute\.GetData\(System\.Reflection\.MethodInfo,Xunit\.Sdk\.DisposalTracker\)')

#### Returns
[System\.Threading\.Tasks\.ValueTask&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1 'System\.Threading\.Tasks\.ValueTask\`1')[System\.Collections\.Generic\.IReadOnlyCollection&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')[Xunit\.ITheoryDataRow](https://learn.microsoft.com/en-us/dotnet/api/xunit.itheorydatarow 'Xunit\.ITheoryDataRow')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1 'System\.Threading\.Tasks\.ValueTask\`1')

#### Exceptions

[CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')  
This attribute's configured seed \(or a profile\-configured one\) is negative; the test method's
signature is unsupported \(a generic method, a `ref`/`out`/`in`/`params`
parameter, more than one Compose\-family attribute, or more than one `[Shared]` parameter
of the same type\); too many inline values were supplied; a supplied inline value is
[null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a non\-nullable parameter or has a type not assignable to its
parameter; or composition itself fails for a parameter \- a new [CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')
propagates whose [System\.Exception\.Message](https://learn.microsoft.com/en-us/dotnet/api/system.exception.message 'System\.Exception\.Message') is the pipeline's original message with the
row's seed appended and whose [Diagnostic](../Compono/Compono.CompositionException.Diagnostic.md 'Compono\.CompositionException\.Diagnostic') is copied through
from that original unchanged \([null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') if the original had none\)\. This new
exception's own [System\.Exception\.InnerException](https://learn.microsoft.com/en-us/dotnet/api/system.exception.innerexception 'System\.Exception\.InnerException') is the pipeline's original
[CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException') itself, not that original's [System\.Exception\.InnerException](https://learn.microsoft.com/en-us/dotnet/api/system.exception.innerexception 'System\.Exception\.InnerException')
\- so a provider failure's chain is wrapper → original composition exception → the provider's
own thrown exception, one level deeper than the original throw\.

### Remarks
[disposalTracker](Compono.XunitV3.ComposeAttribute.GetData(System.Reflection.MethodInfo,Xunit.Sdk.DisposalTracker).md#Compono.XunitV3.ComposeAttribute.GetData(System.Reflection.MethodInfo,Xunit.Sdk.DisposalTracker).disposalTracker 'Compono\.XunitV3\.ComposeAttribute\.GetData\(System\.Reflection\.MethodInfo, Xunit\.Sdk\.DisposalTracker\)\.disposalTracker') is deliberately never used to register a composed value\.
            `CompositionRow.Resolve`/`ResolveShared` return whatever the pipeline produced, with
            no visibility into which stage produced it \- a freshly\-constructed value from Compono's own
            generated composition is exactly as indistinguishable, from here, as a shared/cached instance
            returned by an exact registration or a configured `IServiceProvider`
            \(`docs/adr/0019-registrations-and-service-provider-injection.md`'s "the caller owns the
            provider and its entire lifetime; Compono is a pure consumer" contract\) \(PR \#24 review\)\. Handing
            the latter to [Xunit\.Sdk\.DisposalTracker](https://learn.microsoft.com/en-us/dotnet/api/xunit.sdk.disposaltracker 'Xunit\.Sdk\.DisposalTracker') would dispose an externally\-owned instance \-
            possibly a shared singleton reused across many tests \- which is a strictly worse failure mode
            than a consumer being responsible for disposing their own composed [System\.IDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable 'System\.IDisposable')
            parameter themselves\. Automatic disposal tracking is deferred until `Compono`'s public
            surface can expose enough provenance to distinguish the two safely \- its own design question,
            not one to solve with a guess here\.