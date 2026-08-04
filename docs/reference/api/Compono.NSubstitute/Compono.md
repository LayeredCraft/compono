#### [Compono\.NSubstitute](index.md 'index')

## Compono Namespace

| Classes | |
| :--- | :--- |
| [CompositionBuilderExtensions](Compono.CompositionBuilderExtensions.md 'Compono\.CompositionBuilderExtensions') | Activates [NSubstituteProvider](Compono.NSubstituteProvider.md 'Compono\.NSubstituteProvider') on a [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')\. See `docs/adr/0025-compono-nsubstitute-package-design.md`\. |
| [NSubstituteOptions](Compono.NSubstituteOptions.md 'Compono\.NSubstituteOptions') | Configuration for [NSubstituteProvider](Compono.NSubstituteProvider.md 'Compono\.NSubstituteProvider'), set via `CompositionBuilderExtensions.UseNSubstitute(Action{NSubstituteOptions})`\. See `docs/adr/0025-compono-nsubstitute-package-design.md`\. |
| [NSubstituteProvider](Compono.NSubstituteProvider.md 'Compono\.NSubstituteProvider') | A stage\-6 test\-double provider that composes an interface, delegate, or \(when [SubstituteAbstractClasses](Compono.NSubstituteOptions.SubstituteAbstractClasses.md 'Compono\.NSubstituteOptions\.SubstituteAbstractClasses') allows it\) unsealed abstract\-class request as a real NSubstitute substitute, via `NSubstitute.Substitute.For(System.Type[],System.Object[])`\. Registered via `CompositionBuilderExtensions.UseNSubstitute()`\. See `docs/adr/0025-compono-nsubstitute-package-design.md` \(Amendment 1 for the corrected shape below\)\. |
