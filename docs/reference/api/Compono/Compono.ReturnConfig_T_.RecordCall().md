#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ReturnConfig&lt;T&gt;](Compono.ReturnConfig_T_.md 'Compono\.ReturnConfig\<T\>')

## ReturnConfig\<T\>\.RecordCall\(\) Method

Records one call to this member\. Generated dispatch code always calls this rather than
incrementing `Compono.ReturnConfig&lt;&gt;.CallCount` directly \- that field is [internal](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/internal 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/internal') and
unwritable from the consumer assembly the generated code actually lives in\. See ADR\-0044
Amendment 2, Finding 1\.

```csharp
public void RecordCall();
```