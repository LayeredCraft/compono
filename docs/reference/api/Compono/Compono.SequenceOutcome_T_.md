#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## SequenceOutcome\<T\> Struct

One outcome in a [ReturnsSequence\(SequenceOutcome&lt;T&gt;\[\]\)](Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).md 'Compono\.ReturnConfigBuilder\<T\>\.ReturnsSequence\(Compono\.SequenceOutcome\<T\>\[\]\)') sequence \- either a
configured return value \(implicit conversion from [T](Compono.SequenceOutcome_T_.md#Compono.SequenceOutcome_T_.T 'Compono\.SequenceOutcome\<T\>\.T')\) or a configured
exception \([Throw\(Exception\)](Compono.SequenceOutcome.Throw(System.Exception).md 'Compono\.SequenceOutcome\.Throw\(System\.Exception\)')\), target\-typed so a consumer never spells
`SequenceOutcome<T>` directly \(ADR\-0054\)\. Mirrors [Match&lt;T&gt;](Compono.Match_T_.md 'Compono\.Match\<T\>')'s own "implicit
conversion from a literal, no public constructor" shape\.

```csharp
public readonly struct SequenceOutcome<T>
```
#### Type parameters

<a name='Compono.SequenceOutcome_T_.T'></a>

`T`

### Remarks
Only a single implicit conversion exists \(from [T](Compono.SequenceOutcome_T_.md#Compono.SequenceOutcome_T_.T 'Compono\.SequenceOutcome\<T\>\.T')\) \- a second implicit
conversion from [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') was rejected because it is silently ambiguous/wrong for
[T](Compono.SequenceOutcome_T_.md#Compono.SequenceOutcome_T_.T 'Compono\.SequenceOutcome\<T\>\.T') values that are themselves [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') or a base/derived type
of it \(e\.g\. `T = object` resolves to "throw" with no way left to express "value"; `T =
            InvalidOperationException` silently resolves to "value" instead of "throw" \- both confirmed by
real compiler/runtime evidence, not assumed\)\. [Throw\(Exception\)](Compono.SequenceOutcome.Throw(System.Exception).md 'Compono\.SequenceOutcome\.Throw\(System\.Exception\)') plus the second
implicit conversion from [ThrownOutcome](Compono.SequenceOutcome.ThrownOutcome.md 'Compono\.SequenceOutcome\.ThrownOutcome') is unambiguous for every
[T](Compono.SequenceOutcome_T_.md#Compono.SequenceOutcome_T_.T 'Compono\.SequenceOutcome\<T\>\.T')\.

| Operators | |
| :--- | :--- |
| [implicit operator SequenceOutcome&lt;T&gt;\(ThrownOutcome\)](Compono.SequenceOutcome_T_.op_ImplicitCompono.SequenceOutcome_T_(Compono.SequenceOutcome.ThrownOutcome).md 'Compono\.SequenceOutcome\<T\>\.op\_Implicit Compono\.SequenceOutcome\<T\>\(Compono\.SequenceOutcome\.ThrownOutcome\)') | A sequence entry that throws the exception carried by [thrown](Compono.SequenceOutcome_T_.op_ImplicitCompono.SequenceOutcome_T_(Compono.SequenceOutcome.ThrownOutcome).md#Compono.SequenceOutcome_T_.op_ImplicitCompono.SequenceOutcome_T_(Compono.SequenceOutcome.ThrownOutcome).thrown 'Compono\.SequenceOutcome\<T\>\.op\_Implicit Compono\.SequenceOutcome\<T\>\(Compono\.SequenceOutcome\.ThrownOutcome\)\.thrown') when consumed\. |
| [implicit operator SequenceOutcome&lt;T&gt;\(T\)](Compono.SequenceOutcome_T_.op_ImplicitCompono.SequenceOutcome_T_(T).md 'Compono\.SequenceOutcome\<T\>\.op\_Implicit Compono\.SequenceOutcome\<T\>\(T\)') | A sequence entry that returns [value](Compono.SequenceOutcome_T_.op_ImplicitCompono.SequenceOutcome_T_(T).md#Compono.SequenceOutcome_T_.op_ImplicitCompono.SequenceOutcome_T_(T).value 'Compono\.SequenceOutcome\<T\>\.op\_Implicit Compono\.SequenceOutcome\<T\>\(T\)\.value') when consumed\. |
