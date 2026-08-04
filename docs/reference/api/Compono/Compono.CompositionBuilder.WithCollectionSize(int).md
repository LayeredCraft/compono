#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

## CompositionBuilder\.WithCollectionSize\(int\) Method

Sets this composer's global default collection size \- the size a generated collection plan
builds when no member\-scoped `.For<T>().Member(x => x.Y).WithCollectionSize(...)`
override applies\. Falls back to the built\-in size of `3` if never called\. See
`docs/adr/0020-composition-configuration-rules.md`\.

```csharp
public Compono.CompositionBuilder WithCollectionSize(int size);
```
#### Parameters

<a name='Compono.CompositionBuilder.WithCollectionSize(int).size'></a>

`size` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The default collection size\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

#### Exceptions

[System\.ArgumentOutOfRangeException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception 'System\.ArgumentOutOfRangeException')  
[size](Compono.CompositionBuilder.WithCollectionSize(int).md#Compono.CompositionBuilder.WithCollectionSize(int).size 'Compono\.CompositionBuilder\.WithCollectionSize\(int\)\.size') is negative\.

### Remarks
Calling this more than once is a build\-time conflict, following the same scalar\-fail\-fast rule
as [WithSeed\(int\)](Compono.CompositionBuilder.WithSeed(int).md 'Compono\.CompositionBuilder\.WithSeed\(int\)')\.