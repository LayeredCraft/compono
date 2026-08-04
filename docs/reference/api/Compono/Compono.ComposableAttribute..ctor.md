#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ComposableAttribute](Compono.ComposableAttribute.md 'Compono\.ComposableAttribute')

## ComposableAttribute Constructors

| Overloads | |
| :--- | :--- |
| [ComposableAttribute\(\)](Compono.ComposableAttribute..ctor.md#Compono.ComposableAttribute.ComposableAttribute() 'Compono\.ComposableAttribute\.ComposableAttribute\(\)') | Marks the annotated type as composable\. Only valid on a type declaration — the assembly\-level form requires [ComposableAttribute\(Type\)](Compono.ComposableAttribute..ctor.md#Compono.ComposableAttribute.ComposableAttribute(System.Type) 'Compono\.ComposableAttribute\.ComposableAttribute\(System\.Type\)') to identify the target type\. |
| [ComposableAttribute\(Type\)](Compono.ComposableAttribute..ctor.md#Compono.ComposableAttribute.ComposableAttribute(System.Type) 'Compono\.ComposableAttribute\.ComposableAttribute\(System\.Type\)') | Requests a generated composition plan for [type](Compono.ComposableAttribute.md#Compono.ComposableAttribute.ComposableAttribute(System.Type).type 'Compono\.ComposableAttribute\.ComposableAttribute\(System\.Type\)\.type') — the form to use at assembly level, where there's no annotated type to infer the target from\. |

<a name='ctor.md#Compono.ComposableAttribute.ComposableAttribute()'></a>

## ComposableAttribute\(\) Constructor

Marks the annotated type as composable\. Only valid on a type declaration — the assembly\-level
form requires [ComposableAttribute\(Type\)](Compono.ComposableAttribute..ctor.md#Compono.ComposableAttribute.ComposableAttribute(System.Type) 'Compono\.ComposableAttribute\.ComposableAttribute\(System\.Type\)') to identify the target type\.

```csharp
public ComposableAttribute();
```

<a name='ctor.md#Compono.ComposableAttribute.ComposableAttribute(System.Type)'></a>

## ComposableAttribute\(Type\) Constructor

Requests a generated composition plan for [type](Compono.ComposableAttribute.md#Compono.ComposableAttribute.ComposableAttribute(System.Type).type 'Compono\.ComposableAttribute\.ComposableAttribute\(System\.Type\)\.type') — the form to use at
assembly level, where there's no annotated type to infer the target from\.

```csharp
public ComposableAttribute(System.Type type);
```
#### Parameters

<a name='Compono.ComposableAttribute.ComposableAttribute(System.Type).type'></a>

`type` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

The type to generate a composition plan for\.