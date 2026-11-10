#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.NegativeAttributeSeed\(int\) Method

Builds the failure for a negative attribute\-level seed\. The message is byte\-identical on both
engines, including the literal `Compono.XunitV3` product name\.

```csharp
public static Compono.CompositionException NegativeAttributeSeed(int seed);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.NegativeAttributeSeed(int).seed'></a>

`seed` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The configured \(negative\) seed\.

#### Returns
[CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')  
The failure to throw\.