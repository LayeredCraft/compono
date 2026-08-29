#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ReturnConfig&lt;T&gt;](Compono.ReturnConfig_T_.md 'Compono\.ReturnConfig\<T\>')

## ReturnConfig\<T\>\.NextSequenceOutcome\(\) Method

Consumes and returns \(or throws\) the next outcome in the configured sequence, by invocation
ordinal \- the first call gets index 0, the second index 1, and so on\. Only meaningful when
[HasConfiguredSequence](Compono.ReturnConfig_T_.HasConfiguredSequence.md 'Compono\.ReturnConfig\<T\>\.HasConfiguredSequence') is [true](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool')\. Once the sequence is exhausted,
every further call repeats the final configured outcome \(ADR\-0054's chosen exhaustion
semantics, matching NSubstitute's own established `Returns(a, b, c)` behavior\)\.

```csharp
public T NextSequenceOutcome();
```

#### Returns
[T](Compono.ReturnConfig_T_.md#Compono.ReturnConfig_T_.T 'Compono\.ReturnConfig\<T\>\.T')

### Remarks
Thread\-safe with no lock: `Compono.ReturnConfig&lt;&gt;.Sequence` is never mutated after
[ReturnsSequence\(SequenceOutcome&lt;T&gt;\[\]\)](Compono.ReturnConfigBuilder_T_.ReturnsSequence(Compono.SequenceOutcome_T_[]).md 'Compono\.ReturnConfigBuilder\<T\>\.ReturnsSequence\(Compono\.SequenceOutcome\<T\>\[\]\)') sets it \(a reconfiguration replaces the
whole array reference, never edits an element in place\), so the only shared mutable state is
the ordinal itself \- claimed via [System\.Threading\.Interlocked\.Increment\(System\.Int32@\)](https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked.increment#system-threading-interlocked-increment(system-int32@) 'System\.Threading\.Interlocked\.Increment\(System\.Int32@\)'),
the same primitive [RecordCall\(\)](Compono.ReturnConfig_T_.RecordCall().md 'Compono\.ReturnConfig\<T\>\.RecordCall\(\)') already uses, so two concurrent callers always
claim two distinct, strictly\-increasing ordinals and never observe or corrupt each other's
index\.