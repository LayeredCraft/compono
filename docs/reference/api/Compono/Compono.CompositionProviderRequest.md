#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## CompositionProviderRequest Struct

A composition request, as seen by a public [ICompositionValueProvider](Compono.ICompositionValueProvider.md 'Compono\.ICompositionValueProvider') \- decoupled
from the engine's own internal `Compono.CompositionRequest` \(no path, no shared\-scope flag,
no pipeline plumbing a provider author has no legitimate use for\)\. See
`docs/adr/0024-public-provider-extensibility-model.md`\.

```csharp
public readonly struct CompositionProviderRequest
```

| Constructors | |
| :--- | :--- |
| [CompositionProviderRequest\(Type, Type, string, Nullability\)](Compono.CompositionProviderRequest.CompositionProviderRequest(System.Type,System.Type,string,Compono.Nullability).md 'Compono\.CompositionProviderRequest\.CompositionProviderRequest\(System\.Type, System\.Type, string, Compono\.Nullability\)') | Creates a [CompositionProviderRequest](Compono.CompositionProviderRequest.md 'Compono\.CompositionProviderRequest')\. |

| Properties | |
| :--- | :--- |
| [DeclaringType](Compono.CompositionProviderRequest.DeclaringType.md 'Compono\.CompositionProviderRequest\.DeclaringType') | The type whose constructor/required member declares this request, or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a request with no member identity \(a collection element, a manual resolve, or the composition root itself\) \- the same field/same semantics [DeclaringType](Compono.CompositionRequestDescriptor.DeclaringType.md 'Compono\.CompositionRequestDescriptor\.DeclaringType') already carries, per `docs/adr/0020-composition-configuration-rules.md`\. |
| [Name](Compono.CompositionProviderRequest.Name.md 'Compono\.CompositionProviderRequest\.Name') | The declaring constructor parameter/required member/test\-method\-parameter's own name, for diagnostic display and name\-based provider matching \(e\.g\. a future `Compono.Bogus` member\-name convention\) \- [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a request with no name of its own \(a collection element, dictionary key/value, or manual resolve\)\. |
| [Nullability](Compono.CompositionProviderRequest.Nullability.md 'Compono\.CompositionProviderRequest\.Nullability') | Whether the requesting parameter or member is nullable\-annotated\. |
| [RequestedType](Compono.CompositionProviderRequest.RequestedType.md 'Compono\.CompositionProviderRequest\.RequestedType') | The requested CLR type\. |
