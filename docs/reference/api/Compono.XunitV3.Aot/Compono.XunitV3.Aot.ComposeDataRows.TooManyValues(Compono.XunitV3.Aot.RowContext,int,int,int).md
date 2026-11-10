#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.TooManyValues\(RowContext, int, int, int\) Method

Builds the R0 failure for a row that supplies more leading positional values than the test
method has parameters\. The message is byte\-identical on both engines\.

```csharp
public static Compono.CompositionException TooManyValues(in Compono.XunitV3.Aot.RowContext context, int supplied, int parameterCount, int rowSeed);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.TooManyValues(Compono.XunitV3.Aot.RowContext,int,int,int).context'></a>

`context` [RowContext](Compono.XunitV3.Aot.RowContext.md 'Compono\.XunitV3\.Aot\.RowContext')

The row context \(index, source row label, member, method display name\)\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.TooManyValues(Compono.XunitV3.Aot.RowContext,int,int,int).supplied'></a>

`supplied` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The number of row values supplied\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.TooManyValues(Compono.XunitV3.Aot.RowContext,int,int,int).parameterCount'></a>

`parameterCount` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The number of test method parameters\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.TooManyValues(Compono.XunitV3.Aot.RowContext,int,int,int).rowSeed'></a>

`rowSeed` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The row's effective seed, for the failure seed line\.

#### Returns
[CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')  
The failure to throw\.