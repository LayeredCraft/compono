#### [Compono\.TestDoubles](index.md 'index')

## Compono Namespace

| Classes | |
| :--- | :--- |
| [CompositionBuilderExtensions](Compono.CompositionBuilderExtensions.md 'Compono\.CompositionBuilderExtensions') | Activates [GeneratedTestDoubleProvider](Compono.GeneratedTestDoubleProvider.md 'Compono\.GeneratedTestDoubleProvider') on a [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')\. See ADR\-0043's "Runtime activation and precedence"\. |
| [GeneratedTestDoubleProvider](Compono.GeneratedTestDoubleProvider.md 'Compono\.GeneratedTestDoubleProvider') | A stage\-6 test\-double provider that satisfies a request by looking up a factory already registered into core [GeneratedTestDoubleRegistry](../Compono/Compono.GeneratedTestDoubleRegistry.md 'Compono\.GeneratedTestDoubleRegistry') \- populated by a `Compono.Generators`\-emitted `[ModuleInitializer]` per discovered interface, never by this type\. Registered via `CompositionBuilderExtensions.UseGeneratedTestDoubles()`\. See ADR\-0043's "Runtime activation and precedence"\. |
