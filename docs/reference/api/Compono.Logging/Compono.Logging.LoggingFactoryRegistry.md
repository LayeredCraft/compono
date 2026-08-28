#### [Compono\.Logging](index.md 'index')
### [Compono\.Logging](Compono.Logging.md 'Compono\.Logging')

## LoggingFactoryRegistry Class

A [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')\-keyed registry of statically\-closed [CapturingLogger&lt;T&gt;](Compono.Logging.CapturingLogger_T_.md 'Compono\.Logging\.CapturingLogger\<T\>')
activators, populated by a `Compono.Logging.Generators`\-emitted `[ModuleInitializer]`
per discovered closed `ILogger<T>` category \- never by this type itself\.

```csharp
public static class LoggingFactoryRegistry
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → LoggingFactoryRegistry

### Remarks

<b>Deliberately public - this is not an oversight.</b> Generated registration code is compiled
            directly into the <em>consumer's own assembly</em>, so an `internal` registry could never be
            called by it for an arbitrary, unknowable consumer assembly name (`InternalsVisibleTo` can't
            solve this generically). This is exact, already-shipped Compono precedent, not a new pattern:
            [GeneratedTestDoubleRegistry](../Compono/Compono.GeneratedTestDoubleRegistry.md 'Compono\.GeneratedTestDoubleRegistry') and [RowInvokerRegistry](../Compono/Compono.RowInvokerRegistry.md 'Compono\.RowInvokerRegistry') are both
            [public](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/public 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/public') for this identical cross-assembly reason. See
            docs/adr/0055-compono-logging-testing-support-package.md's Amendment 2.

This is generator infrastructure, not ordinary consumer-facing usage surface - a
`Compono.Logging` consumer composes through `UseLogging()`, inspects through
[LoggerTestingExtensions](Compono.Logging.LoggerTestingExtensions.md 'Compono\.Logging\.LoggerTestingExtensions'), and constructs [CapturingLogger](Compono.Logging.CapturingLogger.md 'Compono\.Logging\.CapturingLogger')/
[CapturingLogger&lt;T&gt;](Compono.Logging.CapturingLogger_T_.md 'Compono\.Logging\.CapturingLogger\<T\>') directly when bypassing composition; nothing about normal usage
calls this type by hand. Left undecorated with no
[System\.ComponentModel\.EditorBrowsableAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.editorbrowsableattribute 'System\.ComponentModel\.EditorBrowsableAttribute') - matching
[GeneratedTestDoubleRegistry](../Compono/Compono.GeneratedTestDoubleRegistry.md 'Compono\.GeneratedTestDoubleRegistry')/[RowInvokerRegistry](../Compono/Compono.RowInvokerRegistry.md 'Compono\.RowInvokerRegistry')/[PlanCache&lt;T&gt;](../Compono/Compono.PlanCache_T_.md 'Compono\.PlanCache\`1'),
none of which carry that attribute either, per this repo's own documented convention
([RowInvokerRegistry](../Compono/Compono.RowInvokerRegistry.md 'Compono\.RowInvokerRegistry')'s remarks).

[Register&lt;TCategory&gt;\(Func&lt;LoggingOptions,object&gt;\)](Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).md 'Compono\.Logging\.LoggingFactoryRegistry\.Register\<TCategory\>\(System\.Func\<Compono\.Logging\.LoggingOptions,object\>\)') is idempotent - a second registration for a category type
            already present (e.g. from another assembly's own generated module initializer) is a no-op,
            never a throw or an overwrite, matching
            [RegisterFactory&lt;T&gt;\(Func&lt;T&gt;\)](../Compono/Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).md 'Compono\.GeneratedTestDoubleRegistry\.RegisterFactory\`\`1\(System\.Func\{\`\`0\}\)')'s own established behavior for the
            same cross-module-initializer-ordering reason.

| Methods | |
| :--- | :--- |
| [Register&lt;TCategory&gt;\(Func&lt;LoggingOptions,object&gt;\)](Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).md 'Compono\.Logging\.LoggingFactoryRegistry\.Register\<TCategory\>\(System\.Func\<Compono\.Logging\.LoggingOptions,object\>\)') | Idempotently registers [factory](Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).md#Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).factory 'Compono\.Logging\.LoggingFactoryRegistry\.Register\<TCategory\>\(System\.Func\<Compono\.Logging\.LoggingOptions,object\>\)\.factory') as the activator for `ILogger<>`\. [TCategory](Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).md#Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).TCategory 'Compono\.Logging\.LoggingFactoryRegistry\.Register\<TCategory\>\(System\.Func\<Compono\.Logging\.LoggingOptions,object\>\)\.TCategory') is closed statically wherever this is called from generated code \- `typeof(ILogger{TCategory})` here is an ordinary generic\-token load inside this method's own per\-[TCategory](Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).md#Compono.Logging.LoggingFactoryRegistry.Register_TCategory_(System.Func_Compono.Logging.LoggingOptions,object_).TCategory 'Compono\.Logging\.LoggingFactoryRegistry\.Register\<TCategory\>\(System\.Func\<Compono\.Logging\.LoggingOptions,object\>\)\.TCategory') compiled instantiation, never [System\.Type\.MakeGenericType\(System\.Type\[\]\)](https://learn.microsoft.com/en-us/dotnet/api/system.type.makegenerictype#system-type-makegenerictype(system-type[]) 'System\.Type\.MakeGenericType\(System\.Type\[\]\)')\. |
| [TryCreate\(Type, LoggingOptions, object\)](Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).md 'Compono\.Logging\.LoggingFactoryRegistry\.TryCreate\(System\.Type, Compono\.Logging\.LoggingOptions, object\)') | Looks up and invokes the activator registered for [requestedType](Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).md#Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).requestedType 'Compono\.Logging\.LoggingFactoryRegistry\.TryCreate\(System\.Type, Compono\.Logging\.LoggingOptions, object\)\.requestedType'), passing [options](Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).md#Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).options 'Compono\.Logging\.LoggingFactoryRegistry\.TryCreate\(System\.Type, Compono\.Logging\.LoggingOptions, object\)\.options') through untouched \- the caller's live [LoggingOptions](Compono.Logging.LoggingOptions.md 'Compono\.Logging\.LoggingOptions') for the request being resolved, never captured ahead of time by the generated registration itself\. Returns [false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') if no activator has been registered \- either because [requestedType](Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).md#Compono.Logging.LoggingFactoryRegistry.TryCreate(System.Type,Compono.Logging.LoggingOptions,object).requestedType 'Compono\.Logging\.LoggingFactoryRegistry\.TryCreate\(System\.Type, Compono\.Logging\.LoggingOptions, object\)\.requestedType') was never discovered as a composed `ILogger<T>` leaf, or because the consuming assembly's module initializers haven't run yet\. |
