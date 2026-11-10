#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataAttribute&lt;TProfile&gt;](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile\>')

## ComposeDataAttribute\<TProfile\>\.MemberType Property

The type that declares [MemberName](Compono.XunitV3.Aot.ComposeDataAttribute_TProfile_.MemberName.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\<TProfile\>\.MemberName')\. [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') \(the default\) means the
test class itself, matching xUnit's own `MemberData` convention\.

```csharp
public System.Type? MemberType { get; set; }
```

#### Property Value
[System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')