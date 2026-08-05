#### [Compono\.NSubstitute](index.md 'index')
### [Compono](Compono.md 'Compono')

## NSubstituteOptions Class

Configuration for [NSubstituteProvider](Compono.NSubstituteProvider.md 'Compono\.NSubstituteProvider'), set via
`CompositionBuilderExtensions.UseNSubstitute(Action{NSubstituteOptions})`\. See
`docs/adr/0025-compono-nsubstitute-package-design.md`\.

```csharp
public sealed class NSubstituteOptions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → NSubstituteOptions

| Properties | |
| :--- | :--- |
| [SubstituteAbstractClasses](Compono.NSubstituteOptions.SubstituteAbstractClasses.md 'Compono\.NSubstituteOptions\.SubstituteAbstractClasses') | Whether an unsealed abstract class is substitutable, in addition to every interface and delegate type\. Defaults to [true](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool')\. |
