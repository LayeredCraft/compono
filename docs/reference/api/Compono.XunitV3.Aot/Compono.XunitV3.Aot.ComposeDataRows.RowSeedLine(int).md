#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.RowSeedLine\(int\) Method

The seed line used for R0 failures that occur from `CreateRow` on\.

```csharp
public static string RowSeedLine(int rowSeed);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.RowSeedLine(int).rowSeed'></a>

`rowSeed` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The row's effective seed\.

#### Returns
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')  
The seed line, including its leading blank line\.