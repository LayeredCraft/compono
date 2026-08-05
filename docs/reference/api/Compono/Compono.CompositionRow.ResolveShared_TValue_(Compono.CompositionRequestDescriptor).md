#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow')

## CompositionRow\.ResolveShared\<TValue\>\(CompositionRequestDescriptor\) Method

Composes [TValue](Compono.CompositionRow.ResolveShared_TValue_(Compono.CompositionRequestDescriptor).md#Compono.CompositionRow.ResolveShared_TValue_(Compono.CompositionRequestDescriptor).TValue 'Compono\.CompositionRow\.ResolveShared\<TValue\>\(Compono\.CompositionRequestDescriptor\)\.TValue') through the same pipeline
[Resolve&lt;TValue&gt;\(CompositionRequestDescriptor\)](Compono.CompositionRow.Resolve.md#Compono.CompositionRow.Resolve_TValue_(Compono.CompositionRequestDescriptor) 'Compono\.CompositionRow\.Resolve\<TValue\>\(Compono\.CompositionRequestDescriptor\)') uses, and additionally stores
the successful result into this row's shared scope \- a later request for the same type in this
row, including one made by a nested generated plan, reuses it instead of composing its own
independent value\.

```csharp
public TValue ResolveShared<TValue>(in Compono.CompositionRequestDescriptor descriptor);
```
#### Type parameters

<a name='Compono.CompositionRow.ResolveShared_TValue_(Compono.CompositionRequestDescriptor).TValue'></a>

`TValue`

The requested value's type\.
#### Parameters

<a name='Compono.CompositionRow.ResolveShared_TValue_(Compono.CompositionRequestDescriptor).descriptor'></a>

`descriptor` [CompositionRequestDescriptor](Compono.CompositionRequestDescriptor.md 'Compono\.CompositionRequestDescriptor')

The compact, compile\-time\-constructed request metadata\.

#### Returns
[TValue](Compono.CompositionRow.ResolveShared_TValue_(Compono.CompositionRequestDescriptor).md#Compono.CompositionRow.ResolveShared_TValue_(Compono.CompositionRequestDescriptor).TValue 'Compono\.CompositionRow\.ResolveShared\<TValue\>\(Compono\.CompositionRequestDescriptor\)\.TValue')

#### Exceptions

[CompositionException](Compono.CompositionException.md 'Compono\.CompositionException')  
No explicit value, shared value, registration, provider, or generated plan could satisfy the
request; or the pipeline\-produced value is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a non\-nullable request,
or its runtime type isn't assignable to [TValue](Compono.CompositionRow.ResolveShared_TValue_(Compono.CompositionRequestDescriptor).md#Compono.CompositionRow.ResolveShared_TValue_(Compono.CompositionRequestDescriptor).TValue 'Compono\.CompositionRow\.ResolveShared\<TValue\>\(Compono\.CompositionRequestDescriptor\)\.TValue'); or a shared value for
[TValue](Compono.CompositionRow.ResolveShared_TValue_(Compono.CompositionRequestDescriptor).md#Compono.CompositionRow.ResolveShared_TValue_(Compono.CompositionRequestDescriptor).TValue 'Compono\.CompositionRow\.ResolveShared\<TValue\>\(Compono\.CompositionRequestDescriptor\)\.TValue') has already been established in this row\.