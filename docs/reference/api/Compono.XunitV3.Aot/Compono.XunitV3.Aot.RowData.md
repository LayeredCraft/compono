#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot')

## RowData Struct

One normalized `[ComposeData]` row: its values and, when it was an `Xunit.ITheoryDataRow`, the source row\.

```csharp
public readonly struct RowData
```

| Properties | |
| :--- | :--- |
| [Source](Compono.XunitV3.Aot.RowData.Source.md 'Compono\.XunitV3\.Aot\.RowData\.Source') | The source row, when the row implemented `Xunit.ITheoryDataRow`; otherwise [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\. |
| [Values](Compono.XunitV3.Aot.RowData.Values.md 'Compono\.XunitV3\.Aot\.RowData\.Values') | The row's values, in parameter order\. |
