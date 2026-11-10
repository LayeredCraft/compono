#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.RowFailure\(RowContext, string, string, Exception\) Method

Builds an R0 row\-failure [CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException') from a [RowContext](Compono.XunitV3.Aot.RowContext.md 'Compono\.XunitV3\.Aot\.RowContext')\.
When [inner](Compono.XunitV3.Aot.ComposeDataRows.RowFailure(Compono.XunitV3.Aot.RowContext,string,string,System.Exception).md#Compono.XunitV3.Aot.ComposeDataRows.RowFailure(Compono.XunitV3.Aot.RowContext,string,string,System.Exception).inner 'Compono\.XunitV3\.Aot\.ComposeDataRows\.RowFailure\(Compono\.XunitV3\.Aot\.RowContext, string, string, System\.Exception\)\.inner') is a [CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException'), its
[Diagnostic](../Compono/Compono.CompositionException.Diagnostic.md 'Compono\.CompositionException\.Diagnostic') is carried through unchanged and it is preserved
as [System\.Exception\.InnerException](https://learn.microsoft.com/en-us/dotnet/api/system.exception.innerexception 'System\.Exception\.InnerException'); any other inner exception is preserved as
[System\.Exception\.InnerException](https://learn.microsoft.com/en-us/dotnet/api/system.exception.innerexception 'System\.Exception\.InnerException') with no diagnostic\.

```csharp
public static Compono.CompositionException RowFailure(in Compono.XunitV3.Aot.RowContext context, string cause, string seedLine, System.Exception? inner=null);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.RowFailure(Compono.XunitV3.Aot.RowContext,string,string,System.Exception).context'></a>

`context` [RowContext](Compono.XunitV3.Aot.RowContext.md 'Compono\.XunitV3\.Aot\.RowContext')

The row context \(index, source row label, member, method display name\)\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.RowFailure(Compono.XunitV3.Aot.RowContext,string,string,System.Exception).cause'></a>

`cause` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The cause message\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.RowFailure(Compono.XunitV3.Aot.RowContext,string,string,System.Exception).seedLine'></a>

`seedLine` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The seed line to append \(see [PreRowSeedLine\(Nullable&lt;int&gt;\)](Compono.XunitV3.Aot.ComposeDataRows.PreRowSeedLine(System.Nullable_int_).md 'Compono\.XunitV3\.Aot\.ComposeDataRows\.PreRowSeedLine\(System\.Nullable\<int\>\)') and [RowSeedLine\(int\)](Compono.XunitV3.Aot.ComposeDataRows.RowSeedLine(int).md 'Compono\.XunitV3\.Aot\.ComposeDataRows\.RowSeedLine\(int\)')\)\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.RowFailure(Compono.XunitV3.Aot.RowContext,string,string,System.Exception).inner'></a>

`inner` [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')

The cause exception, when any\.

#### Returns
[CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')  
The failure to throw\.