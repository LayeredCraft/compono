#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionRequestDescriptor](Compono.CompositionRequestDescriptor.md 'Compono\.CompositionRequestDescriptor')

## CompositionRequestDescriptor\(CompositionRequestKind, int, string, Type, Nullability\) Constructor

Creates a [CompositionRequestDescriptor](Compono.CompositionRequestDescriptor.md 'Compono\.CompositionRequestDescriptor')\.

```csharp
public CompositionRequestDescriptor(Compono.CompositionRequestKind kind, int ordinal, string name, System.Type? declaringType, Compono.Nullability nullability);
```
#### Parameters

<a name='Compono.CompositionRequestDescriptor.CompositionRequestDescriptor(Compono.CompositionRequestKind,int,string,System.Type,Compono.Nullability).kind'></a>

`kind` [CompositionRequestKind](Compono.CompositionRequestKind.md 'Compono\.CompositionRequestKind')

Whether this is a constructor parameter, a required member, or \(for a
[CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow')\) a test method's own parameter\.

<a name='Compono.CompositionRequestDescriptor.CompositionRequestDescriptor(Compono.CompositionRequestKind,int,string,System.Type,Compono.Nullability).ordinal'></a>

`ordinal` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The stable identity this request forks random state and builds path identity from \- a
constructor parameter's position in the selected constructor, a required member's
generator\-assigned declaration\-order index, or a test method parameter's position\. Never
[Name](Compono.CompositionRequestDescriptor.Name.md 'Compono\.CompositionRequestDescriptor\.Name')\.

<a name='Compono.CompositionRequestDescriptor.CompositionRequestDescriptor(Compono.CompositionRequestKind,int,string,System.Type,Compono.Nullability).name'></a>

`name` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The parameter or member name, for diagnostic display only\.

<a name='Compono.CompositionRequestDescriptor.CompositionRequestDescriptor(Compono.CompositionRequestKind,int,string,System.Type,Compono.Nullability).declaringType'></a>

`declaringType` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

The type whose constructor/required member declares this parameter/member, or the test class
for a [TestParameter](Compono.CompositionRequestKind.md#Compono.CompositionRequestKind.TestParameter 'Compono\.CompositionRequestKind\.TestParameter') request \- [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a
request with no member identity of its own \(a collection element, dictionary key/value, or
manual resolve\)\. See `docs/adr/0020-composition-configuration-rules.md`\.

<a name='Compono.CompositionRequestDescriptor.CompositionRequestDescriptor(Compono.CompositionRequestKind,int,string,System.Type,Compono.Nullability).nullability'></a>

`nullability` [Nullability](Compono.Nullability.md 'Compono\.Nullability')

Whether the requesting parameter or member is nullable\-annotated\.