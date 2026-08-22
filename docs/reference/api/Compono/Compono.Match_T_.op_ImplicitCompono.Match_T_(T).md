#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[Match&lt;T&gt;](Compono.Match_T_.md 'Compono\.Match\<T\>')

## Match\<T\>\.implicit operator Match\<T\>\(T\) Operator

A literal argument matches by equality \([System\.Collections\.Generic\.EqualityComparer&lt;&gt;\.Default](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.equalitycomparer-1.default 'System\.Collections\.Generic\.EqualityComparer\`1\.Default')\) \- the same
implicit meaning NSubstitute itself gives a literal argument, and the common case in real
migrated call sites, so it allocates no closure \(unlike [Is&lt;T&gt;\(Func&lt;T,bool&gt;\)](Compono.Match.Is_T_(System.Func_T,bool_).md 'Compono\.Match\.Is\<T\>\(System\.Func\<T,bool\>\)')\)\.

```csharp
public static Compono.Match<T> implicit operator Compono.Match<T>(T value);
```
#### Parameters

<a name='Compono.Match_T_.op_ImplicitCompono.Match_T_(T).value'></a>

`value` [T](Compono.Match_T_.md#Compono.Match_T_.T 'Compono\.Match\<T\>\.T')

#### Returns
[Compono\.Match&lt;](Compono.Match_T_.md 'Compono\.Match\<T\>')[T](Compono.Match_T_.md#Compono.Match_T_.T 'Compono\.Match\<T\>\.T')[&gt;](Compono.Match_T_.md 'Compono\.Match\<T\>')