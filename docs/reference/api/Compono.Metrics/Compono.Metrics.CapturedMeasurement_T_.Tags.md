#### [Compono\.Metrics](index.md 'index')
### [Compono\.Metrics](Compono.Metrics.md 'Compono\.Metrics').[CapturedMeasurement&lt;T&gt;](Compono.Metrics.CapturedMeasurement_T_.md 'Compono\.Metrics\.CapturedMeasurement\<T\>')

## CapturedMeasurement\<T\>\.Tags Property

This measurement's tags, copied out of the BCL's callback span in callback order\. A per\-measurement
copy that never aliases the BCL's span or any other measurement's tags; empty \(never
[null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\) when the measurement had no tags\. Meter\-level and instrument\-level tags are
not merged in \- they're reachable through [Instrument](Compono.Metrics.CapturedMeasurement_T_.Instrument.md 'Compono\.Metrics\.CapturedMeasurement\<T\>\.Instrument')\.

```csharp
public System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string,object?>> Tags { get; }
```

#### Property Value
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[System\.Collections\.Generic\.KeyValuePair&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.keyvaluepair-2 'System\.Collections\.Generic\.KeyValuePair\`2')[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')[,](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.keyvaluepair-2 'System\.Collections\.Generic\.KeyValuePair\`2')[System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.keyvaluepair-2 'System\.Collections\.Generic\.KeyValuePair\`2')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')