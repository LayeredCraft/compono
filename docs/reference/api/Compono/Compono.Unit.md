#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## Unit Struct

Void\-marker type for a generated test double's `void`/[Task](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/Task 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/Task')\-returning
members, so [ReturnConfig&lt;T&gt;](Compono.ReturnConfig_T_.md 'Compono\.ReturnConfig\<T\>') has a closeable type argument even when the member
itself returns nothing\. [public](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/public 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/public') from the start \(not [internal](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/internal 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/internal')\)
\- the same cross\-assembly\-accessibility lesson ADR\-0043 Amendment 3 already applied to
[ReturnConfig&lt;T&gt;](Compono.ReturnConfig_T_.md 'Compono\.ReturnConfig\<T\>')/[ReturnConfigBuilder&lt;T&gt;](Compono.ReturnConfigBuilder_T_.md 'Compono\.ReturnConfigBuilder\<T\>') applies here too: a generated
double lives in the consumer's own assembly, not core `Compono`\. See ADR\-0043 Amendment 4\.

```csharp
public readonly struct Unit
```