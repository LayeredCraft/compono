#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## GeneratedTestDoubleRegistry Class

A [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')\-keyed registry of factories for generator\-emitted test doubles, populated by
a generated `[ModuleInitializer]` per discovered interface \(never by `Compono` itself\),
the same cross\-assembly\-population shape as [RowInvokerRegistry](Compono.RowInvokerRegistry.md 'Compono\.RowInvokerRegistry')\. Read by
`Compono.TestDoubles`'s `GeneratedTestDoubleProvider` \- core `Compono` has no
reference the other way\. See ADR\-0043 Amendment 2\.

```csharp
public static class GeneratedTestDoubleRegistry
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → GeneratedTestDoubleRegistry

### Remarks

Unlike [RowInvokerRegistry](Compono.RowInvokerRegistry.md 'Compono\.RowInvokerRegistry'), whose duplicate registrations for the same
[System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type') are genuinely interchangeable, two different assemblies generating a double
for the same shared interface produce two distinct, non-interchangeable concrete types.
[RegisterFactory&lt;T&gt;\(Func&lt;T&gt;\)](Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).md 'Compono\.GeneratedTestDoubleRegistry\.RegisterFactory\<T\>\(System\.Func\<T\>\)') is still first-registration-wins (via
[System\.Collections\.Concurrent\.ConcurrentDictionary&lt;&gt;\.GetOrAdd\(@0,@1\)](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd#system-collections-concurrent-concurrentdictionary-2-getoradd(-0--1) 'System\.Collections\.Concurrent\.ConcurrentDictionary\`2\.GetOrAdd\(\`0,\`1\)'), deterministic, never a
throw or blind overwrite) - a documented v1 limitation for the multi-assembly-same-interface
scenario, not a bug: the generated `Configure()` bridge's cast-failure message names this
exact scenario so a consumer who hits it understands why. See ADR-0043 Amendment 3, Finding C.

Every entry stored here permanently roots its registered factory delegate (and the generating
assembly) for the process's lifetime - the same collectible-[System\.Runtime\.Loader\.AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext 'System\.Runtime\.Loader\.AssemblyLoadContext')-rooting
consequence already documented for [RowInvokerRegistry](Compono.RowInvokerRegistry.md 'Compono\.RowInvokerRegistry'). See ADR-0043 Amendment 5,
Finding M, and `docs/architecture/current/generated-plans-and-discovery.md`'s "Open
questions" section (Phase 3 doc task).

| Methods | |
| :--- | :--- |
| [RegisterFactory&lt;T&gt;\(Func&lt;T&gt;\)](Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).md 'Compono\.GeneratedTestDoubleRegistry\.RegisterFactory\<T\>\(System\.Func\<T\>\)') | Idempotently registers [factory](Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).md#Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).factory 'Compono\.GeneratedTestDoubleRegistry\.RegisterFactory\<T\>\(System\.Func\<T\>\)\.factory') for [T](Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).md#Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).T 'Compono\.GeneratedTestDoubleRegistry\.RegisterFactory\<T\>\(System\.Func\<T\>\)\.T') \- a second registration for a [T](Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).md#Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).T 'Compono\.GeneratedTestDoubleRegistry\.RegisterFactory\<T\>\(System\.Func\<T\>\)\.T') already present \(e\.g\. from another assembly's own generated module initializer\) is a no\-op, never a throw or an overwrite\. |
| [TryCreate\(Type, object\)](Compono.GeneratedTestDoubleRegistry.TryCreate(System.Type,object).md 'Compono\.GeneratedTestDoubleRegistry\.TryCreate\(System\.Type, object\)') | Looks up and invokes the factory registered for [requestedType](Compono.GeneratedTestDoubleRegistry.TryCreate(System.Type,object).md#Compono.GeneratedTestDoubleRegistry.TryCreate(System.Type,object).requestedType 'Compono\.GeneratedTestDoubleRegistry\.TryCreate\(System\.Type, object\)\.requestedType'), or [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') if none has been registered \- either because [requestedType](Compono.GeneratedTestDoubleRegistry.TryCreate(System.Type,object).md#Compono.GeneratedTestDoubleRegistry.TryCreate(System.Type,object).requestedType 'Compono\.GeneratedTestDoubleRegistry\.TryCreate\(System\.Type, object\)\.requestedType') was never discovered as a generated\-test\-double leaf, or because the consuming assembly's module initializers haven't run yet\. |
