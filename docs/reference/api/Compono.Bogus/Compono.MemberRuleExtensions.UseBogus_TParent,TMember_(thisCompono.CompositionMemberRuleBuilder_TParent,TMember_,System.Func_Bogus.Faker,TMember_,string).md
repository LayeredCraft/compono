#### [Compono\.Bogus](index.md 'index')
### [Compono](Compono.md 'Compono').[MemberRuleExtensions](Compono.MemberRuleExtensions.md 'Compono\.MemberRuleExtensions')

## MemberRuleExtensions\.UseBogus\<TParent,TMember\>\(this CompositionMemberRuleBuilder\<TParent,TMember\>, Func\<Faker,TMember\>, string\) Method

Registers a member rule whose value comes from a deterministically\-seeded [Bogus\.Faker](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker 'Bogus\.Faker')
\- purely ergonomic sugar over
[Use\(Func&lt;ICompositionContext,TMember&gt;\)](../Compono/Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(System.Func_Compono.ICompositionContext,TMember_) 'Compono\.CompositionMemberRuleBuilder\`2\.Use\(System\.Func\{Compono\.ICompositionContext,\`1\}\)')\.
Always wins over [BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider')'s convention guess for the same
member, since stage 4 \(configuration rules\) runs before stage 5 \(semantic providers\)
unconditionally\.

```csharp
public static Compono.CompositionBuilder UseBogus<TParent,TMember>(this Compono.CompositionMemberRuleBuilder<TParent,TMember> builder, System.Func<Bogus.Faker,TMember> configure, string locale="en")
    where TParent : notnull
    where TMember : notnull;
```
#### Type parameters

<a name='Compono.MemberRuleExtensions.UseBogus_TParent,TMember_(thisCompono.CompositionMemberRuleBuilder_TParent,TMember_,System.Func_Bogus.Faker,TMember_,string).TParent'></a>

`TParent`

<a name='Compono.MemberRuleExtensions.UseBogus_TParent,TMember_(thisCompono.CompositionMemberRuleBuilder_TParent,TMember_,System.Func_Bogus.Faker,TMember_,string).TMember'></a>

`TMember`
#### Parameters

<a name='Compono.MemberRuleExtensions.UseBogus_TParent,TMember_(thisCompono.CompositionMemberRuleBuilder_TParent,TMember_,System.Func_Bogus.Faker,TMember_,string).builder'></a>

`builder` [Compono\.CompositionMemberRuleBuilder&lt;](../Compono/Compono.CompositionMemberRuleBuilder_TParent,TMember_.md 'Compono\.CompositionMemberRuleBuilder\`2')[TParent](Compono.MemberRuleExtensions.UseBogus_TParent,TMember_(thisCompono.CompositionMemberRuleBuilder_TParent,TMember_,System.Func_Bogus.Faker,TMember_,string).md#Compono.MemberRuleExtensions.UseBogus_TParent,TMember_(thisCompono.CompositionMemberRuleBuilder_TParent,TMember_,System.Func_Bogus.Faker,TMember_,string).TParent 'Compono\.MemberRuleExtensions\.UseBogus\<TParent,TMember\>\(this Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>, System\.Func\<Bogus\.Faker,TMember\>, string\)\.TParent')[,](../Compono/Compono.CompositionMemberRuleBuilder_TParent,TMember_.md 'Compono\.CompositionMemberRuleBuilder\`2')[TMember](Compono.MemberRuleExtensions.UseBogus_TParent,TMember_(thisCompono.CompositionMemberRuleBuilder_TParent,TMember_,System.Func_Bogus.Faker,TMember_,string).md#Compono.MemberRuleExtensions.UseBogus_TParent,TMember_(thisCompono.CompositionMemberRuleBuilder_TParent,TMember_,System.Func_Bogus.Faker,TMember_,string).TMember 'Compono\.MemberRuleExtensions\.UseBogus\<TParent,TMember\>\(this Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>, System\.Func\<Bogus\.Faker,TMember\>, string\)\.TMember')[&gt;](../Compono/Compono.CompositionMemberRuleBuilder_TParent,TMember_.md 'Compono\.CompositionMemberRuleBuilder\`2')

<a name='Compono.MemberRuleExtensions.UseBogus_TParent,TMember_(thisCompono.CompositionMemberRuleBuilder_TParent,TMember_,System.Func_Bogus.Faker,TMember_,string).configure'></a>

`configure` [System\.Func&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[Bogus\.Faker](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker 'Bogus\.Faker')[,](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')[TMember](Compono.MemberRuleExtensions.UseBogus_TParent,TMember_(thisCompono.CompositionMemberRuleBuilder_TParent,TMember_,System.Func_Bogus.Faker,TMember_,string).md#Compono.MemberRuleExtensions.UseBogus_TParent,TMember_(thisCompono.CompositionMemberRuleBuilder_TParent,TMember_,System.Func_Bogus.Faker,TMember_,string).TMember 'Compono\.MemberRuleExtensions\.UseBogus\<TParent,TMember\>\(this Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>, System\.Func\<Bogus\.Faker,TMember\>, string\)\.TMember')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-2 'System\.Func\`2')

Produces this member's value from a seeded [Bogus\.Faker](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker 'Bogus\.Faker')\.

<a name='Compono.MemberRuleExtensions.UseBogus_TParent,TMember_(thisCompono.CompositionMemberRuleBuilder_TParent,TMember_,System.Func_Bogus.Faker,TMember_,string).locale'></a>

`locale` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The Bogus locale this rule's own [Bogus\.Faker](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker 'Bogus\.Faker') uses\.

#### Returns
[CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')