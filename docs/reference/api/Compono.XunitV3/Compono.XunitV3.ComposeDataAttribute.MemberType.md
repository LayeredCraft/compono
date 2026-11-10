#### [Compono\.XunitV3](index.md 'index')
### [Compono\.XunitV3](Compono.XunitV3.md 'Compono\.XunitV3').[ComposeDataAttribute](Compono.XunitV3.ComposeDataAttribute.md 'Compono\.XunitV3\.ComposeDataAttribute')

## ComposeDataAttribute\.MemberType Property

The type that declares [MemberName](Compono.XunitV3.ComposeDataAttribute.MemberName.md 'Compono\.XunitV3\.ComposeDataAttribute\.MemberName')\. [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') \(the default\) means the
test class itself, matching xUnit's own `MemberData` convention\. The public fields,
properties, and methods are preserved for dynamic access, since the member is located by
reflection\.

```csharp
public System.Type? MemberType { get; set; }
```

#### Property Value
[System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')