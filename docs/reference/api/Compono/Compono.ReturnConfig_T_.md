#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## ReturnConfig\<T\> Struct

Per\-member configured\-return state for a generator\-emitted test double, one instance per
double member\. Backing fields are [internal](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/internal 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/internal') \- only [ReturnConfigBuilder&lt;T&gt;](Compono.ReturnConfigBuilder_T_.md 'Compono\.ReturnConfigBuilder\<T\>'),
same assembly, ever writes them \- but the read side is [public](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/public 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/public') because the
generated dispatch code reading a slot's configured state lives in a different \(consumer\)
assembly\. See ADR\-0043 Amendment 3, Finding A\.

```csharp
public struct ReturnConfig<T>
```
#### Type parameters

<a name='Compono.ReturnConfig_T_.T'></a>

`T`

| Properties | |
| :--- | :--- |
| [ConfiguredCallCount](Compono.ReturnConfig_T_.ConfiguredCallCount.md 'Compono\.ReturnConfig\<T\>\.ConfiguredCallCount') | The number of times this member's dispatch body has actually run, read by [CallVerifier](Compono.CallVerifier.md 'Compono\.CallVerifier')\. |
| [ConfiguredException](Compono.ReturnConfig_T_.ConfiguredException.md 'Compono\.ReturnConfig\<T\>\.ConfiguredException') | The exception configured via [Throws\(Exception\)](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)')\. Only meaningful when [HasConfiguredException](Compono.ReturnConfig_T_.HasConfiguredException.md 'Compono\.ReturnConfig\<T\>\.HasConfiguredException') is [true](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool')\. |
| [ConfiguredValue](Compono.ReturnConfig_T_.ConfiguredValue.md 'Compono\.ReturnConfig\<T\>\.ConfiguredValue') | The value configured via [Returns\(T\)](Compono.ReturnConfigBuilder_T_.Returns(T).md 'Compono\.ReturnConfigBuilder\<T\>\.Returns\(T\)')\. Only meaningful when [HasConfiguredValue](Compono.ReturnConfig_T_.HasConfiguredValue.md 'Compono\.ReturnConfig\<T\>\.HasConfiguredValue') is [true](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool')\. |
| [HasConfiguredException](Compono.ReturnConfig_T_.HasConfiguredException.md 'Compono\.ReturnConfig\<T\>\.HasConfiguredException') | Whether [ConfiguredException](Compono.ReturnConfig_T_.ConfiguredException.md 'Compono\.ReturnConfig\<T\>\.ConfiguredException') was set via [Throws\(Exception\)](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)')\. |
| [HasConfiguredValue](Compono.ReturnConfig_T_.HasConfiguredValue.md 'Compono\.ReturnConfig\<T\>\.HasConfiguredValue') | Whether [ConfiguredValue](Compono.ReturnConfig_T_.ConfiguredValue.md 'Compono\.ReturnConfig\<T\>\.ConfiguredValue') was set via [Returns\(T\)](Compono.ReturnConfigBuilder_T_.Returns(T).md 'Compono\.ReturnConfigBuilder\<T\>\.Returns\(T\)')\. |

| Methods | |
| :--- | :--- |
| [RecordCall\(\)](Compono.ReturnConfig_T_.RecordCall().md 'Compono\.ReturnConfig\<T\>\.RecordCall\(\)') | Records one call to this member\. Generated dispatch code always calls this rather than incrementing `Compono.ReturnConfig&lt;&gt;.CallCount` directly \- that field is [internal](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/internal 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/internal') and unwritable from the consumer assembly the generated code actually lives in\. See ADR\-0044 Amendment 2, Finding 1\. |
