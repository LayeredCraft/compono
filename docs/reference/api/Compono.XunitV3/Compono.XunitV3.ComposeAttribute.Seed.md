#### [Compono\.XunitV3](index.md 'index')
### [Compono\.XunitV3](Compono.XunitV3.md 'Compono\.XunitV3').[ComposeAttribute](Compono.XunitV3.ComposeAttribute.md 'Compono\.XunitV3\.ComposeAttribute')

## ComposeAttribute\.Seed Property

An explicit root seed for this row \- the same underlying contract as
[WithSeed\(int\)](../Compono/Compono.CompositionBuilder.WithSeed(int).md 'Compono\.CompositionBuilder\.WithSeed\(System\.Int32\)'), but restricted to non\-negative values \(enforced by
Phase 2's binding algorithm, not here\) so a seed reported in a failure message is always
pasteable back into this property unchanged\. Unset: a fresh, non\-negative seed is generated on
every [GetData\(MethodInfo, DisposalTracker\)](Compono.XunitV3.ComposeAttribute.GetData(System.Reflection.MethodInfo,Xunit.Sdk.DisposalTracker).md 'Compono\.XunitV3\.ComposeAttribute\.GetData\(System\.Reflection\.MethodInfo, Xunit\.Sdk\.DisposalTracker\)') call \- \<b\>unless\</b\> a profile applied via
[ComposeAttribute&lt;TProfile&gt;](Compono.XunitV3.ComposeAttribute_TProfile_.md 'Compono\.XunitV3\.ComposeAttribute\<TProfile\>')'s `TProfile.Configure` itself calls
[WithSeed\(int\)](../Compono/Compono.CompositionBuilder.WithSeed(int).md 'Compono\.CompositionBuilder\.WithSeed\(System\.Int32\)'), in which case every row reuses that profile\-configured
seed instead, even though this property itself was never set \(ADR\-0022 Amendment 3 \- a profile
pinning a seed is a deliberate reproducibility choice, honored the same way a value set here
would be\)\. A plain [int](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/int'), not [int?](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/int? 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/int?') \- an attribute named
argument cannot target a [System\.Nullable&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1') property \(CS0655\); see
`Compono.XunitV3.ComposeAttribute.SeedAsNullable` for the property the binding algorithm actually reads\.

```csharp
public int Seed { get; set; }
```

#### Property Value
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')