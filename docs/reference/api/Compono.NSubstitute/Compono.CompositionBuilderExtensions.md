#### [Compono\.NSubstitute](index.md 'index')
### [Compono](Compono.md 'Compono')

## CompositionBuilderExtensions Class

Activates [NSubstituteProvider](Compono.NSubstituteProvider.md 'Compono\.NSubstituteProvider') on a [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')\. See
`docs/adr/0025-compono-nsubstitute-package-design.md`\.

```csharp
public static class CompositionBuilderExtensions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → CompositionBuilderExtensions

| Methods | |
| :--- | :--- |
| [UseNSubstitute\(this CompositionBuilder\)](Compono.CompositionBuilderExtensions.UseNSubstitute.md#Compono.CompositionBuilderExtensions.UseNSubstitute(thisCompono.CompositionBuilder) 'Compono\.CompositionBuilderExtensions\.UseNSubstitute\(this Compono\.CompositionBuilder\)') | Registers an [NSubstituteProvider](Compono.NSubstituteProvider.md 'Compono\.NSubstituteProvider') with default [NSubstituteOptions](Compono.NSubstituteOptions.md 'Compono\.NSubstituteOptions')\. |
| [UseNSubstitute\(this CompositionBuilder, Action&lt;NSubstituteOptions&gt;\)](Compono.CompositionBuilderExtensions.UseNSubstitute.md#Compono.CompositionBuilderExtensions.UseNSubstitute(thisCompono.CompositionBuilder,System.Action_Compono.NSubstituteOptions_) 'Compono\.CompositionBuilderExtensions\.UseNSubstitute\(this Compono\.CompositionBuilder, System\.Action\<Compono\.NSubstituteOptions\>\)') | Registers an [NSubstituteProvider](Compono.NSubstituteProvider.md 'Compono\.NSubstituteProvider'), configured by [configure](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseNSubstitute(thisCompono.CompositionBuilder,System.Action_Compono.NSubstituteOptions_).configure 'Compono\.CompositionBuilderExtensions\.UseNSubstitute\(this Compono\.CompositionBuilder, System\.Action\<Compono\.NSubstituteOptions\>\)\.configure')\. |
