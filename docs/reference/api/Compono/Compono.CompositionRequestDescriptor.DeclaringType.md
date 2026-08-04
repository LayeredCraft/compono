#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionRequestDescriptor](Compono.CompositionRequestDescriptor.md 'Compono\.CompositionRequestDescriptor')

## CompositionRequestDescriptor\.DeclaringType Property

The type whose constructor/required member declares this parameter/member, or the test class
for a [TestParameter](Compono.CompositionRequestKind.md#Compono.CompositionRequestKind.TestParameter 'Compono\.CompositionRequestKind\.TestParameter') request \- [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') for a
request with no member identity of its own \(a collection element, dictionary key/value, or
manual resolve\)\. Never fed into random\-fork hashing \- used only for configuration\-rule matching
\(stage 4\) and collection\-size override lookup \(stage 7\)\. See
`docs/adr/0020-composition-configuration-rules.md`\.

```csharp
public System.Type? DeclaringType { get; }
```

#### Property Value
[System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')