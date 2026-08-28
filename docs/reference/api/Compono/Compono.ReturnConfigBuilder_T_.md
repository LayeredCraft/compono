#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## ReturnConfigBuilder\<T\> Struct

Public write surface over a single [ReturnConfig&lt;T&gt;](Compono.ReturnConfig_T_.md 'Compono\.ReturnConfig\<T\>') slot \- constructed by
generator\-emitted configuration extensions \(`Configure().Member()`\) in the consumer's own
assembly, per ADR\-0043\. A [ref struct](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ref struct 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/ref struct') because it only ever wraps a
[ref](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ref 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/ref') to a field already living on the generated double instance; it's never
stored, only used inline at the call site\.

```csharp
public readonly ref struct ReturnConfigBuilder<T>
```
#### Type parameters

<a name='Compono.ReturnConfigBuilder_T_.T'></a>

`T`

### Remarks
[Returns\(T\)](Compono.ReturnConfigBuilder_T_.Returns(T).md 'Compono\.ReturnConfigBuilder\<T\>\.Returns\(T\)')/[Throws\(Exception\)](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)')/[ReturnsSequence\(SequenceOutcome&lt;T&gt;\[\]\)](Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).md 'Compono\.ReturnConfigBuilder\<T\>\.ReturnsSequence\(Compono\.SequenceOutcome\<T\>\[\]\)') are all
            last\-configuration\-wins: each of the three clears the other two's state, so configuring any one
            of them after an earlier call to a different one of them doesn't leave stale state behind\. See
            ADR\-0043 Amendment 7, Finding R \(the original two\-way rule\) and ADR\-0054 \(the sequence extension\)\.

| Constructors | |
| :--- | :--- |
| [ReturnConfigBuilder\(ReturnConfig&lt;T&gt;\)](Compono.ReturnConfigBuilder_T_.ReturnConfigBuilder(Compono.ReturnConfig_T_).md 'Compono\.ReturnConfigBuilder\<T\>\.ReturnConfigBuilder\(Compono\.ReturnConfig\<T\>\)') | Wraps [slot](Compono.ReturnConfigBuilder_T_.ReturnConfigBuilder(Compono.ReturnConfig_T_).md#Compono.ReturnConfigBuilder_T_.ReturnConfigBuilder(Compono.ReturnConfig_T_).slot 'Compono\.ReturnConfigBuilder\<T\>\.ReturnConfigBuilder\(Compono\.ReturnConfig\<T\>\)\.slot'), the generated double's own backing field for this member\. |

| Methods | |
| :--- | :--- |
| [Returns\(T\)](Compono.ReturnConfigBuilder_T_.Returns(T).md 'Compono\.ReturnConfigBuilder\<T\>\.Returns\(T\)') | Configures the member to return [value](Compono.ReturnConfigBuilder_T_.Returns(T).md#Compono.ReturnConfigBuilder_T_.Returns(T).value 'Compono\.ReturnConfigBuilder\<T\>\.Returns\(T\)\.value'), clearing any prior [Throws\(Exception\)](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)')/[ReturnsSequence\(SequenceOutcome&lt;T&gt;\[\]\)](Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).md 'Compono\.ReturnConfigBuilder\<T\>\.ReturnsSequence\(Compono\.SequenceOutcome\<T\>\[\]\)')\. |
| [ReturnsSequence\(SequenceOutcome&lt;T&gt;\[\]\)](Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).md 'Compono\.ReturnConfigBuilder\<T\>\.ReturnsSequence\(Compono\.SequenceOutcome\<T\>\[\]\)') | Configures the member to return \(or throw\) each [outcomes](Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).md#Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).outcomes 'Compono\.ReturnConfigBuilder\<T\>\.ReturnsSequence\(Compono\.SequenceOutcome\<T\>\[\]\)\.outcomes') entry in order, one per invocation, by ordinal \- the first call gets `outcomes[0]`, the second `outcomes[1]`, and so on; once exhausted, every further call repeats the final entry \(ADR\-0054\)\. Clears any prior [Returns\(T\)](Compono.ReturnConfigBuilder_T_.Returns(T).md 'Compono\.ReturnConfigBuilder\<T\>\.Returns\(T\)')/[Throws\(Exception\)](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)')/[ReturnsSequence\(SequenceOutcome&lt;T&gt;\[\]\)](Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).md 'Compono\.ReturnConfigBuilder\<T\>\.ReturnsSequence\(Compono\.SequenceOutcome\<T\>\[\]\)') state and resets the ordinal to 0, the same last\-configuration\-wins contract [Returns\(T\)](Compono.ReturnConfigBuilder_T_.Returns(T).md 'Compono\.ReturnConfigBuilder\<T\>\.Returns\(T\)')/ [Throws\(Exception\)](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)') already document\. An ordinary [T](Compono.ReturnConfigBuilder_T_.md#Compono.ReturnConfigBuilder_T_.T 'Compono\.ReturnConfigBuilder\<T\>\.T') value implicitly converts to [SequenceOutcome&lt;T&gt;](Compono.SequenceOutcome_T_.md 'Compono\.SequenceOutcome\<T\>'), so a pure\-value sequence reads as plain values \(`.ReturnsSequence(false, false, true)`\); an exception outcome is spelled explicitly with [Throw\(Exception\)](Compono.SequenceOutcome.Throw(System.Exception).md 'Compono\.SequenceOutcome\.Throw\(System\.Exception\)') \- there is no implicit conversion from [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception'), since that would be silently wrong for a [T](Compono.ReturnConfigBuilder_T_.md#Compono.ReturnConfigBuilder_T_.T 'Compono\.ReturnConfigBuilder\<T\>\.T') that is itself [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') or a base/derived type of it \- so a mixed sequence reads `.ReturnsSequence(SequenceOutcome.Throw(ex1), SequenceOutcome.Throw(ex2), value)`\. |
| [Throws\(Exception\)](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)') | Configures the member to throw [exception](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md#Compono.ReturnConfigBuilder_T_.Throws(System.Exception).exception 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)\.exception'), clearing any prior [Returns\(T\)](Compono.ReturnConfigBuilder_T_.Returns(T).md 'Compono\.ReturnConfigBuilder\<T\>\.Returns\(T\)')/[ReturnsSequence\(SequenceOutcome&lt;T&gt;\[\]\)](Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).md 'Compono\.ReturnConfigBuilder\<T\>\.ReturnsSequence\(Compono\.SequenceOutcome\<T\>\[\]\)')\. |
