#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionMemberRuleBuilder&lt;TParent,TMember&gt;](Compono.CompositionMemberRuleBuilder_TParent,TMember_.md 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>')

## CompositionMemberRuleBuilder\<TParent,TMember\>\.Use Method

| Overloads | |
| :--- | :--- |
| [Use\(Func&lt;ICompositionContext,TMember&gt;\)](Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(System.Func_Compono.ICompositionContext,TMember_) 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.Use\(System\.Func\<Compono\.ICompositionContext,TMember\>\)') | Registers a member rule whose value is produced by [factory](Compono.CompositionMemberRuleBuilder_TParent,TMember_.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(System.Func_Compono.ICompositionContext,TMember_).factory 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.Use\(System\.Func\<Compono\.ICompositionContext,TMember\>\)\.factory') for this member\. |
| [Use\(TMember\)](Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(TMember) 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.Use\(TMember\)') | Registers a member rule that always produces [value](Compono.CompositionMemberRuleBuilder_TParent,TMember_.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(TMember).value 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.Use\(TMember\)\.value') for this member\. |

<a name='Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(System.Func_Compono.ICompositionContext,TMember_)'></a>

## CompositionMemberRuleBuilder\<TParent,TMember\>\.Use\(Func\<ICompositionContext,TMember\>\) Method

Registers a member rule whose value is produced by [factory](Compono.CompositionMemberRuleBuilder_TParent,TMember_.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(System.Func_Compono.ICompositionContext,TMember_).factory 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.Use\(System\.Func\<Compono\.ICompositionContext,TMember\>\)\.factory') for this member\.

```csharp
public Compono.CompositionBuilder Use(System.Func<Compono.ICompositionContext,TMember> factory);
```
#### Parameters

<a name='Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(System.Func_Compono.ICompositionContext,TMember_).factory'></a>

`factory` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[ICompositionContext](Compono.ICompositionContext.md 'Compono\.ICompositionContext')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[TMember](Compono.CompositionMemberRuleBuilder_TParent,TMember_.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.TMember 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.TMember')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')

Produces the rule's value, given the resolving context\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

<a name='Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(TMember)'></a>

## CompositionMemberRuleBuilder\<TParent,TMember\>\.Use\(TMember\) Method

Registers a member rule that always produces [value](Compono.CompositionMemberRuleBuilder_TParent,TMember_.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(TMember).value 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.Use\(TMember\)\.value') for this member\.

```csharp
public Compono.CompositionBuilder Use(TMember value);
```
#### Parameters

<a name='Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(TMember).value'></a>

`value` [TMember](Compono.CompositionMemberRuleBuilder_TParent,TMember_.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.TMember 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.TMember')

The value this rule always produces\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')