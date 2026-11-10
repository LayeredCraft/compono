#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.ExtractConfig\<TConfig\>\(object\[\], RowContext, Nullable\<int\>\) Method

Extracts the per\-row [TConfig](Compono.XunitV3.Aot.ComposeDataRows.ExtractConfig_TConfig_(object[],Compono.XunitV3.Aot.RowContext,System.Nullable_int_).md#Compono.XunitV3.Aot.ComposeDataRows.ExtractConfig_TConfig_(object[],Compono.XunitV3.Aot.RowContext,System.Nullable_int_).TConfig 'Compono\.XunitV3\.Aot\.ComposeDataRows\.ExtractConfig\<TConfig\>\(object\[\], Compono\.XunitV3\.Aot\.RowContext, System\.Nullable\<int\>\)\.TConfig') value from element 0 of a
`ComposeData<TProfile, TConfig>` row and returns the remaining row values\.
A missing, null, or wrong\-typed element 0 throws an R0 wrapper with the pre\-composition seed
line\.

```csharp
public static (TConfig Config,object?[] Values) ExtractConfig<TConfig>(object?[] values, in Compono.XunitV3.Aot.RowContext context, System.Nullable<int> attributeSeed);
```
#### Type parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.ExtractConfig_TConfig_(object[],Compono.XunitV3.Aot.RowContext,System.Nullable_int_).TConfig'></a>

`TConfig`

The profile configuration type\.
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.ExtractConfig_TConfig_(object[],Compono.XunitV3.Aot.RowContext,System.Nullable_int_).values'></a>

`values` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

The normalized row values \(element 0 is the configuration value\)\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.ExtractConfig_TConfig_(object[],Compono.XunitV3.Aot.RowContext,System.Nullable_int_).context'></a>

`context` [RowContext](Compono.XunitV3.Aot.RowContext.md 'Compono\.XunitV3\.Aot\.RowContext')

The row context \(index, source row, member, method display name\)\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.ExtractConfig_TConfig_(object[],Compono.XunitV3.Aot.RowContext,System.Nullable_int_).attributeSeed'></a>

`attributeSeed` [System\.Nullable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')

The attribute\-level seed, when set\.

#### Returns
[&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.valuetuple 'System\.ValueTuple')[TConfig](Compono.XunitV3.Aot.ComposeDataRows.ExtractConfig_TConfig_(object[],Compono.XunitV3.Aot.RowContext,System.Nullable_int_).md#Compono.XunitV3.Aot.ComposeDataRows.ExtractConfig_TConfig_(object[],Compono.XunitV3.Aot.RowContext,System.Nullable_int_).TConfig 'Compono\.XunitV3\.Aot\.ComposeDataRows\.ExtractConfig\<TConfig\>\(object\[\], Compono\.XunitV3\.Aot\.RowContext, System\.Nullable\<int\>\)\.TConfig')[,](https://learn.microsoft.com/en-us/dotnet/api/system.valuetuple 'System\.ValueTuple')[System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.valuetuple 'System\.ValueTuple')  
The configuration value and the remaining row values\.

#### Exceptions

[CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')  
Element 0 is missing, null, or not a [TConfig](Compono.XunitV3.Aot.ComposeDataRows.ExtractConfig_TConfig_(object[],Compono.XunitV3.Aot.RowContext,System.Nullable_int_).md#Compono.XunitV3.Aot.ComposeDataRows.ExtractConfig_TConfig_(object[],Compono.XunitV3.Aot.RowContext,System.Nullable_int_).TConfig 'Compono\.XunitV3\.Aot\.ComposeDataRows\.ExtractConfig\<TConfig\>\(object\[\], Compono\.XunitV3\.Aot\.RowContext, System\.Nullable\<int\>\)\.TConfig')\.