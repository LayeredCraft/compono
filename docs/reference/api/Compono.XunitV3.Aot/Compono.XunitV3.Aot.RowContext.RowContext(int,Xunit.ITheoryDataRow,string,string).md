#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[RowContext](Compono.XunitV3.Aot.RowContext.md 'Compono\.XunitV3\.Aot\.RowContext')

## RowContext\(int, ITheoryDataRow, string, string\) Constructor

Identifies one `[ComposeData]` row for failure messages and metadata merging\.

```csharp
public RowContext(int index, Xunit.ITheoryDataRow? source, string member, string methodDisplayName);
```
#### Parameters

<a name='Compono.XunitV3.Aot.RowContext.RowContext(int,Xunit.ITheoryDataRow,string,string).index'></a>

`index` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The zero\-based row index\.

<a name='Compono.XunitV3.Aot.RowContext.RowContext(int,Xunit.ITheoryDataRow,string,string).source'></a>

`source` `Xunit.ITheoryDataRow`

The source row, when the row implemented `Xunit.ITheoryDataRow`\.

<a name='Compono.XunitV3.Aot.RowContext.RowContext(int,Xunit.ITheoryDataRow,string,string).member'></a>

`member` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The `Namespace.Type.Member` the attribute named\.

<a name='Compono.XunitV3.Aot.RowContext.RowContext(int,Xunit.ITheoryDataRow,string,string).methodDisplayName'></a>

`methodDisplayName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The test method display name\.