#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

## CompositionBuilder\.UseServiceProvider\(IServiceProvider\) Method

Configures a native [System\.IServiceProvider](https://learn.microsoft.com/en-us/dotnet/api/system.iserviceprovider 'System\.IServiceProvider') as stage 3's fallback for a type with no
exact [Register&lt;T&gt;\(Func&lt;ICompositionContext,T&gt;\)](Compono.CompositionBuilder.Register.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_) 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)') entry \- tried only after every
exact registration misses, before falling through to stage 4\. See
`docs/adr/0019-registrations-and-service-provider-injection.md` for the full ordering and
null/exception/wrong\-type semantics\.

```csharp
public Compono.CompositionBuilder UseServiceProvider(System.IServiceProvider provider);
```
#### Parameters

<a name='Compono.CompositionBuilder.UseServiceProvider(System.IServiceProvider).provider'></a>

`provider` [System\.IServiceProvider](https://learn.microsoft.com/en-us/dotnet/api/system.iserviceprovider 'System\.IServiceProvider')

The externally\-owned container to fall back to\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

### Remarks
Compono never creates, resolves, or disposes a scope from [provider](Compono.CompositionBuilder.UseServiceProvider(System.IServiceProvider).md#Compono.CompositionBuilder.UseServiceProvider(System.IServiceProvider).provider 'Compono\.CompositionBuilder\.UseServiceProvider\(System\.IServiceProvider\)\.provider') \- it
calls `GetService(Type)` directly and nothing else\. Calling this more than once is a
build\-time conflict, following the same scalar\-fail\-fast rule as [WithSeed\(int\)](Compono.CompositionBuilder.WithSeed(int).md 'Compono\.CompositionBuilder\.WithSeed\(int\)')\.