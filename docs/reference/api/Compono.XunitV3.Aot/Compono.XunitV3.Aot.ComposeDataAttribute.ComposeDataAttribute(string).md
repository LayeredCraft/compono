#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataAttribute](Compono.XunitV3.Aot.ComposeDataAttribute.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute')

## ComposeDataAttribute\(string\) Constructor

Creates a [ComposeDataAttribute](Compono.XunitV3.Aot.ComposeDataAttribute.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute')\.

```csharp
public ComposeDataAttribute(string memberName);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataAttribute.ComposeDataAttribute(string).memberName'></a>

`memberName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The name of the public static field, property, or parameterless method that supplies this
theory's rows\. Resolved against [MemberType](Compono.XunitV3.Aot.ComposeDataAttribute.MemberType.md 'Compono\.XunitV3\.Aot\.ComposeDataAttribute\.MemberType') when it is set, otherwise against the
test class itself\.