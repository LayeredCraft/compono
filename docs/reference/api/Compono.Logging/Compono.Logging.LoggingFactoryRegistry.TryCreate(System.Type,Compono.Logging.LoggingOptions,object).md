#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LoggingFactoryRegistry](Compono.Logging.LoggingFactoryRegistry.md 'Compono\.Logging\.LoggingFactoryRegistry')

## LoggingFactoryRegistry\.TryCreate\(Type, LoggingOptions, object\) Method

Looks up and invokes the activator registered for [requestedType](Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).md#Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).requestedType 'Compono\.Logging\.LoggingFactoryRegistry\.TryCreate\(System\.Type, Compono\.Logging\.LoggingOptions, object\)\.requestedType'), passing
[options](Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).md#Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).options 'Compono\.Logging\.LoggingFactoryRegistry\.TryCreate\(System\.Type, Compono\.Logging\.LoggingOptions, object\)\.options') through untouched \- the caller's live [LoggingOptions](Compono.Logging.LoggingOptions.md 'Compono\.Logging\.LoggingOptions')
for the request being resolved, never captured ahead of time by the generated registration
itself\. Returns [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') if no activator has been registered \- either because
[requestedType](Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).md#Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).requestedType 'Compono\.Logging\.LoggingFactoryRegistry\.TryCreate\(System\.Type, Compono\.Logging\.LoggingOptions, object\)\.requestedType') was never discovered as a composed `ILogger<T>`
leaf, or because the consuming assembly's module initializers haven't run yet\.

```csharp
public static bool TryCreate(System.Type requestedType, Compono.Logging.LoggingOptions options, out object? value);
```
#### Parameters

<a name='Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).requestedType'></a>

`requestedType` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

<a name='Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).options'></a>

`options` [LoggingOptions](Compono.Logging.LoggingOptions.md 'Compono\.Logging\.LoggingOptions')

<a name='Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).value'></a>

`value` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')

#### Returns
[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')