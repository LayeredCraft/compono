#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## CompositionMemberRuleBuilder\<TParent,TMember\> Class

A thin, member\-scoped view over a [CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')'s shared accumulator state \-
returned by [Member&lt;TMember&gt;\(Expression&lt;Func&lt;T,TMember&gt;&gt;\)](Compono.CompositionTypeRuleBuilder_T_.Member_TMember_(System.Linq.Expressions.Expression_System.Func_T,TMember__).md 'Compono\.CompositionTypeRuleBuilder\<T\>\.Member\<TMember\>\(System\.Linq\.Expressions\.Expression\<System\.Func\<T,TMember\>\>\)')\. Registers either a member
value rule \([Use\(TMember\)](Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(TMember) 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.Use\(TMember\)')/[Use\(Func&lt;ICompositionContext,TMember&gt;\)](Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(System.Func_Compono.ICompositionContext,TMember_) 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.Use\(System\.Func\<Compono\.ICompositionContext,TMember\>\)')\) or a
member\-scoped collection\-size override \([WithCollectionSize\(int\)](Compono.CompositionMemberRuleBuilder_TParent,TMember_.WithCollectionSize(int).md 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.WithCollectionSize\(int\)')\) for the exact
\(declaring type, member name\) pair captured when `.Member(...)` was called\. See
`docs/adr/0020-composition-configuration-rules.md`\.

```csharp
public sealed class CompositionMemberRuleBuilder<TParent,TMember>
```
#### Type parameters

<a name='Compono.CompositionMemberRuleBuilder_TParent,TMember_.TParent'></a>

`TParent`

The declaring type this rule is scoped to\.

<a name='Compono.CompositionMemberRuleBuilder_TParent,TMember_.TMember'></a>

`TMember`

The member's type\.

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → CompositionMemberRuleBuilder\<TParent,TMember\>

| Methods | |
| :--- | :--- |
| [Use\(Func&lt;ICompositionContext,TMember&gt;\)](Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(System.Func_Compono.ICompositionContext,TMember_) 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.Use\(System\.Func\<Compono\.ICompositionContext,TMember\>\)') | Registers a member rule whose value is produced by [factory](Compono.CompositionMemberRuleBuilder_TParent,TMember_.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(System.Func_Compono.ICompositionContext,TMember_).factory 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.Use\(System\.Func\<Compono\.ICompositionContext,TMember\>\)\.factory') for this member\. |
| [Use\(TMember\)](Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(TMember) 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.Use\(TMember\)') | Registers a member rule that always produces [value](Compono.CompositionMemberRuleBuilder_TParent,TMember_.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(TMember).value 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.Use\(TMember\)\.value') for this member\. |
| [WithCollectionSize\(int\)](Compono.CompositionMemberRuleBuilder_TParent,TMember_.WithCollectionSize(int).md 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>\.WithCollectionSize\(int\)') | Overrides the default collection size for this member only, following the same [WithCollectionSize\(int\)](Compono.CompositionBuilder.WithCollectionSize(int).md 'Compono\.CompositionBuilder\.WithCollectionSize\(int\)') precedence: this override wins over the global default and the built\-in size of `3`\. |
