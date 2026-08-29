#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[CapturedLogEntry](Compono.Logging.CapturedLogEntry.md 'Compono\.Logging\.CapturedLogEntry')

## CapturedLogEntry\.Properties Property

Non\-null only when [State](Compono.Logging.CapturedLogEntry.State.md 'Compono\.Logging\.CapturedLogEntry\.State') implements
[System\.Collections\.Generic\.IReadOnlyList&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1') of [System\.Collections\.Generic\.KeyValuePair&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.keyvaluepair-2 'System\.Collections\.Generic\.KeyValuePair\`2') of
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String') and [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') \- both the compiler\-generated
`FormattedLogValues` behind an ordinary `LogInformation(...)` call and the shared
`LoggerMessageState` behind every `[LoggerMessage]` source\-generated call satisfy
this identically, so one code path covers both\. The value slot is exposed as nullable here
even though the BCL's own interface declares it non\-nullable [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') \- a
structured logging call can legitimately pass a [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') argument, and this
signature is the more truthful contract for that case \(ADR\-0055's "Properties nullability"
decision\)\.

```csharp
public System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string,object?>>? Properties { get; }
```

#### Property Value
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[System\.Collections\.Generic\.KeyValuePair&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.keyvaluepair-2 'System\.Collections\.Generic\.KeyValuePair\`2')[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')[,](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.keyvaluepair-2 'System\.Collections\.Generic\.KeyValuePair\`2')[System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.keyvaluepair-2 'System\.Collections\.Generic\.KeyValuePair\`2')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')