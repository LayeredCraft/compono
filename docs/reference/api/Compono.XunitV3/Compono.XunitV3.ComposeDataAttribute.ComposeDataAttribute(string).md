#### [Compono\.XunitV3](index.md 'index')
### [Compono\.XunitV3](Compono.XunitV3.md 'Compono\.XunitV3').[ComposeDataAttribute](Compono.XunitV3.ComposeDataAttribute.md 'Compono\.XunitV3\.ComposeDataAttribute')

## ComposeDataAttribute\(string\) Constructor

Creates a [ComposeDataAttribute](Compono.XunitV3.ComposeDataAttribute.md 'Compono\.XunitV3\.ComposeDataAttribute')\.

```csharp
public ComposeDataAttribute(string memberName);
```
#### Parameters

<a name='Compono.XunitV3.ComposeDataAttribute.ComposeDataAttribute(string).memberName'></a>

`memberName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The name of the public static field, property, or parameterless method that supplies this
theory's rows\. Resolved against [MemberType](Compono.XunitV3.ComposeDataAttribute.MemberType.md 'Compono\.XunitV3\.ComposeDataAttribute\.MemberType') when it is set, otherwise against the
test class itself\.

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[memberName](Compono.XunitV3.ComposeDataAttribute.ComposeDataAttribute(string).md#Compono.XunitV3.ComposeDataAttribute.ComposeDataAttribute(string).memberName 'Compono\.XunitV3\.ComposeDataAttribute\.ComposeDataAttribute\(string\)\.memberName') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.