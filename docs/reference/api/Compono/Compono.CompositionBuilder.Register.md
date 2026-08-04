#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

## CompositionBuilder\.Register Method

| Overloads | |
| :--- | :--- |
| [Register&lt;T&gt;\(Func&lt;ICompositionContext,T&gt;\)](Compono.CompositionBuilder.Register.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_) 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)') | Registers an exact\-type factory for [T](Compono.CompositionBuilder.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_).T 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)\.T') \- pipeline stage 3 \(`docs/architecture.md`\) resolves [T](Compono.CompositionBuilder.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_).T 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)\.T') by invoking [factory](Compono.CompositionBuilder.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_).factory 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)\.factory'), called through [ICompositionContext](Compono.ICompositionContext.md 'Compono\.ICompositionContext') exactly like generated code's own resolution, so [factory](Compono.CompositionBuilder.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_).factory 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)\.factory') can call [Resolve&lt;TValue&gt;\(\)](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_() 'Compono\.ICompositionContext\.Resolve\<TValue\>\(\)') to compose nested dependencies\. |
| [Register&lt;T&gt;\(Func&lt;T&gt;\)](Compono.CompositionBuilder.Register.md#Compono.CompositionBuilder.Register_T_(System.Func_T_) 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<T\>\)') | Registers an exact\-type factory for [T](Compono.CompositionBuilder.md#Compono.CompositionBuilder.Register_T_(System.Func_T_).T 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<T\>\)\.T') with no dependency on the resolving context \- the convenience overload for [Register&lt;T&gt;\(Func&lt;ICompositionContext,T&gt;\)](Compono.CompositionBuilder.Register.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_) 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)')'s common no\-dependency case \(e\.g\. `Register<IClock>(() => new FakeClock())`\)\. |

<a name='Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_)'></a>

## CompositionBuilder\.Register\<T\>\(Func\<ICompositionContext,T\>\) Method

Registers an exact\-type factory for [T](Compono.CompositionBuilder.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_).T 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)\.T') \- pipeline stage 3
\(`docs/architecture.md`\) resolves [T](Compono.CompositionBuilder.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_).T 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)\.T') by invoking
[factory](Compono.CompositionBuilder.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_).factory 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)\.factory'), called through [ICompositionContext](Compono.ICompositionContext.md 'Compono\.ICompositionContext') exactly like
generated code's own resolution, so [factory](Compono.CompositionBuilder.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_).factory 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)\.factory') can call
[Resolve&lt;TValue&gt;\(\)](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_() 'Compono\.ICompositionContext\.Resolve\<TValue\>\(\)') to compose nested dependencies\.

```csharp
public Compono.CompositionBuilder Register<T>(System.Func<Compono.ICompositionContext,T> factory);
```
#### Type parameters

<a name='Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_).T'></a>

`T`

The exact type to register a factory for\.
#### Parameters

<a name='Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_).factory'></a>

`factory` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[ICompositionContext](Compono.ICompositionContext.md 'Compono\.ICompositionContext')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[T](Compono.CompositionBuilder.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_).T 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)\.T')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')

Produces the registered value, given the resolving context\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

### Remarks
Registering the same [T](Compono.CompositionBuilder.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_).T 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)\.T') more than once \(directly, or once directly and
once from a profile, or from two different profiles\) is a build\-time conflict, not last\-write\-
wins \- see `docs/adr/0019-registrations-and-service-provider-injection.md`'s deliberately
strict throw\-on\-duplicate decision\.

<a name='Compono.CompositionBuilder.Register_T_(System.Func_T_)'></a>

## CompositionBuilder\.Register\<T\>\(Func\<T\>\) Method

Registers an exact\-type factory for [T](Compono.CompositionBuilder.md#Compono.CompositionBuilder.Register_T_(System.Func_T_).T 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<T\>\)\.T') with no dependency on the
resolving context \- the convenience overload for [Register&lt;T&gt;\(Func&lt;ICompositionContext,T&gt;\)](Compono.CompositionBuilder.Register.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_) 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)')'s
common no\-dependency case \(e\.g\. `Register<IClock>(() => new FakeClock())`\)\.

```csharp
public Compono.CompositionBuilder Register<T>(System.Func<T> factory);
```
#### Type parameters

<a name='Compono.CompositionBuilder.Register_T_(System.Func_T_).T'></a>

`T`

The exact type to register a factory for\.
#### Parameters

<a name='Compono.CompositionBuilder.Register_T_(System.Func_T_).factory'></a>

`factory` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-1 'System\.Func\`1')[T](Compono.CompositionBuilder.md#Compono.CompositionBuilder.Register_T_(System.Func_T_).T 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<T\>\)\.T')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-1 'System\.Func\`1')

Produces the registered value\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')