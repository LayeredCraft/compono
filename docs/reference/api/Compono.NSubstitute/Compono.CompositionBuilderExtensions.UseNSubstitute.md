#### [Compono\.NSubstitute](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionBuilderExtensions](Compono.CompositionBuilderExtensions.md 'Compono\.CompositionBuilderExtensions')

## CompositionBuilderExtensions\.UseNSubstitute Method

| Overloads | |
| :--- | :--- |
| [UseNSubstitute\(this CompositionBuilder\)](Compono.CompositionBuilderExtensions.UseNSubstitute.md#Compono.CompositionBuilderExtensions.UseNSubstitute(thisCompono.CompositionBuilder) 'Compono\.CompositionBuilderExtensions\.UseNSubstitute\(this Compono\.CompositionBuilder\)') | Registers an [NSubstituteProvider](Compono.NSubstituteProvider.md 'Compono\.NSubstituteProvider') with default [NSubstituteOptions](Compono.NSubstituteOptions.md 'Compono\.NSubstituteOptions')\. |
| [UseNSubstitute\(this CompositionBuilder, Action&lt;NSubstituteOptions&gt;\)](Compono.CompositionBuilderExtensions.UseNSubstitute.md#Compono.CompositionBuilderExtensions.UseNSubstitute(thisCompono.CompositionBuilder,System.Action_Compono.NSubstituteOptions_) 'Compono\.CompositionBuilderExtensions\.UseNSubstitute\(this Compono\.CompositionBuilder, System\.Action\<Compono\.NSubstituteOptions\>\)') | Registers an [NSubstituteProvider](Compono.NSubstituteProvider.md 'Compono\.NSubstituteProvider'), configured by [configure](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseNSubstitute(thisCompono.CompositionBuilder,System.Action_Compono.NSubstituteOptions_).configure 'Compono\.CompositionBuilderExtensions\.UseNSubstitute\(this Compono\.CompositionBuilder, System\.Action\<Compono\.NSubstituteOptions\>\)\.configure')\. |

<a name='Compono.CompositionBuilderExtensions.UseNSubstitute(thisCompono.CompositionBuilder)'></a>

## CompositionBuilderExtensions\.UseNSubstitute\(this CompositionBuilder\) Method

Registers an [NSubstituteProvider](Compono.NSubstituteProvider.md 'Compono\.NSubstituteProvider') with default [NSubstituteOptions](Compono.NSubstituteOptions.md 'Compono\.NSubstituteOptions')\.

```csharp
public static Compono.CompositionBuilder UseNSubstitute(this Compono.CompositionBuilder builder);
```
#### Parameters

<a name='Compono.CompositionBuilderExtensions.UseNSubstitute(thisCompono.CompositionBuilder).builder'></a>

`builder` [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

#### Returns
[CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

<a name='Compono.CompositionBuilderExtensions.UseNSubstitute(thisCompono.CompositionBuilder,System.Action_Compono.NSubstituteOptions_)'></a>

## CompositionBuilderExtensions\.UseNSubstitute\(this CompositionBuilder, Action\<NSubstituteOptions\>\) Method

Registers an [NSubstituteProvider](Compono.NSubstituteProvider.md 'Compono\.NSubstituteProvider'), configured by [configure](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseNSubstitute(thisCompono.CompositionBuilder,System.Action_Compono.NSubstituteOptions_).configure 'Compono\.CompositionBuilderExtensions\.UseNSubstitute\(this Compono\.CompositionBuilder, System\.Action\<Compono\.NSubstituteOptions\>\)\.configure')\.

```csharp
public static Compono.CompositionBuilder UseNSubstitute(this Compono.CompositionBuilder builder, System.Action<Compono.NSubstituteOptions> configure);
```
#### Parameters

<a name='Compono.CompositionBuilderExtensions.UseNSubstitute(thisCompono.CompositionBuilder,System.Action_Compono.NSubstituteOptions_).builder'></a>

`builder` [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

<a name='Compono.CompositionBuilderExtensions.UseNSubstitute(thisCompono.CompositionBuilder,System.Action_Compono.NSubstituteOptions_).configure'></a>

`configure` [System\.Action&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')[NSubstituteOptions](Compono.NSubstituteOptions.md 'Compono\.NSubstituteOptions')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')

Sets the provider's [NSubstituteOptions](Compono.NSubstituteOptions.md 'Compono\.NSubstituteOptions')\.

#### Returns
[CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')