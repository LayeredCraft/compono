#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.NormalizeRow\(object, RowContext, Nullable\<int\>\) Method

Normalizes one row into its values array and, when the row implements
`Xunit.ITheoryDataRow`, that source row \(for metadata\)\. Classification order is
`Xunit.ITheoryDataRow` \(`Xunit.ITheoryDataRow.GetData`\), then `object?[]`
\(as\-is\), then [System\.Runtime\.CompilerServices\.ITuple](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.ituple 'System\.Runtime\.CompilerServices\.ITuple') \(indexer, which flattens 8\+ elements\)\. A null or otherwise
unsupported row throws an R0 wrapper with the pre\-composition seed line\.

```csharp
public static Compono.XunitV3.Aot.RowData NormalizeRow(object? row, in Compono.XunitV3.Aot.RowContext context, System.Nullable<int> attributeSeed);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.NormalizeRow(object,Compono.XunitV3.Aot.RowContext,System.Nullable_int_).row'></a>

`row` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')

The raw row element\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.NormalizeRow(object,Compono.XunitV3.Aot.RowContext,System.Nullable_int_).context'></a>

`context` [RowContext](Compono.XunitV3.Aot.RowContext.md 'Compono\.XunitV3\.Aot\.RowContext')

The row context \(index, member, method display name\)\. The source row is not yet known when
normalization runs, so this is normally created with a null source\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.NormalizeRow(object,Compono.XunitV3.Aot.RowContext,System.Nullable_int_).attributeSeed'></a>

`attributeSeed` [System\.Nullable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')

The attribute\-level seed, when set\.

#### Returns
[RowData](Compono.XunitV3.Aot.RowData.md 'Compono\.XunitV3\.Aot\.RowData')  
The normalized row\.

#### Exceptions

[CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')  
The row is null or an unsupported shape\.