#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging').[CapturedLogEntry](Compono.Logging.CapturedLogEntry.md 'Compono\.Logging\.CapturedLogEntry')

## CapturedLogEntry\.Scopes Property

Every scope active at the moment this entry was captured, outermost\-to\-innermost \- matches
[Microsoft\.Extensions\.Logging\.LoggerExternalScopeProvider\.ForEachScope&lt;&gt;\.Action\{System\.Object,&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loggerexternalscopeprovider.foreachscope--1#microsoft-extensions-logging-loggerexternalscopeprovider-foreachscope--1(system-action{system-object---0}---0) 'Microsoft\.Extensions\.Logging\.LoggerExternalScopeProvider\.ForEachScope\`\`1\(System\.Action\{System\.Object,\`\`0\},\`\`0\)')'s
own enumeration order and Microsoft's own `FakeLogRecord.Scopes` ordering\. A snapshot
fixed at capture time \- a scope pushed or disposed afterward never retroactively changes an
already\-captured entry\.

```csharp
public System.Collections.Generic.IReadOnlyList<object> Scopes { get; }
```

#### Property Value
[System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')