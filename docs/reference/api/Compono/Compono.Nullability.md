#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## Nullability Enum

Whether a value requested from [Resolve&lt;TValue&gt;\(CompositionRequestDescriptor\)](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_(Compono.CompositionRequestDescriptor) 'Compono\.ICompositionContext\.Resolve\<TValue\>\(Compono\.CompositionRequestDescriptor\)') came from a
nullable\-annotated parameter or required member \(a `string?`, not a `string`\)\.

```csharp
public enum Nullability
```
### Fields

<a name='Compono.Nullability.NotNullable'></a>

`NotNullable` 0

The requested parameter or member is not nullable\-annotated\.

<a name='Compono.Nullability.Nullable'></a>

`Nullable` 1

The requested parameter or member is nullable\-annotated\.

### Remarks
Reference\-type nullable annotations are erased from the runtime generic type argument \-
`Resolve<string>()` and a hypothetical `Resolve<string?>()` are the same
closed generic method at runtime \- so a generated [ICompositionPlan&lt;T&gt;](Compono.ICompositionPlan_T_.md 'Compono\.ICompositionPlan\<T\>') passes this
explicitly rather than relying on the requested type argument to carry it\. See
`docs/adr/0006-required-members-and-nullability-metadata.md`\.