#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow')

## CompositionRow\.ShareExplicit\<TValue\>\(CompositionRequestDescriptor, TValue\) Method

Stores [value](Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).md#Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).value 'Compono\.CompositionRow\.ShareExplicit\<TValue\>\(Compono\.CompositionRequestDescriptor, TValue\)\.value') \- already known, not composed \- as this row's shared value for
[TValue](Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).md#Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).TValue 'Compono\.CompositionRow\.ShareExplicit\<TValue\>\(Compono\.CompositionRequestDescriptor, TValue\)\.TValue'), after the same authoritative validation a successful
[ResolveShared&lt;TValue&gt;\(CompositionRequestDescriptor\)](Compono.CompositionRow.ResolveShared_TValue_(Compono.CompositionRequestDescriptor).md 'Compono\.CompositionRow\.ResolveShared\<TValue\>\(Compono\.CompositionRequestDescriptor\)') pipeline result gets\. No
pipeline dispatch, no random fork consumed \- there is nothing left to compose\.

```csharp
public void ShareExplicit<TValue>(in Compono.CompositionRequestDescriptor descriptor, TValue value);
```
#### Type parameters

<a name='Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).TValue'></a>

`TValue`

The shared value's type\.
#### Parameters

<a name='Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).descriptor'></a>

`descriptor` [CompositionRequestDescriptor](Compono.CompositionRequestDescriptor.md 'Compono\.CompositionRequestDescriptor')

The compact, compile\-time\-constructed request metadata\.

<a name='Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).value'></a>

`value` [TValue](Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).md#Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).TValue 'Compono\.CompositionRow\.ShareExplicit\<TValue\>\(Compono\.CompositionRequestDescriptor, TValue\)\.TValue')

The already\-known value to share\.

#### Exceptions

[CompositionException](Compono.CompositionException.md 'Compono\.CompositionException')  
[value](Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).md#Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).value 'Compono\.CompositionRow\.ShareExplicit\<TValue\>\(Compono\.CompositionRequestDescriptor, TValue\)\.value') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a non\-nullable request, or its runtime
            type isn't assignable to [TValue](Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).md#Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).TValue 'Compono\.CompositionRow\.ShareExplicit\<TValue\>\(Compono\.CompositionRequestDescriptor, TValue\)\.TValue'); or a shared value for
            [TValue](Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).md#Compono.CompositionRow.ShareExplicit_TValue_(Compono.CompositionRequestDescriptor,TValue).TValue 'Compono\.CompositionRow\.ShareExplicit\<TValue\>\(Compono\.CompositionRequestDescriptor, TValue\)\.TValue') has already been established in this row\.