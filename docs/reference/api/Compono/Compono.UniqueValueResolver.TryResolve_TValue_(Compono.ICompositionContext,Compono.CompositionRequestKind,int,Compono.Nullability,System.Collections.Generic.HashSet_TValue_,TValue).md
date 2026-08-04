#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[UniqueValueResolver](Compono.UniqueValueResolver.md 'Compono\.UniqueValueResolver')

## UniqueValueResolver\.TryResolve\<TValue\>\(ICompositionContext, CompositionRequestKind, int, Nullability, HashSet\<TValue\>, TValue\) Method

Attempts to resolve a value for [position](Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).md#Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).position 'Compono\.UniqueValueResolver\.TryResolve\<TValue\>\(Compono\.ICompositionContext, Compono\.CompositionRequestKind, int, Compono\.Nullability, System\.Collections\.Generic\.HashSet\<TValue\>, TValue\)\.position') that isn't already present in
[alreadyResolved](Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).md#Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).alreadyResolved 'Compono\.UniqueValueResolver\.TryResolve\<TValue\>\(Compono\.ICompositionContext, Compono\.CompositionRequestKind, int, Compono\.Nullability, System\.Collections\.Generic\.HashSet\<TValue\>, TValue\)\.alreadyResolved'), retrying with a distinct, deterministic fork per attempt\.
A successful call both returns the unique value and leaves it already added to
[alreadyResolved](Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).md#Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).alreadyResolved 'Compono\.UniqueValueResolver\.TryResolve\<TValue\>\(Compono\.ICompositionContext, Compono\.CompositionRequestKind, int, Compono\.Nullability, System\.Collections\.Generic\.HashSet\<TValue\>, TValue\)\.alreadyResolved')\.

```csharp
public static bool TryResolve<TValue>(Compono.ICompositionContext context, Compono.CompositionRequestKind kind, int position, Compono.Nullability nullability, System.Collections.Generic.HashSet<TValue> alreadyResolved, out TValue value);
```
#### Type parameters

<a name='Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).TValue'></a>

`TValue`
#### Parameters

<a name='Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).context'></a>

`context` [ICompositionContext](Compono.ICompositionContext.md 'Compono\.ICompositionContext')

The active composition context\.

<a name='Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).kind'></a>

`kind` [CompositionRequestKind](Compono.CompositionRequestKind.md 'Compono\.CompositionRequestKind')

[CollectionElement](Compono.CompositionRequestKind.md#Compono.CompositionRequestKind.CollectionElement 'Compono\.CompositionRequestKind\.CollectionElement') for a `HashSet<T>` element,
            or [DictionaryKey](Compono.CompositionRequestKind.md#Compono.CompositionRequestKind.DictionaryKey 'Compono\.CompositionRequestKind\.DictionaryKey') for a `Dictionary<TKey, TValue>`
            key\.

<a name='Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).position'></a>

`position` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The element/key's logical position in the collection being built\.

<a name='Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).nullability'></a>

`nullability` [Nullability](Compono.Nullability.md 'Compono\.Nullability')

Whether the requested value is nullable\-annotated\.

<a name='Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).alreadyResolved'></a>

`alreadyResolved` [System\.Collections\.Generic\.HashSet&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1 'System\.Collections\.Generic\.HashSet\`1')[TValue](Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).md#Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).TValue 'Compono\.UniqueValueResolver\.TryResolve\<TValue\>\(Compono\.ICompositionContext, Compono\.CompositionRequestKind, int, Compono\.Nullability, System\.Collections\.Generic\.HashSet\<TValue\>, TValue\)\.TValue')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1 'System\.Collections\.Generic\.HashSet\`1')

The set of values already resolved for this collection\.

<a name='Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).value'></a>

`value` [TValue](Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).md#Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).TValue 'Compono\.UniqueValueResolver\.TryResolve\<TValue\>\(Compono\.ICompositionContext, Compono\.CompositionRequestKind, int, Compono\.Nullability, System\.Collections\.Generic\.HashSet\<TValue\>, TValue\)\.TValue')

The resolved unique value, if this call returns [true](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool')\.

#### Returns
[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')  
[false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') if [MaxAttempts](Compono.UniqueValueResolver.MaxAttempts.md 'Compono\.UniqueValueResolver\.MaxAttempts') attempts were exhausted without producing
            a value not already in [alreadyResolved](Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).md#Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).alreadyResolved 'Compono\.UniqueValueResolver\.TryResolve\<TValue\>\(Compono\.ICompositionContext, Compono\.CompositionRequestKind, int, Compono\.Nullability, System\.Collections\.Generic\.HashSet\<TValue\>, TValue\)\.alreadyResolved') \- the caller reports this as a
            [CompositionException](Compono.CompositionException.md 'Compono\.CompositionException') naming the value type and requested count, per ADR\-0013\.