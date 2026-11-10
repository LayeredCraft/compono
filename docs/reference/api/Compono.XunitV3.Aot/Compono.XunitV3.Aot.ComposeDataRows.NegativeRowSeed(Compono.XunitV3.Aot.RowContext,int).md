#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.NegativeRowSeed\(RowContext, int\) Method

Builds the failure for a negative row\-level seed\. The message is byte\-identical on both engines\.

```csharp
public static Compono.CompositionException NegativeRowSeed(in Compono.XunitV3.Aot.RowContext context, int rowSeed);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.NegativeRowSeed(Compono.XunitV3.Aot.RowContext,int).context'></a>

`context` [RowContext](Compono.XunitV3.Aot.RowContext.md 'Compono\.XunitV3\.Aot\.RowContext')

The row context \(index, source row label, member, method display name\)\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.NegativeRowSeed(Compono.XunitV3.Aot.RowContext,int).rowSeed'></a>

`rowSeed` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The configured \(negative\) row seed\.

#### Returns
[CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')  
The failure to throw\.