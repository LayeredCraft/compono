#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## ShareExplicitInvoker\(CompositionRow, CompositionRequestDescriptor, object\) Delegate

Non\-generic delegate shapes every registered invoker is adapted to, regardless of the closed
parameter type \- what lets [RowInvokerRegistry](Compono.RowInvokerRegistry.md 'Compono\.RowInvokerRegistry')'s caller dispatch with no reflection
at all, only an ordinary delegate invocation\. Moved here from what was previously
`Compono.XunitV3.Binding.RowInvokers`' own local definition, so core and every integration
package share one definition instead of each declaring its own\. See ADR\-0041\.

```csharp
public delegate void ShareExplicitInvoker(Compono.CompositionRow row, in Compono.CompositionRequestDescriptor descriptor, object? value);
```
#### Parameters

<a name='Compono.ShareExplicitInvoker(Compono.CompositionRow,Compono.CompositionRequestDescriptor,object).row'></a>

`row` [CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow')

<a name='Compono.ShareExplicitInvoker(Compono.CompositionRow,Compono.CompositionRequestDescriptor,object).descriptor'></a>

`descriptor` [CompositionRequestDescriptor](Compono.CompositionRequestDescriptor.md 'Compono\.CompositionRequestDescriptor')

<a name='Compono.ShareExplicitInvoker(Compono.CompositionRow,Compono.CompositionRequestDescriptor,object).value'></a>

`value` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')