#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.BindValue\<T\>\(object\[\], int, bool, string, RowContext, int\) Method

Binds one leading positional row value to a closed parameter type [T](Compono.XunitV3.Aot.ComposeDataRows.BindValue_T_(object[],int,bool,string,Compono.XunitV3.Aot.RowContext,int).md#Compono.XunitV3.Aot.ComposeDataRows.BindValue_T_(object[],int,bool,string,Compono.XunitV3.Aot.RowContext,int).T 'Compono\.XunitV3\.Aot\.ComposeDataRows\.BindValue\<T\>\(object\[\], int, bool, string, Compono\.XunitV3\.Aot\.RowContext, int\)\.T')
using ADR\-0022's strict typing rules \(no widening; a null is allowed only for a nullable value
type or a nullable reference type; a non\-null value must be an instance of the parameter's
underlying type\)\. A failure throws an R0 wrapper with the row's effective seed line\.

```csharp
public static T BindValue<T>(object?[] values, int index, bool parameterIsNullable, string parameterName, in Compono.XunitV3.Aot.RowContext context, int rowSeed);
```
#### Type parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.BindValue_T_(object[],int,bool,string,Compono.XunitV3.Aot.RowContext,int).T'></a>

`T`

The closed parameter type\.
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.BindValue_T_(object[],int,bool,string,Compono.XunitV3.Aot.RowContext,int).values'></a>

`values` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

The normalized row values\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.BindValue_T_(object[],int,bool,string,Compono.XunitV3.Aot.RowContext,int).index'></a>

`index` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The row value index \(0\-based, after any `TConfig` element\)\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.BindValue_T_(object[],int,bool,string,Compono.XunitV3.Aot.RowContext,int).parameterIsNullable'></a>

`parameterIsNullable` [System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')

Whether the parameter is nullable\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.BindValue_T_(object[],int,bool,string,Compono.XunitV3.Aot.RowContext,int).parameterName'></a>

`parameterName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The parameter name, for failure messages\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.BindValue_T_(object[],int,bool,string,Compono.XunitV3.Aot.RowContext,int).context'></a>

`context` [RowContext](Compono.XunitV3.Aot.RowContext.md 'Compono\.XunitV3\.Aot\.RowContext')

The row context \(index, source row, member, method display name\)\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.BindValue_T_(object[],int,bool,string,Compono.XunitV3.Aot.RowContext,int).rowSeed'></a>

`rowSeed` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The row's effective seed, for the failure seed line\.

#### Returns
[T](Compono.XunitV3.Aot.ComposeDataRows.BindValue_T_(object[],int,bool,string,Compono.XunitV3.Aot.RowContext,int).md#Compono.XunitV3.Aot.ComposeDataRows.BindValue_T_(object[],int,bool,string,Compono.XunitV3.Aot.RowContext,int).T 'Compono\.XunitV3\.Aot\.ComposeDataRows\.BindValue\<T\>\(object\[\], int, bool, string, Compono\.XunitV3\.Aot\.RowContext, int\)\.T')  
The bound value\.

#### Exceptions

[CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')  
The value is null for a non\-nullable parameter, or its type is not assignable\.