#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[SequenceOutcome](Compono.SequenceOutcome.md 'Compono\.SequenceOutcome')

## SequenceOutcome\.ThrownOutcome Struct

Marker carrying the exception for a thrown sequence entry, implicitly convertible to
[SequenceOutcome&lt;T&gt;](Compono.SequenceOutcome_T_.md 'Compono\.SequenceOutcome\<T\>') for any `T`\. Only ever produced by [Throw\(Exception\)](Compono.SequenceOutcome.Throw(System.Exception).md 'Compono\.SequenceOutcome\.Throw\(System\.Exception\)') \-
its own conversion guards against the struct's `default` value, which would otherwise
carry a null exception\.

```csharp
public readonly struct SequenceOutcome.ThrownOutcome
```