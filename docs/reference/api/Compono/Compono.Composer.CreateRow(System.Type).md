#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[Composer](Compono.Composer.md 'Compono\.Composer')

## Composer\.CreateRow\(Type\) Method

Creates a new [CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow') \- one composition scope for several sibling
top\-level parameter requests \(e\.g\. one xUnit theory row's own method parameters\), sharing one
seed, one shared\-value scope, and one pre\-rooted path\. See
`docs/adr/0021-row-composition-entry-point-for-test-framework-integrations.md`\.

```csharp
public Compono.CompositionRow CreateRow(System.Type declaringType);
```
#### Parameters

<a name='Compono.Composer.CreateRow(System.Type).declaringType'></a>

`declaringType` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

The type whose method declares the parameters this row composes \(e\.g\. a test class\) \- the
row's diagnostic root, reported as [RootType](Compono.CompositionDiagnostic.RootType.md 'Compono\.CompositionDiagnostic\.RootType') on failure\.

#### Returns
[CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow')

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
[declaringType](Compono.Composer.CreateRow(System.Type).md#Compono.Composer.CreateRow(System.Type).declaringType 'Compono\.Composer\.CreateRow\(System\.Type\)\.declaringType') is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.