#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow')

## CompositionRow\.TryResolveConfigured\(Type, object\) Method

Attempts to resolve [type](Compono.CompositionRow.TryResolveConfigured(System.Type,object).md#Compono.CompositionRow.TryResolveConfigured(System.Type,object).type 'Compono\.CompositionRow\.TryResolveConfigured\(System\.Type, object\)\.type') using only Compono's configured/provider\-backed
resolution stages: this row's existing scope values, exact registrations, configuration rules,
and registered [ICompositionValueProvider](Compono.ICompositionValueProvider.md 'Compono\.ICompositionValueProvider') instances \(including Compono\.TestDoubles
and Compono\.NSubstitute\)\. This is NOT equivalent to [Resolve&lt;TValue&gt;\(CompositionRequestDescriptor\)](Compono.CompositionRow.Resolve.md#Compono.CompositionRow.Resolve_TValue_(Compono.CompositionRequestDescriptor) 'Compono\.CompositionRow\.Resolve\<TValue\>\(Compono\.CompositionRequestDescriptor\)') \-
it does not consult a configured `IServiceProvider` \(`UseServiceProvider`\), and it
cannot perform ordinary generated\-plan composition of arbitrary concrete types, because that
dispatch requires the target type to be known at compile time\. See
`docs/adr/0047-compono-dependencyinjection-configured-resolution-bridge.md`\.

```csharp
public bool TryResolveConfigured(System.Type type, out object? value);
```
#### Parameters

<a name='Compono.CompositionRow.TryResolveConfigured(System.Type,object).type'></a>

`type` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

The runtime type to resolve\.

<a name='Compono.CompositionRow.TryResolveConfigured(System.Type,object).value'></a>

`value` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')

The resolved value if a configured/provider stage satisfied [type](Compono.CompositionRow.TryResolveConfigured(System.Type,object).md#Compono.CompositionRow.TryResolveConfigured(System.Type,object).type 'Compono\.CompositionRow\.TryResolveConfigured\(System\.Type, object\)\.type'); otherwise
[null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\. A legitimate handled result can itself be [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') \- check
this method's return value, not whether [value](Compono.CompositionRow.TryResolveConfigured(System.Type,object).md#Compono.CompositionRow.TryResolveConfigured(System.Type,object).value 'Compono\.CompositionRow\.TryResolveConfigured\(System\.Type, object\)\.value') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null'), to
tell "handled" from "not handled" apart\.

#### Returns
[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')  
[true](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') if a configured/provider stage satisfied [type](Compono.CompositionRow.TryResolveConfigured(System.Type,object).md#Compono.CompositionRow.TryResolveConfigured(System.Type,object).type 'Compono\.CompositionRow\.TryResolveConfigured\(System\.Type, object\)\.type');
            [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') if no such stage could handle it\.

#### Exceptions

[CompositionException](Compono.CompositionException.md 'Compono\.CompositionException')  
A reachable stage was applicable but produced an invalid or failing result \(e\.g\. a registration
factory or provider threw, or produced a value of the wrong runtime type\)\. This method
distinguishes "nothing could handle this" \(a [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') return\) from "something
tried and failed" \(a thrown, diagnosed exception\) \- it never collapses the latter into the
former\.