#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ReturnConfigBuilder&lt;T&gt;](Compono.ReturnConfigBuilder_T_.md 'Compono\.ReturnConfigBuilder\<T\>')

## ReturnConfigBuilder\<T\>\.ReturnsSequence\(SequenceOutcome\<T\>\[\]\) Method

Configures the member to return \(or throw\) each [outcomes](Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).md#Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).outcomes 'Compono\.ReturnConfigBuilder\<T\>\.ReturnsSequence\(Compono\.SequenceOutcome\<T\>\[\]\)\.outcomes') entry in order, one
per invocation, by ordinal \- the first call gets `outcomes[0]`, the second
`outcomes[1]`, and so on; once exhausted, every further call repeats the final entry
\(ADR\-0054\)\. Clears any prior [Returns\(T\)](Compono.ReturnConfigBuilder_T_.Returns(T).md 'Compono\.ReturnConfigBuilder\<T\>\.Returns\(T\)')/[Throws\(Exception\)](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)')/[ReturnsSequence\(SequenceOutcome&lt;T&gt;\[\]\)](Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).md 'Compono\.ReturnConfigBuilder\<T\>\.ReturnsSequence\(Compono\.SequenceOutcome\<T\>\[\]\)')
state and resets the ordinal to 0, the same last\-configuration\-wins contract [Returns\(T\)](Compono.ReturnConfigBuilder_T_.Returns(T).md 'Compono\.ReturnConfigBuilder\<T\>\.Returns\(T\)')/
[Throws\(Exception\)](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)') already document\. An ordinary [T](Compono.ReturnConfigBuilder_T_.md#Compono.ReturnConfigBuilder_T_.T 'Compono\.ReturnConfigBuilder\<T\>\.T') value implicitly
converts to [SequenceOutcome&lt;T&gt;](Compono.SequenceOutcome_T_.md 'Compono\.SequenceOutcome\<T\>'), so a pure\-value sequence reads as plain values
\(`.ReturnsSequence(false, false, true)`\); an exception outcome is spelled explicitly with
[Throw\(Exception\)](Compono.SequenceOutcome.Throw(System.Exception).md 'Compono\.SequenceOutcome\.Throw\(System\.Exception\)') \- there is no implicit conversion from
[System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception'), since that would be silently wrong for a [T](Compono.ReturnConfigBuilder_T_.md#Compono.ReturnConfigBuilder_T_.T 'Compono\.ReturnConfigBuilder\<T\>\.T') that
is itself [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') or a base/derived type of it \- so a mixed sequence reads
`.ReturnsSequence(SequenceOutcome.Throw(ex1), SequenceOutcome.Throw(ex2), value)`\.

```csharp
public void ReturnsSequence(params Compono.SequenceOutcome<T>[] outcomes);
```
#### Parameters

<a name='Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).outcomes'></a>

`outcomes` [Compono\.SequenceOutcome&lt;](Compono.SequenceOutcome_T_.md 'Compono\.SequenceOutcome\<T\>')[T](Compono.ReturnConfigBuilder_T_.md#Compono.ReturnConfigBuilder_T_.T 'Compono\.ReturnConfigBuilder\<T\>\.T')[&gt;](Compono.SequenceOutcome_T_.md 'Compono\.SequenceOutcome\<T\>')[\[\]](https://learn.microsoft.com/en-us/dotnet/api/system.array 'System\.Array')

#### Exceptions

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[outcomes](Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).md#Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).outcomes 'Compono\.ReturnConfigBuilder\<T\>\.ReturnsSequence\(Compono\.SequenceOutcome\<T\>\[\]\)\.outcomes') is empty\.