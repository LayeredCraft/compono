#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionProviderRequest](Compono.CompositionProviderRequest.md 'Compono\.CompositionProviderRequest')

## CompositionProviderRequest\(Type, Type, string, Nullability\) Constructor

Creates a [CompositionProviderRequest](Compono.CompositionProviderRequest.md 'Compono\.CompositionProviderRequest')\.

```csharp
public CompositionProviderRequest(System.Type requestedType, System.Type? declaringType, string? name, Compono.Nullability nullability);
```
#### Parameters

<a name='Compono.CompositionProviderRequest.CompositionProviderRequest(System.Type,System.Type,string,Compono.Nullability).requestedType'></a>

`requestedType` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

The requested CLR type\.

<a name='Compono.CompositionProviderRequest.CompositionProviderRequest(System.Type,System.Type,string,Compono.Nullability).declaringType'></a>

`declaringType` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

The type whose constructor/required member declares this request, or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')
for a request with no member identity of its own\.

<a name='Compono.CompositionProviderRequest.CompositionProviderRequest(System.Type,System.Type,string,Compono.Nullability).name'></a>

`name` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The declaring constructor parameter/required member/test\-method\-parameter's own name, or
[null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a request with no name of its own\.

<a name='Compono.CompositionProviderRequest.CompositionProviderRequest(System.Type,System.Type,string,Compono.Nullability).nullability'></a>

`nullability` [Nullability](Compono.Nullability.md 'Compono\.Nullability')

Whether the requesting parameter or member is nullable\-annotated\.