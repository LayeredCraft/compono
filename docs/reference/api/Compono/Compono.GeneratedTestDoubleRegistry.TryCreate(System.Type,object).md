#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[GeneratedTestDoubleRegistry](Compono.GeneratedTestDoubleRegistry.md 'Compono\.GeneratedTestDoubleRegistry')

## GeneratedTestDoubleRegistry\.TryCreate\(Type, object\) Method

Looks up and invokes the factory registered for [requestedType](Compono.GeneratedTestDoubleRegistry.TryCreate(System.Type,object).md#Compono.GeneratedTestDoubleRegistry.TryCreate(System.Type,object).requestedType 'Compono\.GeneratedTestDoubleRegistry\.TryCreate\(System\.Type, object\)\.requestedType'), or
[false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') if none has been registered \- either because
[requestedType](Compono.GeneratedTestDoubleRegistry.TryCreate(System.Type,object).md#Compono.GeneratedTestDoubleRegistry.TryCreate(System.Type,object).requestedType 'Compono\.GeneratedTestDoubleRegistry\.TryCreate\(System\.Type, object\)\.requestedType') was never discovered as a generated\-test\-double leaf, or
because the consuming assembly's module initializers haven't run yet\.

```csharp
public static bool TryCreate(System.Type requestedType, out object? value);
```
#### Parameters

<a name='Compono.GeneratedTestDoubleRegistry.TryCreate(System.Type,object).requestedType'></a>

`requestedType` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

<a name='Compono.GeneratedTestDoubleRegistry.TryCreate(System.Type,object).value'></a>

`value` [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object')

#### Returns
[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')