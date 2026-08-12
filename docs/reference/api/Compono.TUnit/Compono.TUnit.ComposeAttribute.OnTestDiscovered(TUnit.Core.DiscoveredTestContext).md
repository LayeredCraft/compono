#### [Compono\.TUnit](index.md 'index')
### [Compono\.TUnit](Compono.TUnit.md 'Compono\.TUnit').[ComposeAttribute](Compono.TUnit.ComposeAttribute.md 'Compono\.TUnit\.ComposeAttribute')

## ComposeAttribute\.OnTestDiscovered\(DiscoveredTestContext\) Method

Called when a test is discovered during the test discovery phase\.

```csharp
public System.Threading.Tasks.ValueTask OnTestDiscovered(TUnit.Core.DiscoveredTestContext context);
```
#### Parameters

<a name='Compono.TUnit.ComposeAttribute.OnTestDiscovered(TUnit.Core.DiscoveredTestContext).context'></a>

`context` `TUnit.Core.DiscoveredTestContext`

The discovered test context, which provides methods to modify the test's
            configuration such as retry limits, timeouts, and skip conditions\.

Implements `OnTestDiscovered(DiscoveredTestContext)`

#### Returns
[System\.Threading\.Tasks\.ValueTask](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask 'System\.Threading\.Tasks\.ValueTask')  
A [System\.Threading\.Tasks\.ValueTask](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask 'System\.Threading\.Tasks\.ValueTask') representing the asynchronous operation\.