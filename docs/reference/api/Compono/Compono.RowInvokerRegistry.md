#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## RowInvokerRegistry Class

A non\-generic, [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')\-keyed registry of [Resolve&lt;TValue&gt;\(CompositionRequestDescriptor\)](Compono.CompositionRow.Resolve.md#Compono.CompositionRow.Resolve_TValue_(Compono.CompositionRequestDescriptor) 'Compono\.CompositionRow\.Resolve\<TValue\>\(Compono\.CompositionRequestDescriptor\)')/
[ResolveShared&lt;TValue&gt;\(CompositionRequestDescriptor\)](Compono.CompositionRow.ResolveShared_TValue_(Compono.CompositionRequestDescriptor).md 'Compono\.CompositionRow\.ResolveShared\<TValue\>\(Compono\.CompositionRequestDescriptor\)')/
[ShareExplicit&lt;TValue&gt;\(CompositionRequestDescriptor, TValue\)](Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).md 'Compono\.CompositionRow\.ShareExplicit\<TValue\>\(Compono\.CompositionRequestDescriptor, TValue\)') dispatch
delegates, per ADR\-0041 Amendment 2\.

```csharp
public static class RowInvokerRegistry
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → RowInvokerRegistry

### Remarks
A test\-framework integration's binding algorithm only ever has a runtime [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type') for a
test parameter \(reflected off the test method's own signature\), never a compile\-time `T` \-
unlike [PlanCache&lt;T&gt;](Compono.PlanCache_T_.md 'Compono\.PlanCache\<T\>'), whose only reader is itself generic with `T` bound at a real
call site, there is no call site here to bind a closed generic field against\. A [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')
object is an ordinary runtime value under Native AOT; only \*dynamic instantiation\* of a generic
method/type from one \(`MethodInfo.MakeGenericMethod`\) is unsafe, and this registry never does
that \- every `Resolve<T>()`/`ResolveShared<T>()`/`ShareExplicit<T>()`
call a registered entry actually makes is written directly, with a compile\-time\-known `T`, in
generator\-emitted source\.

[Register\(Type, ResolveInvoker, ResolveSharedInvoker, ShareExplicitInvoker\)](Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).md 'Compono\.RowInvokerRegistry\.Register\(System\.Type, Compono\.ResolveInvoker, Compono\.ResolveSharedInvoker, Compono\.ShareExplicitInvoker\)') is populated by a generated module initializer in the consuming assembly
            (never by `Compono` itself), the same cross-assembly reason [PlanCache&lt;T&gt;](Compono.PlanCache_T_.md 'Compono\.PlanCache\<T\>')'s own
            setter is [public](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/public 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/public') despite `coding-standards.md`'s "no static singletons" rule.
            Two consuming assemblies loaded into the same process that both discover the same parameter type
            (e.g. both composing [string](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/string 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/string') as a `[Compose]` parameter) each run their own
            generated module initializer against this same registry - backed by a
            [System\.Collections\.Concurrent\.ConcurrentDictionary&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2 'System\.Collections\.Concurrent\.ConcurrentDictionary\`2') with an atomic [System\.Collections\.Concurrent\.ConcurrentDictionary&lt;&gt;\.GetOrAdd\(@0,@1\)](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd#system-collections-concurrent-concurrentdictionary-2-getoradd(-0--1) 'System\.Collections\.Concurrent\.ConcurrentDictionary\`2\.GetOrAdd\(\`0,\`1\)'),
            never a throwing or blind-overwrite registration, because a plain, non-concurrent dictionary's
            internal structure can corrupt under genuinely concurrent writes from two module initializers
            running on different threads during assembly load - a strictly worse failure mode than "last write
            wins." This is safe specifically because every registration for a given [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type') is
            functionally interchangeable regardless of which assembly generated it (the emitted lambda is
            always the same shape, `(row, descriptor) => row.Resolve<T>(descriptor)`, for the same
            `T`) - unlike [PlanCache&lt;T&gt;](Compono.PlanCache_T_.md 'Compono\.PlanCache\<T\>')'s own genuine "which plan is correct" ambiguity, there
            is no real question to defer here (ADR-0041 Amendment 3).

[Register\(Type, ResolveInvoker, ResolveSharedInvoker, ShareExplicitInvoker\)](Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).md 'Compono\.RowInvokerRegistry\.Register\(System\.Type, Compono\.ResolveInvoker, Compono\.ResolveSharedInvoker, Compono\.ShareExplicitInvoker\)') is hidden from IntelliSense because only generated consumer-assembly code
            calls it. [TryGet\(Type, ResolveInvoker, ResolveSharedInvoker, ShareExplicitInvoker\)](Compono.RowInvokerRegistry.TryGet(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).md 'Compono\.RowInvokerRegistry\.TryGet\(System\.Type, Compono\.ResolveInvoker, Compono\.ResolveSharedInvoker, Compono\.ShareExplicitInvoker\)') remains visible because test-framework integration packages call it.
            See ADR-0058.

Every entry stored here permanently roots its registered delegates (and the generating assembly) -
this registry has no closed-generic-instantiation home-context tie the way [PlanCache&lt;T&gt;](Compono.PlanCache_T_.md 'Compono\.PlanCache\<T\>')/
[CollectionPlanCache&lt;T&gt;](Compono.CollectionPlanCache_T_.md 'Compono\.CollectionPlanCache\<T\>') do, so the limitation is broader in scope than
[CollectionPlanCache&lt;T&gt;](Compono.CollectionPlanCache_T_.md 'Compono\.CollectionPlanCache\<T\>')'s own already-documented one (scoped only to collections whose
type arguments are entirely BCL types). Deferred, same disposition as that existing limitation - see
`docs/architecture/current/generated-plans-and-discovery.md`'s collectible-[System\.Runtime\.Loader\.AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext 'System\.Runtime\.Loader\.AssemblyLoadContext')-rooting
note, extended to name this registry.

| Methods | |
| :--- | :--- |
| [Register\(Type, ResolveInvoker, ResolveSharedInvoker, ShareExplicitInvoker\)](Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).md 'Compono\.RowInvokerRegistry\.Register\(System\.Type, Compono\.ResolveInvoker, Compono\.ResolveSharedInvoker, Compono\.ShareExplicitInvoker\)') | Idempotently registers the three dispatch delegates for [type](Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).md#Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).type 'Compono\.RowInvokerRegistry\.Register\(System\.Type, Compono\.ResolveInvoker, Compono\.ResolveSharedInvoker, Compono\.ShareExplicitInvoker\)\.type') \- a second registration for a [type](Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).md#Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).type 'Compono\.RowInvokerRegistry\.Register\(System\.Type, Compono\.ResolveInvoker, Compono\.ResolveSharedInvoker, Compono\.ShareExplicitInvoker\)\.type') already present \(e\.g\. from another assembly's own generated module initializer\) is a no\-op, never a throw or an overwrite\. |
| [TryGet\(Type, ResolveInvoker, ResolveSharedInvoker, ShareExplicitInvoker\)](Compono.RowInvokerRegistry.TryGet(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).md 'Compono\.RowInvokerRegistry\.TryGet\(System\.Type, Compono\.ResolveInvoker, Compono\.ResolveSharedInvoker, Compono\.ShareExplicitInvoker\)') | Looks up the three dispatch delegates registered for [type](Compono.RowInvokerRegistry.TryGet(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).md#Compono.RowInvokerRegistry.TryGet(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).type 'Compono\.RowInvokerRegistry\.TryGet\(System\.Type, Compono\.ResolveInvoker, Compono\.ResolveSharedInvoker, Compono\.ShareExplicitInvoker\)\.type'), or [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') if none has been registered \- either because [type](Compono.RowInvokerRegistry.TryGet(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).md#Compono.RowInvokerRegistry.TryGet(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).type 'Compono\.RowInvokerRegistry\.TryGet\(System\.Type, Compono\.ResolveInvoker, Compono\.ResolveSharedInvoker, Compono\.ShareExplicitInvoker\)\.type') was never discovered at a dispatch\-eligible `[Compose]`\-family parameter, or because the consuming assembly's module initializers haven't run yet\. |
