#### [Compono\.Http](index.md 'index')
### [Compono\.Http](Compono.Http.md 'Compono\.Http').[TestHttpHandler](Compono.Http.TestHttpHandler.md 'Compono\.Http\.TestHttpHandler')

## TestHttpHandler\.Requests Property

Every request that has reached this handler's `Compono.Http.TestHttpHandler.SendAsync(System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken)`, in arrival order,
matched or not \- a fresh point\-in\-time snapshot on every access, never a live view over the
mutable backing log \(ADR\-0051 "Request log"\)\. Kept deliberately separate from
[Verify\(\)](Compono.Http.HttpResponseRegistration.Verify().md 'Compono\.Http\.HttpResponseRegistration\.Verify\(\)') \- this answers "what did the system under test
actually send," not "how many times did one configured behavior match\."

```csharp
public System.Collections.Generic.IReadOnlyList<System.Net.Http.HttpRequestMessage> Requests { get; }
```

#### Property Value
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[System\.Net\.Http\.HttpRequestMessage](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httprequestmessage 'System\.Net\.Http\.HttpRequestMessage')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')