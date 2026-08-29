#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[CapturingLogger](Compono.Logging.CapturingLogger.md 'Compono\.Logging\.CapturingLogger')

## CapturingLogger\.BeginScope\<TState\>\(TState\) Method

Begins a logical operation scope\.

```csharp
public System.IDisposable? BeginScope<TState>(TState state)
    where TState : notnull;
```
#### Type parameters

<a name='Compono.Logging.CapturingLogger.BeginScope_TState_(TState).TState'></a>

`TState`

The type of the state to begin scope for\.
#### Parameters

<a name='Compono.Logging.CapturingLogger.BeginScope_TState_(TState).state'></a>

`state` [TState](Compono.Logging.CapturingLogger.BeginScope_TState_(TState).md#Compono.Logging.CapturingLogger.BeginScope_TState_(TState).TState 'Compono\.Logging\.CapturingLogger\.BeginScope\<TState\>\(TState\)\.TState')

The identifier for the scope\.

Implements [BeginScope&lt;TState&gt;\(TState\)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger.beginscope--1#microsoft-extensions-logging-ilogger-beginscope--1(--0) 'Microsoft\.Extensions\.Logging\.ILogger\.BeginScope\`\`1\(\`\`0\)')

#### Returns
[System\.IDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable 'System\.IDisposable')  
An [System\.IDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable 'System\.IDisposable') that ends the logical operation scope on dispose\.