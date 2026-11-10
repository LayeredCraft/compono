#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.ResolveGenericTestClassType\(Type\) Method

Resolves the closed generic test class whose generic type definition matches
[genericTypeDefinition](Compono.XunitV3.Aot.ComposeDataRows.ResolveGenericTestClassType(System.Type).md#Compono.XunitV3.Aot.ComposeDataRows.ResolveGenericTestClassType(System.Type).genericTypeDefinition 'Compono\.XunitV3\.Aot\.ComposeDataRows\.ResolveGenericTestClassType\(System\.Type\)\.genericTypeDefinition') by walking the executing test class's base types\.
Reads only type identity metadata; no member reflection is performed\.

```csharp
public static System.Type ResolveGenericTestClassType(System.Type genericTypeDefinition);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.ResolveGenericTestClassType(System.Type).genericTypeDefinition'></a>

`genericTypeDefinition` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

The open generic test\-class type\.

#### Returns
[System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')  
The closed generic test class\.