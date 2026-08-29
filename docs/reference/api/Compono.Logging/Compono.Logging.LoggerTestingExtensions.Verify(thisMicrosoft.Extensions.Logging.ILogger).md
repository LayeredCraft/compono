#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[LoggerTestingExtensions](Compono.Logging.LoggerTestingExtensions.md 'Compono\.Logging\.LoggerTestingExtensions')

## LoggerTestingExtensions\.Verify\(this ILogger\) Method

The entry point for fluent verification, e\.g\.
`logger.Verify().AtLevel(LogLevel.Warning).WithMessageContaining("retry").Once()` \-
matching the same single\-verb vocabulary `Compono.TestDoubles`/`Compono.Http`
already established \(`repository.Verify().Save().Once()`,
`registration.Verify().Once()`\), not a two\-verb `VerifyLog()...Verify()` shape\.

```csharp
public static Compono.Logging.LogVerificationBuilder Verify(this Microsoft.Extensions.Logging.ILogger logger);
```
#### Parameters

<a name='Compono.Logging.LoggerTestingExtensions.Verify(thisMicrosoft.Extensions.Logging.ILogger).logger'></a>

`logger` [Microsoft\.Extensions\.Logging\.ILogger](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger 'Microsoft\.Extensions\.Logging\.ILogger')

#### Returns
[LogVerificationBuilder](Compono.Logging.LogVerificationBuilder.md 'Compono\.Logging\.LogVerificationBuilder')

#### Exceptions

[System\.InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception 'System\.InvalidOperationException')  
[logger](Compono.Logging.LoggerTestingExtensions.Verify(thisMicrosoft.Extensions.Logging.ILogger).md#Compono.Logging.LoggerTestingExtensions.Verify(thisMicrosoft.Extensions.Logging.ILogger).logger 'Compono\.Logging\.LoggerTestingExtensions\.Verify\(this Microsoft\.Extensions\.Logging\.ILogger\)\.logger') is not a
            Compono\.Logging capturing logger\.