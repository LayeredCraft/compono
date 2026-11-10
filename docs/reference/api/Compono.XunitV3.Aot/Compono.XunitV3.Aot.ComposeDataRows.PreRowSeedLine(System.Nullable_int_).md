#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.PreRowSeedLine\(Nullable\<int\>\) Method

The seed line used for R0 failures that occur before `CreateRow`\.

```csharp
public static string PreRowSeedLine(System.Nullable<int> attributeSeed);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.PreRowSeedLine(System.Nullable_int_).attributeSeed'></a>

`attributeSeed` [System\.Nullable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')

The attribute\-level seed, when set\.

#### Returns
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')  
The seed line, including its leading blank line\.