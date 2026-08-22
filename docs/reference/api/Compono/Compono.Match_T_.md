#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## Match\<T\> Struct

An argument matcher for a generator\-emitted test double's argument\-aware `Configure()`/
`Verify()` surface \- a literal value \(equality match\), [Any&lt;T&gt;\(\)](Compono.Match.Any_T_().md 'Compono\.Match\.Any\<T\>\(\)') \(matches
anything\), or [Is&lt;T&gt;\(Func&lt;T,bool&gt;\)](Compono.Match.Is_T_(System.Func_T,bool_).md 'Compono\.Match\.Is\<T\>\(System\.Func\<T,bool\>\)') \(matches by predicate\)\. See ADR\-0048\.

```csharp
public readonly struct Match<T>
```
#### Type parameters

<a name='Compono.Match_T_.T'></a>

`T`

### Remarks
Exposes exactly one public, generated\-code\-facing operation \- [Matches\(T\)](Compono.Match_T_.Matches(T).md 'Compono\.Match\<T\>\.Matches\(T\)')\. No public
delegate/predicate accessor: generated dispatch/verification code lives in the \*consumer\*
assembly, not `Compono`, so an [internal](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/internal 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/internal') accessor is unreachable from it \-
the same cross\-assembly\-accessibility class of defect ADR\-0044 Amendment 3 already fixed for
[ReturnConfig&lt;T&gt;](Compono.ReturnConfig_T_.md 'Compono\.ReturnConfig\<T\>')\. Keeping the internal representation private also means it stays
free to change without becoming a breaking change to generated output's dependency on this type\.
Named `Match`, not `Arg`, specifically because `Compono.Arg` collides with
`NSubstitute.Arg` for any consumer whose own namespace nests under `Compono` \(this
repo's own samples convention\) or who combines `Compono` with `Compono.NSubstitute`
directly \- confirmed with a real failing build during PLAN\-0048's implementation, not a
theoretical concern\. See ADR\-0048's Decision Outcome\.

| Methods | |
| :--- | :--- |
| [Matches\(T\)](Compono.Match_T_.Matches(T).md 'Compono\.Match\<T\>\.Matches\(T\)') | Whether [value](Compono.Match_T_.Matches(T).md#Compono.Match_T_.Matches(T).value 'Compono\.Match\<T\>\.Matches\(T\)\.value') satisfies this matcher \- the one operation generated dispatch/verification code calls\. |

| Operators | |
| :--- | :--- |
| [implicit operator Match&lt;T&gt;\(T\)](Compono.Match_T_.op_ImplicitCompono.Match_T_(T).md 'Compono\.Match\<T\>\.op\_Implicit Compono\.Match\<T\>\(T\)') | A literal argument matches by equality \([System\.Collections\.Generic\.EqualityComparer&lt;&gt;\.Default](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.equalitycomparer-1.default 'System\.Collections\.Generic\.EqualityComparer\`1\.Default')\) \- the same implicit meaning NSubstitute itself gives a literal argument, and the common case in real migrated call sites, so it allocates no closure \(unlike [Is&lt;T&gt;\(Func&lt;T,bool&gt;\)](Compono.Match.Is_T_(System.Func_T,bool_).md 'Compono\.Match\.Is\<T\>\(System\.Func\<T,bool\>\)')\)\. |
