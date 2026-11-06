#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionException](Compono.CompositionException.md 'Compono\.CompositionException')

## CompositionException Constructors

| Overloads | |
| :--- | :--- |
| [CompositionException\(CompositionDiagnostic\)](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic) 'Compono\.CompositionException\.CompositionException\(Compono\.CompositionDiagnostic\)') | Creates a [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') from a structured [CompositionDiagnostic](Compono.CompositionDiagnostic.md 'Compono\.CompositionDiagnostic') \- the shape every pipeline\-thrown instance uses\. |
| [CompositionException\(CompositionDiagnostic, Exception\)](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic,System.Exception) 'Compono\.CompositionException\.CompositionException\(Compono\.CompositionDiagnostic, System\.Exception\)') | Creates a [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') from a structured [CompositionDiagnostic](Compono.CompositionDiagnostic.md 'Compono\.CompositionDiagnostic'), preserving [innerException](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic,System.Exception).innerException 'Compono\.CompositionException\.CompositionException\(Compono\.CompositionDiagnostic, System\.Exception\)\.innerException') \- the shape a configured `IServiceProvider` throwing during stage 3's fallback sub\-step uses, per `docs/adr/0019-registrations-and-service-provider-injection.md` \("never `throw ex;`, the original exception is always preserved"\)\. |
| [CompositionException\(string\)](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(string) 'Compono\.CompositionException\.CompositionException\(string\)') | Creates a [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') with no structured [Diagnostic](Compono.CompositionException.Diagnostic.md 'Compono\.CompositionException\.Diagnostic')\. |
| [CompositionException\(string, CompositionDiagnostic, Exception\)](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(string,Compono.CompositionDiagnostic,System.Exception) 'Compono\.CompositionException\.CompositionException\(string, Compono\.CompositionDiagnostic, System\.Exception\)') | Creates a [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') with an explicit [message](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(string,Compono.CompositionDiagnostic,System.Exception).message 'Compono\.CompositionException\.CompositionException\(string, Compono\.CompositionDiagnostic, System\.Exception\)\.message'), together with a structured [diagnostic](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(string,Compono.CompositionDiagnostic,System.Exception).diagnostic 'Compono\.CompositionException\.CompositionException\(string, Compono\.CompositionDiagnostic, System\.Exception\)\.diagnostic') and a preserved [innerException](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(string,Compono.CompositionDiagnostic,System.Exception).innerException 'Compono\.CompositionException\.CompositionException\(string, Compono\.CompositionDiagnostic, System\.Exception\)\.innerException') \- the shape a caller that supplies its own [System\.Exception\.Message](https://learn.microsoft.com/en-us/dotnet/api/system.exception.message 'System\.Exception\.Message') while keeping the causing exception and the original [CompositionDiagnostic](Compono.CompositionDiagnostic.md 'Compono\.CompositionDiagnostic') reference needs\. Added for the `[ComposeData]` row\-failure wrapper, which prefixes the cause's message with the failing row's identity \(ADR\-0070 Amendment 1\)\. |

<a name='Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic)'></a>

## CompositionException\(CompositionDiagnostic\) Constructor

Creates a [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') from a structured [CompositionDiagnostic](Compono.CompositionDiagnostic.md 'Compono\.CompositionDiagnostic')
\- the shape every pipeline\-thrown instance uses\.

```csharp
public CompositionException(Compono.CompositionDiagnostic diagnostic);
```
#### Parameters

<a name='Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic).diagnostic'></a>

`diagnostic` [CompositionDiagnostic](Compono.CompositionDiagnostic.md 'Compono\.CompositionDiagnostic')

The structured detail behind this failure\.

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[diagnostic](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic).diagnostic 'Compono\.CompositionException\.CompositionException\(Compono\.CompositionDiagnostic\)\.diagnostic') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.

<a name='Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic,System.Exception)'></a>

## CompositionException\(CompositionDiagnostic, Exception\) Constructor

Creates a [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') from a structured [CompositionDiagnostic](Compono.CompositionDiagnostic.md 'Compono\.CompositionDiagnostic'),
preserving [innerException](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic,System.Exception).innerException 'Compono\.CompositionException\.CompositionException\(Compono\.CompositionDiagnostic, System\.Exception\)\.innerException') \- the shape a configured `IServiceProvider`
throwing during stage 3's fallback sub\-step uses, per
`docs/adr/0019-registrations-and-service-provider-injection.md` \("never `throw ex;`,
the original exception is always preserved"\)\.

```csharp
public CompositionException(Compono.CompositionDiagnostic diagnostic, System.Exception innerException);
```
#### Parameters

<a name='Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic,System.Exception).diagnostic'></a>

`diagnostic` [CompositionDiagnostic](Compono.CompositionDiagnostic.md 'Compono\.CompositionDiagnostic')

The structured detail behind this failure\.

<a name='Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic,System.Exception).innerException'></a>

`innerException` [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')

The exception that caused this failure\.

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[diagnostic](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic,System.Exception).diagnostic 'Compono\.CompositionException\.CompositionException\(Compono\.CompositionDiagnostic, System\.Exception\)\.diagnostic') or [innerException](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(Compono.CompositionDiagnostic,System.Exception).innerException 'Compono\.CompositionException\.CompositionException\(Compono\.CompositionDiagnostic, System\.Exception\)\.innerException') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.

<a name='Compono.CompositionException.CompositionException(string)'></a>

## CompositionException\(string\) Constructor

Creates a [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') with no structured [Diagnostic](Compono.CompositionException.Diagnostic.md 'Compono\.CompositionException\.Diagnostic')\.

```csharp
public CompositionException(string message);
```
#### Parameters

<a name='Compono.CompositionException.CompositionException(string).message'></a>

`message` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

A message describing what couldn't be composed and why\.

<a name='Compono.CompositionException.CompositionException(string,Compono.CompositionDiagnostic,System.Exception)'></a>

## CompositionException\(string, CompositionDiagnostic, Exception\) Constructor

Creates a [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') with an explicit [message](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(string,Compono.CompositionDiagnostic,System.Exception).message 'Compono\.CompositionException\.CompositionException\(string, Compono\.CompositionDiagnostic, System\.Exception\)\.message'),
together with a structured [diagnostic](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(string,Compono.CompositionDiagnostic,System.Exception).diagnostic 'Compono\.CompositionException\.CompositionException\(string, Compono\.CompositionDiagnostic, System\.Exception\)\.diagnostic') and a preserved
[innerException](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(string,Compono.CompositionDiagnostic,System.Exception).innerException 'Compono\.CompositionException\.CompositionException\(string, Compono\.CompositionDiagnostic, System\.Exception\)\.innerException') \- the shape a caller that supplies its own
[System\.Exception\.Message](https://learn.microsoft.com/en-us/dotnet/api/system.exception.message 'System\.Exception\.Message') while keeping the causing exception and the original
[CompositionDiagnostic](Compono.CompositionDiagnostic.md 'Compono\.CompositionDiagnostic') reference needs\. Added for the `[ComposeData]`
row\-failure wrapper, which prefixes the cause's message with the failing row's identity
\(ADR\-0070 Amendment 1\)\.

```csharp
public CompositionException(string message, Compono.CompositionDiagnostic? diagnostic, System.Exception innerException);
```
#### Parameters

<a name='Compono.CompositionException.CompositionException(string,Compono.CompositionDiagnostic,System.Exception).message'></a>

`message` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The message to surface exactly as supplied\.

<a name='Compono.CompositionException.CompositionException(string,Compono.CompositionDiagnostic,System.Exception).diagnostic'></a>

`diagnostic` [CompositionDiagnostic](Compono.CompositionDiagnostic.md 'Compono\.CompositionDiagnostic')

The structured detail behind this failure, or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') when the cause carried
none; preserved by reference\.

<a name='Compono.CompositionException.CompositionException(string,Compono.CompositionDiagnostic,System.Exception).innerException'></a>

`innerException` [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')

The exception that caused this failure\.

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[innerException](Compono.CompositionException..ctor.md#Compono.CompositionException.CompositionException(string,Compono.CompositionDiagnostic,System.Exception).innerException 'Compono\.CompositionException\.CompositionException\(string, Compono\.CompositionDiagnostic, System\.Exception\)\.innerException') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.