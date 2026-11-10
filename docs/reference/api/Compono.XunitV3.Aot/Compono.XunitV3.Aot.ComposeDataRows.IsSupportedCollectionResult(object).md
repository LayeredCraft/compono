#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.IsSupportedCollectionResult\(object\) Method

Reports whether an awaited member result is a supported synchronous row collection: any
[System\.Collections\.IEnumerable](https://learn.microsoft.com/en-us/dotnet/api/system.collections.ienumerable 'System\.Collections\.IEnumerable') other than [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')\. Async
\(`IAsyncEnumerable<T>`\) results are classified by the caller, which owns the
enumeration loop\.

```csharp
public static bool IsSupportedCollectionResult(object? result);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.IsSupportedCollectionResult(object).result'></a>

`result` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')

The awaited member result\.

#### Returns
[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')  
[true](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') when the result is a supported collection\.