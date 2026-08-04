#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## CompositionTypeRuleBuilder\<T\> Class

A thin, type\-scoped view over a [CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')'s shared accumulator state \-
returned by [For&lt;T&gt;\(\)](Compono.CompositionBuilder.For_T_().md 'Compono\.CompositionBuilder\.For\<T\>\(\)')\. Calling [Use\(T\)](Compono.CompositionTypeRuleBuilder_T_.Use.md#Compono.CompositionTypeRuleBuilder_T_.Use(T) 'Compono\.CompositionTypeRuleBuilder\<T\>\.Use\(T\)')/
[Use\(Func&lt;ICompositionContext,T&gt;\)](Compono.CompositionTypeRuleBuilder_T_.Use.md#Compono.CompositionTypeRuleBuilder_T_.Use(System.Func_Compono.ICompositionContext,T_) 'Compono\.CompositionTypeRuleBuilder\<T\>\.Use\(System\.Func\<Compono\.ICompositionContext,T\>\)') directly registers a type rule; calling
[Member&lt;TMember&gt;\(Expression&lt;Func&lt;T,TMember&gt;&gt;\)](Compono.CompositionTypeRuleBuilder_T_.Member_TMember_(System.Linq.Expressions.Expression_System.Func_T,TMember__).md 'Compono\.CompositionTypeRuleBuilder\<T\>\.Member\<TMember\>\(System\.Linq\.Expressions\.Expression\<System\.Func\<T,TMember\>\>\)') first returns a further\-scoped
[CompositionMemberRuleBuilder&lt;TParent,TMember&gt;](Compono.CompositionMemberRuleBuilder_TParent,TMember_.md 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>') whose own `Use`/
`WithCollectionSize` register a member rule instead\. See
`docs/adr/0020-composition-configuration-rules.md`\.

```csharp
public sealed class CompositionTypeRuleBuilder<T>
```
#### Type parameters

<a name='Compono.CompositionTypeRuleBuilder_T_.T'></a>

`T`

The type this rule builder is scoped to\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → CompositionTypeRuleBuilder\<T\>

| Methods | |
| :--- | :--- |
| [Member&lt;TMember&gt;\(Expression&lt;Func&lt;T,TMember&gt;&gt;\)](Compono.CompositionTypeRuleBuilder_T_.Member_TMember_(System.Linq.Expressions.Expression_System.Func_T,TMember__).md 'Compono\.CompositionTypeRuleBuilder\<T\>\.Member\<TMember\>\(System\.Linq\.Expressions\.Expression\<System\.Func\<T,TMember\>\>\)') | Scopes this rule to a single member of [T](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.T 'Compono\.CompositionTypeRuleBuilder\<T\>\.T') \- parsed immediately, at the point this method is called, not deferred to `Build()`\. |
| [Use\(Func&lt;ICompositionContext,T&gt;\)](Compono.CompositionTypeRuleBuilder_T_.Use.md#Compono.CompositionTypeRuleBuilder_T_.Use(System.Func_Compono.ICompositionContext,T_) 'Compono\.CompositionTypeRuleBuilder\<T\>\.Use\(System\.Func\<Compono\.ICompositionContext,T\>\)') | Registers a type rule whose value is produced by [factory](Compono.CompositionTypeRuleBuilder_T_.Use.md#Compono.CompositionTypeRuleBuilder_T_.Use(System.Func_Compono.ICompositionContext,T_).factory 'Compono\.CompositionTypeRuleBuilder\<T\>\.Use\(System\.Func\<Compono\.ICompositionContext,T\>\)\.factory') \- matches any stage\-4 request for exactly [T](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.T 'Compono\.CompositionTypeRuleBuilder\<T\>\.T'), regardless of which member/position requested it\. |
| [Use\(T\)](Compono.CompositionTypeRuleBuilder_T_.Use.md#Compono.CompositionTypeRuleBuilder_T_.Use(T) 'Compono\.CompositionTypeRuleBuilder\<T\>\.Use\(T\)') | Registers a type rule that always produces [value](Compono.CompositionTypeRuleBuilder_T_.Use.md#Compono.CompositionTypeRuleBuilder_T_.Use(T).value 'Compono\.CompositionTypeRuleBuilder\<T\>\.Use\(T\)\.value') \- matches any stage\-4 request for exactly [T](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.T 'Compono\.CompositionTypeRuleBuilder\<T\>\.T'), regardless of which member/position requested it\. |
