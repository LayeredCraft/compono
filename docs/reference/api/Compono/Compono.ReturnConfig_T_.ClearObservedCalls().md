#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ReturnConfig&lt;T&gt;](Compono.ReturnConfig_T_.md 'Compono\.ReturnConfig\<T\>')

## ReturnConfig\<T\>\.ClearObservedCalls\(\) Method

Clears the recorded call count without changing the configured value, exception, or sequence \-
the mirror of [ClearConfiguredResponse\(\)](Compono.ReturnConfig_T_.ClearConfiguredResponse().md 'Compono\.ReturnConfig\<T\>\.ClearConfiguredResponse\(\)')\. Backs `ClearCalls()` \(PLAN\-0063/
ADR\-0060\): observation history is reset, configured behavior \(including in\-progress
`Compono.ReturnConfig&lt;&gt;.SequenceOrdinal` progress\) is untouched, so a subsequent call resumes the
sequence rather than rewinding it\.

```csharp
public void ClearObservedCalls();
```