#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## CompositionRequestDescriptor Struct

The compact, compile\-time\-constructible value a generated [ICompositionPlan&lt;T&gt;](Compono.ICompositionPlan_T_.md 'Compono\.ICompositionPlan\<T\>')
passes to [Resolve&lt;TValue&gt;\(CompositionRequestDescriptor\)](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_(Compono.CompositionRequestDescriptor) 'Compono\.ICompositionContext\.Resolve\<TValue\>\(Compono\.CompositionRequestDescriptor\)') for one constructor parameter or
required member \- or, for a test\-framework integration composing a [CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow'),
one of a test method's own parameters \([TestParameter](Compono.CompositionRequestKind.md#Compono.CompositionRequestKind.TestParameter 'Compono\.CompositionRequestKind\.TestParameter')\)\.

```csharp
public readonly struct CompositionRequestDescriptor
```

### Remarks
A plain [struct](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/struct 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/struct'), not a `record struct` \- equality, `Deconstruct`, and
record\-style formatting aren't part of this type's contract, per
`docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md`'s second amendment\.
[Ordinal](Compono.CompositionRequestDescriptor.Ordinal.md 'Compono\.CompositionRequestDescriptor\.Ordinal'), not [Name](Compono.CompositionRequestDescriptor.Name.md 'Compono\.CompositionRequestDescriptor\.Name'), is the identity [Compono\.CompositionContext](https://learn.microsoft.com/en-us/dotnet/api/compono.compositioncontext 'Compono\.CompositionContext')
uses for path/random\-fork keys \- [Name](Compono.CompositionRequestDescriptor.Name.md 'Compono\.CompositionRequestDescriptor\.Name') exists for diagnostic display only, per
`docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md`'s second
amendment\.

| Constructors | |
| :--- | :--- |
| [CompositionRequestDescriptor\(CompositionRequestKind, int, string, Type, Nullability\)](Compono.CompositionRequestDescriptor.CompositionRequestDescriptor(Compono.CompositionRequestKind,int,string,System.Type,Compono.Nullability).md 'Compono\.CompositionRequestDescriptor\.CompositionRequestDescriptor\(Compono\.CompositionRequestKind, int, string, System\.Type, Compono\.Nullability\)') | Creates a [CompositionRequestDescriptor](Compono.CompositionRequestDescriptor.md 'Compono\.CompositionRequestDescriptor')\. |

| Properties | |
| :--- | :--- |
| [DeclaringType](Compono.CompositionRequestDescriptor.DeclaringType.md 'Compono\.CompositionRequestDescriptor\.DeclaringType') | The type whose constructor/required member declares this parameter/member, or the test class for a [TestParameter](Compono.CompositionRequestKind.md#Compono.CompositionRequestKind.TestParameter 'Compono\.CompositionRequestKind\.TestParameter') request \- [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a request with no member identity of its own \(a collection element, dictionary key/value, or manual resolve\)\. Never fed into random\-fork hashing \- used only for configuration\-rule matching \(stage 4\) and collection\-size override lookup \(stage 7\)\. See `docs/adr/0020-composition-configuration-rules.md`\. |
| [Kind](Compono.CompositionRequestDescriptor.Kind.md 'Compono\.CompositionRequestDescriptor\.Kind') | Whether this is a constructor parameter, a required member, or \(for a [CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow')\) a test method's own parameter\. |
| [Name](Compono.CompositionRequestDescriptor.Name.md 'Compono\.CompositionRequestDescriptor\.Name') | The parameter or member name, for diagnostic display only\. |
| [Nullability](Compono.CompositionRequestDescriptor.Nullability.md 'Compono\.CompositionRequestDescriptor\.Nullability') | Whether the requesting parameter or member is nullable\-annotated\. |
| [Ordinal](Compono.CompositionRequestDescriptor.Ordinal.md 'Compono\.CompositionRequestDescriptor\.Ordinal') | The stable identity this request forks random state and builds path identity from \- never [Name](Compono.CompositionRequestDescriptor.Name.md 'Compono\.CompositionRequestDescriptor\.Name')\. |
