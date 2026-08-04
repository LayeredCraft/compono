#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## ComposableAttribute Class

Opts a type into generated composition when discovery can't find it on its own — a
plan\-generation request equivalent to a `Composer.Create<T>()` call site, per
`docs/adr/0004-composition-plan-discovery-and-dispatch.md`'s hybrid discovery decision\.

```csharp
public sealed class ComposableAttribute : System.Attribute
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Attribute](https://learn.microsoft.com/en-us/dotnet/api/system.attribute 'System\.Attribute') → ComposableAttribute

### Remarks
Most types never need this: any type reachable from a `Create<T>()` call site in the
compilation \(directly or through another type's constructor parameters\) gets a generated plan
automatically\. Apply `[Composable]` directly to a type this compilation owns; use the
assembly\-level form \(`[assembly: Composable(typeof(SomeType))]`\) for a type in a referenced
assembly that can't be annotated\. Both forms are equivalent requests and repeated requests for
the same type are deduplicated — prefer the type\-level form whenever the type is owned locally\.

| Constructors | |
| :--- | :--- |
| [ComposableAttribute\(\)](Compono.ComposableAttribute..ctor.md#Compono.ComposableAttribute.ComposableAttribute() 'Compono\.ComposableAttribute\.ComposableAttribute\(\)') | Marks the annotated type as composable\. Only valid on a type declaration — the assembly\-level form requires [ComposableAttribute\(Type\)](Compono.ComposableAttribute..ctor.md#Compono.ComposableAttribute.ComposableAttribute(System.Type) 'Compono\.ComposableAttribute\.ComposableAttribute\(System\.Type\)') to identify the target type\. |
| [ComposableAttribute\(Type\)](Compono.ComposableAttribute..ctor.md#Compono.ComposableAttribute.ComposableAttribute(System.Type) 'Compono\.ComposableAttribute\.ComposableAttribute\(System\.Type\)') | Requests a generated composition plan for [type](Compono.ComposableAttribute.md#Compono.ComposableAttribute.ComposableAttribute(System.Type).type 'Compono\.ComposableAttribute\.ComposableAttribute\(System\.Type\)\.type') — the form to use at assembly level, where there's no annotated type to infer the target from\. |

| Properties | |
| :--- | :--- |
| [Type](Compono.ComposableAttribute.Type.md 'Compono\.ComposableAttribute\.Type') | The explicitly requested type, or [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null') when the request targets the annotated type itself\. |
