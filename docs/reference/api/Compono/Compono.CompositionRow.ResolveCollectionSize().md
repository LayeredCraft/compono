#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow')

## CompositionRow\.ResolveCollectionSize\(\) Method

Resolves the collection size a generated collection plan should build \- the size a
member\-scoped `WithCollectionSize` override configures for the collection member
currently being resolved, falling back to the global default, then the built\-in size of
`3`\. Parameterless: the context already knows the current request's declaring type/member
name \(the same identity `.For<T>().Member(...)` rule matching uses\), since a
collection plan's [Compose\(ICompositionContext\)](Compono.ICompositionPlan_T_.Compose(Compono.ICompositionContext).md 'Compono\.ICompositionPlan\<T\>\.Compose\(Compono\.ICompositionContext\)') has no descriptor to pass\. See
`docs/adr/0020-composition-configuration-rules.md`\.

```csharp
public int ResolveCollectionSize();
```

Implements [ResolveCollectionSize\(\)](Compono.ICompositionContext.ResolveCollectionSize().md 'Compono\.ICompositionContext\.ResolveCollectionSize\(\)')

#### Returns
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')