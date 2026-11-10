#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot')

## RowContext Struct

Identifies one `[ComposeData]` row for failure messages and metadata merging\.

```csharp
public readonly struct RowContext
```

| Constructors | |
| :--- | :--- |
| [RowContext\(int, ITheoryDataRow, string, string\)](Compono.XunitV3.Aot.RowContext.RowContext(int,Xunit.ITheoryDataRow,string,string).md 'Compono\.XunitV3\.Aot\.RowContext\.RowContext\(int, Xunit\.ITheoryDataRow, string, string\)') | Identifies one `[ComposeData]` row for failure messages and metadata merging\. |

| Properties | |
| :--- | :--- |
| [Index](Compono.XunitV3.Aot.RowContext.Index.md 'Compono\.XunitV3\.Aot\.RowContext\.Index') | The zero\-based row index\. |
| [Label](Compono.XunitV3.Aot.RowContext.Label.md 'Compono\.XunitV3\.Aot\.RowContext\.Label') | The source row's label, when present\. |
| [Member](Compono.XunitV3.Aot.RowContext.Member.md 'Compono\.XunitV3\.Aot\.RowContext\.Member') | The `Namespace.Type.Member` the attribute named\. |
| [MethodDisplayName](Compono.XunitV3.Aot.RowContext.MethodDisplayName.md 'Compono\.XunitV3\.Aot\.RowContext\.MethodDisplayName') | The test method display name\. |
| [Source](Compono.XunitV3.Aot.RowContext.Source.md 'Compono\.XunitV3\.Aot\.RowContext\.Source') | The source row, when the row implemented `Xunit.ITheoryDataRow`; otherwise [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\. |
