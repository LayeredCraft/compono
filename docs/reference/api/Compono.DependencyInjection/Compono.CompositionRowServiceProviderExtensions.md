#### [Compono\.DependencyInjection](index.md 'index')
### [Compono](Compono.md 'Compono')

## CompositionRowServiceProviderExtensions Class

The one public entry point of `Compono.DependencyInjection` \- a configured\-resolution
[System\.IServiceProvider](https://learn.microsoft.com/en-us/dotnet/api/system.iserviceprovider 'System\.IServiceProvider') bridge over a [CompositionRow](../Compono/Compono.CompositionRow.md 'Compono\.CompositionRow')\. See
`docs/adr/0047-compono-dependencyinjection-configured-resolution-bridge.md`\.

```csharp
public static class CompositionRowServiceProviderExtensions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → CompositionRowServiceProviderExtensions

| Methods | |
| :--- | :--- |
| [AsServiceProvider\(this CompositionRow\)](Compono.CompositionRowServiceProviderExtensions.AsServiceProvider(thisCompono.CompositionRow).md 'Compono\.CompositionRowServiceProviderExtensions\.AsServiceProvider\(this Compono\.CompositionRow\)') | Wraps [row](Compono.CompositionRowServiceProviderExtensions.AsServiceProvider(thisCompono.CompositionRow).md#Compono.CompositionRowServiceProviderExtensions.AsServiceProvider(thisCompono.CompositionRow).row 'Compono\.CompositionRowServiceProviderExtensions\.AsServiceProvider\(this Compono\.CompositionRow\)\.row') as an [System\.IServiceProvider](https://learn.microsoft.com/en-us/dotnet/api/system.iserviceprovider 'System\.IServiceProvider') backed by [TryResolveConfigured\(Type, object\)](../Compono/Compono.CompositionRow.TryResolveConfigured(System.Type,object).md 'Compono\.CompositionRow\.TryResolveConfigured\(System\.Type,System\.Object@\)'), with stable per\-[System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type') identity for the lifetime of the returned instance: the first successful resolution for a given type is cached, and every later `GetService` call for that same type returns the identical instance \- this is what lets a test configure a double once and have a separately\-rendered consumer \(e\.g\. a bUnit component's `[Inject]`\) observe the same value\. A miss is never cached \- a type unsatisfiable on one call can still be satisfied by a later one, if the row's own configuration changes in between\. |
