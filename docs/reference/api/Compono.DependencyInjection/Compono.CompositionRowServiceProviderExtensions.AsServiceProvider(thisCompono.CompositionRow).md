#### [Compono\.DependencyInjection](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionRowServiceProviderExtensions](Compono.CompositionRowServiceProviderExtensions.md 'Compono\.CompositionRowServiceProviderExtensions')

## CompositionRowServiceProviderExtensions\.AsServiceProvider\(this CompositionRow\) Method

Wraps [row](Compono.CompositionRowServiceProviderExtensions.AsServiceProvider(thisCompono.CompositionRow).md#Compono.CompositionRowServiceProviderExtensions.AsServiceProvider(thisCompono.CompositionRow).row 'Compono\.CompositionRowServiceProviderExtensions\.AsServiceProvider\(this Compono\.CompositionRow\)\.row') as an [System\.IServiceProvider](https://learn.microsoft.com/en-us/dotnet/api/system.iserviceprovider 'System\.IServiceProvider') backed by
[TryResolveConfigured\(Type, object\)](../Compono/Compono.CompositionRow.TryResolveConfigured(System.Type,object).md 'Compono\.CompositionRow\.TryResolveConfigured\(System\.Type,System\.Object@\)'), with stable per\-[System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')
identity for the lifetime of the returned instance: the first successful resolution for a
given type is cached, and every later `GetService` call for that same type returns the
identical instance \- this is what lets a test configure a double once and have a
separately\-rendered consumer \(e\.g\. a bUnit component's `[Inject]`\) observe the same
value\. A miss is never cached \- a type unsatisfiable on one call can still be satisfied by
a later one, if the row's own configuration changes in between\. Calling this more than once
for the same [row](Compono.CompositionRowServiceProviderExtensions.AsServiceProvider(thisCompono.CompositionRow).md#Compono.CompositionRowServiceProviderExtensions.AsServiceProvider(thisCompono.CompositionRow).row 'Compono\.CompositionRowServiceProviderExtensions\.AsServiceProvider\(this Compono\.CompositionRow\)\.row') returns the identical [System\.IServiceProvider](https://learn.microsoft.com/en-us/dotnet/api/system.iserviceprovider 'System\.IServiceProvider')
instance every time, not a fresh one \- this is what makes the whole surface, including
concurrent `GetService` calls made through two separately\-obtained references, safe to
use concurrently: no corruption, no torn state, no two same\-type callers ever observing
different instances\. It is NOT a promise that which of two DIFFERENT types resolves first is
itself deterministic when both are requested concurrently for the first time \- see the
concurrency remark below for what that means for randomness\-dependent factories/providers\.

```csharp
public static System.IServiceProvider AsServiceProvider(this Compono.CompositionRow row);
```
#### Parameters

<a name='Compono.CompositionRowServiceProviderExtensions.AsServiceProvider(thisCompono.CompositionRow).row'></a>

`row` [CompositionRow](../Compono/Compono.CompositionRow.md 'Compono\.CompositionRow')

#### Returns
[System\.IServiceProvider](https://learn.microsoft.com/en-us/dotnet/api/system.iserviceprovider 'System\.IServiceProvider')

### Remarks
Wiring a DIFFERENT row's `UseServiceProvider` with the result of this call, where that
row itself \(directly or transitively\) resolves back into this row, is discouraged but not
unguarded: a [CompositionRow](../Compono/Compono.CompositionRow.md 'Compono\.CompositionRow')'s underlying context is created once and reused for
every call made on it, so a SEQUENTIAL cycle re\-enters the same registration factory or
provider on that context while it is still active, and Compono's existing reentrance guard
raises a diagnosed [CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException') \- it does not overflow the stack\. A
CONCURRENT cross\-row cycle \(the two rows' calls arrive on different threads at the same time\)
is different: each row's adapter can be waiting to acquire the OTHER row's adapter lock while
holding its own, which the reentrance guard above never gets a chance to see \- this method
bounds that wait instead of blocking forever, surfacing a [System\.TimeoutException](https://learn.microsoft.com/en-us/dotnet/api/system.timeoutexception 'System\.TimeoutException')
\(wrapped in a [CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException') if it fires inside a registration/
configuration\-rule factory, per this method's own exception contract\) rather than an
unrecoverable hang\. See ADR\-0047's Recursion section and Amendment 4/5\.

This adapter fully serializes `GetService` calls made through it (see above), so two
concurrent first-time requests for two DIFFERENT types on the SAME row never race on shared
state - but whichever request's caller happens to acquire that internal serialization first
is not itself a deterministic fact this method controls. For a randomness-dependent factory
or provider (one that calls `ctx.DeriveSeed()` or otherwise reads structural position),
this means the derived value for a given type can differ across runs when two or more types
are resolved concurrently for the first time on a fixed seed - sequential resolution is
unaffected; only genuinely concurrent first-time requests carry this caveat. See ADR-0047
Amendment 5.

The returned [System\.IServiceProvider](https://learn.microsoft.com/en-us/dotnet/api/system.iserviceprovider 'System\.IServiceProvider') does not own or dispose anything it resolves and
caches - if a resolved value implements [System\.IDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable 'System\.IDisposable')/[System\.IAsyncDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.iasyncdisposable 'System\.IAsyncDisposable'),
disposing it is the caller's responsibility, exactly as it would be for a value the caller
constructed by hand. This matches [CompositionRow](../Compono/Compono.CompositionRow.md 'Compono\.CompositionRow')/[Composer](../Compono/Compono.Composer.md 'Compono\.Composer')'s own
lack of any disposal contract - this bridge does not introduce one.