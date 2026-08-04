#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionException](Compono.CompositionException.md 'Compono\.CompositionException')

## CompositionException\.WithSeedInMessage\(CompositionException, int\) Method

Creates a copy of [original](Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).md#Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).original 'Compono\.CompositionException\.WithSeedInMessage\(Compono\.CompositionException, int\)\.original') whose [System\.Exception\.Message](https://learn.microsoft.com/en-us/dotnet/api/system.exception.message 'System\.Exception\.Message') has a
`"Seed: <value>"` line appended, so a consumer building custom composition\-failure
tooling \(e\.g\. a test\-framework integration reporting a reproducible seed\) can surface it
without needing [Diagnostic](Compono.CompositionException.Diagnostic.md 'Compono\.CompositionException\.Diagnostic') to be present \- [Diagnostic](Compono.CompositionException.Diagnostic.md 'Compono\.CompositionException\.Diagnostic') already
renders its own `"Seed:"` line via [ToString\(\)](Compono.CompositionDiagnostic.ToString().md 'Compono\.CompositionDiagnostic\.ToString\(\)') when it's
there, but not every [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') has one \(a plain
[CompositionException\(string\)](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(string) 'Compono\.CompositionException\.CompositionException\(string\)'), e\.g\. a generated collection plan's
unique\-value\-exhaustion failure, has none\)\.

```csharp
public static Compono.CompositionException WithSeedInMessage(Compono.CompositionException original, int seed);
```
#### Parameters

<a name='Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).original'></a>

`original` [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException')

The exception to copy and append the seed to\.

<a name='Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).seed'></a>

`seed` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The seed value to append\.

#### Returns
[CompositionException](Compono.CompositionException.md 'Compono\.CompositionException')  
A new [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') whose [Diagnostic](Compono.CompositionException.Diagnostic.md 'Compono\.CompositionException\.Diagnostic') is
[original](Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).md#Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).original 'Compono\.CompositionException\.WithSeedInMessage\(Compono\.CompositionException, int\)\.original')'s, unchanged \([null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') stays [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\),
and whose [System\.Exception\.InnerException](https://learn.microsoft.com/en-us/dotnet/api/system.exception.innerexception 'System\.Exception\.InnerException') is [original](Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).md#Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).original 'Compono\.CompositionException\.WithSeedInMessage\(Compono\.CompositionException, int\)\.original') itself \- so a
provider failure's full chain becomes this new exception, then [original](Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).md#Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).original 'Compono\.CompositionException\.WithSeedInMessage\(Compono\.CompositionException, int\)\.original'),
then whatever [original](Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).md#Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).original 'Compono\.CompositionException\.WithSeedInMessage\(Compono\.CompositionException, int\)\.original')'s own [System\.Exception\.InnerException](https://learn.microsoft.com/en-us/dotnet/api/system.exception.innerexception 'System\.Exception\.InnerException') was \(if
any\), one level deeper than the original throw\.

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[original](Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).md#Compono.CompositionException.WithSeedInMessage(Compono.CompositionException,int).original 'Compono\.CompositionException\.WithSeedInMessage\(Compono\.CompositionException, int\)\.original') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.