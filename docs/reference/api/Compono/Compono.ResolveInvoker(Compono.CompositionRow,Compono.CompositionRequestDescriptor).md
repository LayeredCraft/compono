#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## ResolveInvoker\(CompositionRow, CompositionRequestDescriptor\) Delegate

Non\-generic delegate shapes every registered invoker is adapted to, regardless of the closed
parameter type \- what lets [RowInvokerRegistry](Compono.RowInvokerRegistry.md 'Compono\.RowInvokerRegistry')'s caller dispatch with no reflection
at all, only an ordinary delegate invocation\. Moved here from what was previously
`Compono.XunitV3.Binding.RowInvokers`' own local definition, so core and every integration
package share one definition instead of each declaring its own\. See ADR\-0041\.

```csharp
public delegate object? ResolveInvoker(Compono.CompositionRow row, in Compono.CompositionRequestDescriptor descriptor);
```
#### Parameters

<a name='Compono.ResolveInvoker(Compono.CompositionRow,Compono.CompositionRequestDescriptor).row'></a>

`row` [CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow')

<a name='Compono.ResolveInvoker(Compono.CompositionRow,Compono.CompositionRequestDescriptor).descriptor'></a>

`descriptor` [CompositionRequestDescriptor](Compono.CompositionRequestDescriptor.md 'Compono\.CompositionRequestDescriptor')

#### Returns
[System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')