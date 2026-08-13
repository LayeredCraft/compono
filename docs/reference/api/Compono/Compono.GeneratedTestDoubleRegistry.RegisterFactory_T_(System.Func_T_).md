#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[GeneratedTestDoubleRegistry](Compono.GeneratedTestDoubleRegistry.md 'Compono\.GeneratedTestDoubleRegistry')

## GeneratedTestDoubleRegistry\.RegisterFactory\<T\>\(Func\<T\>\) Method

Idempotently registers [factory](Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).md#Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).factory 'Compono\.GeneratedTestDoubleRegistry\.RegisterFactory\<T\>\(System\.Func\<T\>\)\.factory') for [T](Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).md#Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).T 'Compono\.GeneratedTestDoubleRegistry\.RegisterFactory\<T\>\(System\.Func\<T\>\)\.T') \- a second
registration for a [T](Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).md#Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).T 'Compono\.GeneratedTestDoubleRegistry\.RegisterFactory\<T\>\(System\.Func\<T\>\)\.T') already present \(e\.g\. from another assembly's
own generated module initializer\) is a no\-op, never a throw or an overwrite\.

```csharp
public static void RegisterFactory<T>(System.Func<T> factory)
    where T : class;
```
#### Type parameters

<a name='Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).T'></a>

`T`
#### Parameters

<a name='Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).factory'></a>

`factory` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-1 'System\.Func\`1')[T](Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).md#Compono.GeneratedTestDoubleRegistry.RegisterFactory_T_(System.Func_T_).T 'Compono\.GeneratedTestDoubleRegistry\.RegisterFactory\<T\>\(System\.Func\<T\>\)\.T')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-1 'System\.Func\`1')