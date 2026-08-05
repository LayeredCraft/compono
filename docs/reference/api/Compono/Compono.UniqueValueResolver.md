#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## UniqueValueResolver Class

The bounded, deterministic duplicate\-value retry helper a generated `HashSet<T>`/
`Dictionary<TKey, TValue>` collection plan calls once per element/key position, per
`docs/adr/0013-collection-generation-semantics.md` \(bounded retry, then diagnosable failure\)
and `docs/adr/0014-generator-emitted-collection-plans.md`
\(generated code, not a runtime provider, builds collections\)\.

```csharp
public static class UniqueValueResolver
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → UniqueValueResolver

### Remarks
[public](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/public 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/public') for the same reason [CompositionRequestDescriptor](Compono.CompositionRequestDescriptor.md 'Compono\.CompositionRequestDescriptor')/
            [CompositionRequestKind](Compono.CompositionRequestKind.md 'Compono\.CompositionRequestKind')/[ICompositionContext](Compono.ICompositionContext.md 'Compono\.ICompositionContext') already are: it's part of
            the generated\-code call surface, not the internal engine\.

| Fields | |
| :--- | :--- |
| [MaxAttempts](Compono.UniqueValueResolver.MaxAttempts.md 'Compono\.UniqueValueResolver\.MaxAttempts') | The bounded number of attempts before giving up on a unique value at one position\. |

| Methods | |
| :--- | :--- |
| [TryResolve&lt;TValue&gt;\(ICompositionContext, CompositionRequestKind, int, Nullability, HashSet&lt;TValue&gt;, TValue\)](Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).md 'Compono\.UniqueValueResolver\.TryResolve\<TValue\>\(Compono\.ICompositionContext, Compono\.CompositionRequestKind, int, Compono\.Nullability, System\.Collections\.Generic\.HashSet\<TValue\>, TValue\)') | Attempts to resolve a value for [position](Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).md#Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).position 'Compono\.UniqueValueResolver\.TryResolve\<TValue\>\(Compono\.ICompositionContext, Compono\.CompositionRequestKind, int, Compono\.Nullability, System\.Collections\.Generic\.HashSet\<TValue\>, TValue\)\.position') that isn't already present in [alreadyResolved](Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).md#Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).alreadyResolved 'Compono\.UniqueValueResolver\.TryResolve\<TValue\>\(Compono\.ICompositionContext, Compono\.CompositionRequestKind, int, Compono\.Nullability, System\.Collections\.Generic\.HashSet\<TValue\>, TValue\)\.alreadyResolved'), retrying with a distinct, deterministic fork per attempt\. A successful call both returns the unique value and leaves it already added to [alreadyResolved](Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).md#Compono.UniqueValueResolver.TryResolve_TValue_(Compono.ICompositionContext,Compono.CompositionRequestKind,int,Compono.Nullability,System.Collections.Generic.HashSet_TValue_,TValue).alreadyResolved 'Compono\.UniqueValueResolver\.TryResolve\<TValue\>\(Compono\.ICompositionContext, Compono\.CompositionRequestKind, int, Compono\.Nullability, System\.Collections\.Generic\.HashSet\<TValue\>, TValue\)\.alreadyResolved')\. |
