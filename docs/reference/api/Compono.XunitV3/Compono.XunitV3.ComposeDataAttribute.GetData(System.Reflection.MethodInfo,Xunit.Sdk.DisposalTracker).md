#### [Compono\.XunitV3](index.md 'index')
### [Compono\.XunitV3](Compono.XunitV3.md 'Compono\.XunitV3').[ComposeDataAttribute](Compono.XunitV3.ComposeDataAttribute.md 'Compono\.XunitV3\.ComposeDataAttribute')

## ComposeDataAttribute\.GetData\(MethodInfo, DisposalTracker\) Method

Returns the data to be used to test the theory\.

```csharp
public override System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyCollection<Xunit.ITheoryDataRow>> GetData(System.Reflection.MethodInfo testMethod, Xunit.Sdk.DisposalTracker disposalTracker);
```
#### Parameters

<a name='Compono.XunitV3.ComposeDataAttribute.GetData(System.Reflection.MethodInfo,Xunit.Sdk.DisposalTracker).testMethod'></a>

`testMethod` [System\.Reflection\.MethodInfo](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.methodinfo 'System\.Reflection\.MethodInfo')

The test method the data attribute is attached to

<a name='Compono.XunitV3.ComposeDataAttribute.GetData(System.Reflection.MethodInfo,Xunit.Sdk.DisposalTracker).disposalTracker'></a>

`disposalTracker` `Xunit.Sdk.DisposalTracker`

The disposal tracker used to dispose the data

Implements `GetData(MethodInfo, DisposalTracker)`

#### Returns
[System\.Threading\.Tasks\.ValueTask&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1 'System\.Threading\.Tasks\.ValueTask\`1')[System\.Collections\.Generic\.IReadOnlyCollection&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')`Xunit.ITheoryDataRow`[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlycollection-1 'System\.Collections\.Generic\.IReadOnlyCollection\`1')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1 'System\.Threading\.Tasks\.ValueTask\`1')