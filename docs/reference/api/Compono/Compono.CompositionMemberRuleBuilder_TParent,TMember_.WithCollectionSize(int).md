#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionMemberRuleBuilder&lt;TParent,TMember&gt;](Compono.CompositionMemberRuleBuilder_TParent,TMember_.md 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>')

## CompositionMemberRuleBuilder\<TParent,TMember\>\.WithCollectionSize\(int\) Method

Overrides the default collection size for this member only, following the same
[WithCollectionSize\(int\)](Compono.CompositionBuilder.WithCollectionSize(int).md 'Compono\.CompositionBuilder\.WithCollectionSize\(int\)') precedence: this override wins over the
global default and the built\-in size of `3`\.

```csharp
public Compono.CompositionBuilder WithCollectionSize(int size);
```
#### Parameters

<a name='Compono.CompositionMemberRuleBuilder_TParent,TMember_.WithCollectionSize(int).size'></a>

`size` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The collection size to build for this member\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

#### Exceptions

[System\.ArgumentOutOfRangeException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception 'System\.ArgumentOutOfRangeException')  
[size](Compono.CompositionMemberRuleBuilder_TParent,TMember_.WithCollectionSize(int).md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.WithCollectionSize(int).size 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.WithCollectionSize\(int\)\.size') is negative\.