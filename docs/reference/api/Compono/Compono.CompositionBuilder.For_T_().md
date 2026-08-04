#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

## CompositionBuilder\.For\<T\>\(\) Method

Starts a type or member configuration rule for [T](Compono.CompositionBuilder.For_T_().md#Compono.CompositionBuilder.For_T_().T 'Compono\.CompositionBuilder\.For\<T\>\(\)\.T') \- calling `.Use(...)`
directly registers a type rule \(matches any stage\-4 request for exactly [T](Compono.CompositionBuilder.For_T_().md#Compono.CompositionBuilder.For_T_().T 'Compono\.CompositionBuilder\.For\<T\>\(\)\.T'),
regardless of which member/position requested it\); calling `.Member(x => x.Y)` first
registers a member rule instead \(matches only requests for that exact declaring type/member
pair\)\. See `docs/adr/0020-composition-configuration-rules.md`\.

```csharp
public Compono.CompositionTypeRuleBuilder<T> For<T>();
```
#### Type parameters

<a name='Compono.CompositionBuilder.For_T_().T'></a>

`T`

The type to configure a rule for\.

#### Returns
[Compono\.CompositionTypeRuleBuilder&lt;](Compono.CompositionTypeRuleBuilder_T_.md 'Compono\.CompositionTypeRuleBuilder\<T\>')[T](Compono.CompositionBuilder.For_T_().md#Compono.CompositionBuilder.For_T_().T 'Compono\.CompositionBuilder\.For\<T\>\(\)\.T')[&gt;](Compono.CompositionTypeRuleBuilder_T_.md 'Compono\.CompositionTypeRuleBuilder\<T\>')