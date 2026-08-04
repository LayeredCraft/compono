#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionTypeRuleBuilder&lt;T&gt;](Compono.CompositionTypeRuleBuilder_T_.md 'Compono\.CompositionTypeRuleBuilder\<T\>')

## CompositionTypeRuleBuilder\<T\>\.Use Method

| Overloads | |
| :--- | :--- |
| [Use\(Func&lt;ICompositionContext,T&gt;\)](Compono.CompositionTypeRuleBuilder_T_.Use.md#Compono.CompositionTypeRuleBuilder_T_.Use(System.Func_Compono.ICompositionContext,T_) 'Compono\.CompositionTypeRuleBuilder\<T\>\.Use\(System\.Func\<Compono\.ICompositionContext,T\>\)') | Registers a type rule whose value is produced by [factory](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.Use(System.Func_Compono.ICompositionContext,T_).factory 'Compono\.CompositionTypeRuleBuilder\<T\>\.Use\(System\.Func\<Compono\.ICompositionContext,T\>\)\.factory') \- matches any stage\-4 request for exactly [T](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.T 'Compono\.CompositionTypeRuleBuilder\<T\>\.T'), regardless of which member/position requested it\. |
| [Use\(T\)](Compono.CompositionTypeRuleBuilder_T_.Use.md#Compono.CompositionTypeRuleBuilder_T_.Use(T) 'Compono\.CompositionTypeRuleBuilder\<T\>\.Use\(T\)') | Registers a type rule that always produces [value](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.Use(T).value 'Compono\.CompositionTypeRuleBuilder\<T\>\.Use\(T\)\.value') \- matches any stage\-4 request for exactly [T](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.T 'Compono\.CompositionTypeRuleBuilder\<T\>\.T'), regardless of which member/position requested it\. |

<a name='Compono.CompositionTypeRuleBuilder_T_.Use(System.Func_Compono.ICompositionContext,T_)'></a>

## CompositionTypeRuleBuilder\<T\>\.Use\(Func\<ICompositionContext,T\>\) Method

Registers a type rule whose value is produced by [factory](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.Use(System.Func_Compono.ICompositionContext,T_).factory 'Compono\.CompositionTypeRuleBuilder\<T\>\.Use\(System\.Func\<Compono\.ICompositionContext,T\>\)\.factory') \- matches any
stage\-4 request for exactly [T](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.T 'Compono\.CompositionTypeRuleBuilder\<T\>\.T'), regardless of which member/position
requested it\.

```csharp
public Compono.CompositionBuilder Use(System.Func<Compono.ICompositionContext,T> factory);
```
#### Parameters

<a name='Compono.CompositionTypeRuleBuilder_T_.Use(System.Func_Compono.ICompositionContext,T_).factory'></a>

`factory` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[ICompositionContext](Compono.ICompositionContext.md 'Compono\.ICompositionContext')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[T](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.T 'Compono\.CompositionTypeRuleBuilder\<T\>\.T')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')

Produces the rule's value, given the resolving context\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

<a name='Compono.CompositionTypeRuleBuilder_T_.Use(T)'></a>

## CompositionTypeRuleBuilder\<T\>\.Use\(T\) Method

Registers a type rule that always produces [value](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.Use(T).value 'Compono\.CompositionTypeRuleBuilder\<T\>\.Use\(T\)\.value') \- matches any stage\-4
request for exactly [T](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.T 'Compono\.CompositionTypeRuleBuilder\<T\>\.T'), regardless of which member/position requested it\.

```csharp
public Compono.CompositionBuilder Use(T value);
```
#### Parameters

<a name='Compono.CompositionTypeRuleBuilder_T_.Use(T).value'></a>

`value` [T](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.T 'Compono\.CompositionTypeRuleBuilder\<T\>\.T')

The value this rule always produces\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')