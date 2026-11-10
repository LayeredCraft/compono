#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.CreateDataRow\(object\`], RowContext, ComposeDataAttributeMetadata, int) Method

Builds the output [Xunit.TheoryDataRow` for one composed row, applying the single
Compono metadata\-merge rule \(ADR\-0070\): the skip group \(`Skip`/`SkipType`/
`SkipUnless`/`SkipWhen`\) is grouped \(row group when the row's `Skip` is set,
else the attribute group when the attribute's `Skip` is set, else none\); other metadata
is row ?? attribute; traits are the union of the row's, the attribute's, and
[SeedTraitName](Compono.XunitV3.Aot.ComposeDataRows.SeedTraitName.md 'Compono\.XunitV3\.Aot\.ComposeDataRows\.SeedTraitName')\.

```csharp
public static Xunit.TheoryDataRow CreateDataRow(object?[] values, in Compono.XunitV3.Aot.RowContext context, in Compono.XunitV3.Aot.ComposeDataAttributeMetadata attribute, int seed);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.CreateDataRow(object[],Compono.XunitV3.Aot.RowContext,Compono.XunitV3.Aot.ComposeDataAttributeMetadata,int).values'></a>

`values` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

The composed row values, in parameter order\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.CreateDataRow(object[],Compono.XunitV3.Aot.RowContext,Compono.XunitV3.Aot.ComposeDataAttributeMetadata,int).context'></a>

`context` [RowContext](Compono.XunitV3.Aot.RowContext.md 'Compono\.XunitV3\.Aot\.RowContext')

The row context \(index, source row, member, method display name\)\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.CreateDataRow(object[],Compono.XunitV3.Aot.RowContext,Compono.XunitV3.Aot.ComposeDataAttributeMetadata,int).attribute'></a>

`attribute` [ComposeDataAttributeMetadata](Compono.XunitV3.Aot.ComposeDataAttributeMetadata.md 'Compono\.XunitV3\.Aot\.ComposeDataAttributeMetadata')

The attribute\-level metadata\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.CreateDataRow(object[],Compono.XunitV3.Aot.RowContext,Compono.XunitV3.Aot.ComposeDataAttributeMetadata,int).seed'></a>

`seed` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The row's effective seed\.

#### Returns
`Xunit.TheoryDataRow`  
The output row\.