#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionTypeRuleBuilder&lt;T&gt;](Compono.CompositionTypeRuleBuilder_T_.md 'Compono\.CompositionTypeRuleBuilder\<T\>')

## CompositionTypeRuleBuilder\<T\>\.Member\<TMember\>\(Expression\<Func\<T,TMember\>\>\) Method

Scopes this rule to a single member of [T](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.T 'Compono\.CompositionTypeRuleBuilder\<T\>\.T') \- parsed immediately, at the
point this method is called, not deferred to `Build()`\.

```csharp
public Compono.CompositionMemberRuleBuilder<T,TMember> Member<TMember>(System.Linq.Expressions.Expression<System.Func<T,TMember>> member);
```
#### Type parameters

<a name='Compono.CompositionTypeRuleBuilder_T_.Member_TMember_(System.Linq.Expressions.Expression_System.Func_T,TMember__).TMember'></a>

`TMember`

The member's type\.
#### Parameters

<a name='Compono.CompositionTypeRuleBuilder_T_.Member_TMember_(System.Linq.Expressions.Expression_System.Func_T,TMember__).member'></a>

`member` [System\.Linq\.Expressions\.Expression&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1 'System\.Linq\.Expressions\.Expression\`1')[System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[T](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.T 'Compono\.CompositionTypeRuleBuilder\<T\>\.T')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[TMember](Compono.CompositionTypeRuleBuilder_T_.Member_TMember_(System.Linq.Expressions.Expression_System.Func_T,TMember__).md#Compono.CompositionTypeRuleBuilder_T_.Member_TMember_(System.Linq.Expressions.Expression_System.Func_T,TMember__).TMember 'Compono\.CompositionTypeRuleBuilder\<T\>\.Member\<TMember\>\(System\.Linq\.Expressions\.Expression\<System\.Func\<T,TMember\>\>\)\.TMember')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1 'System\.Linq\.Expressions\.Expression\`1')

A direct property or field access, e\.g\. `x => x.Email`\.

#### Returns
[Compono\.CompositionMemberRuleBuilder&lt;](Compono.CompositionMemberRuleBuilder_TParent,TMember_.md 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>')[T](Compono.CompositionTypeRuleBuilder_T_.md#Compono.CompositionTypeRuleBuilder_T_.T 'Compono\.CompositionTypeRuleBuilder\<T\>\.T')[,](Compono.CompositionMemberRuleBuilder_TParent,TMember_.md 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>')[TMember](Compono.CompositionTypeRuleBuilder_T_.Member_TMember_(System.Linq.Expressions.Expression_System.Func_T,TMember__).md#Compono.CompositionTypeRuleBuilder_T_.Member_TMember_(System.Linq.Expressions.Expression_System.Func_T,TMember__).TMember 'Compono\.CompositionTypeRuleBuilder\<T\>\.Member\<TMember\>\(System\.Linq\.Expressions\.Expression\<System\.Func\<T,TMember\>\>\)\.TMember')[&gt;](Compono.CompositionMemberRuleBuilder_TParent,TMember_.md 'Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>')

#### Exceptions

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[member](Compono.CompositionTypeRuleBuilder_T_.Member_TMember_(System.Linq.Expressions.Expression_System.Func_T,TMember__).md#Compono.CompositionTypeRuleBuilder_T_.Member_TMember_(System.Linq.Expressions.Expression_System.Func_T,TMember__).member 'Compono\.CompositionTypeRuleBuilder\<T\>\.Member\<TMember\>\(System\.Linq\.Expressions\.Expression\<System\.Func\<T,TMember\>\>\)\.member') is not a direct property or field access\.