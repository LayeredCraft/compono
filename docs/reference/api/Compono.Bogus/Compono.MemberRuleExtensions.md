#### [Compono\.Bogus](index.md 'index')
### [Compono](Compono.md 'Compono')

## MemberRuleExtensions Class

Adds Bogus\-backed sugar to [CompositionMemberRuleBuilder&lt;TParent,TMember&gt;](../Compono/Compono.CompositionMemberRuleBuilder_TParent,TMember_.md 'Compono\.CompositionMemberRuleBuilder\`2') \- an
explicit member rule whose value comes from a deterministically\-seeded `Bogus.Faker`, for a
member the conservative [BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider') convention allowlist doesn't \(or
shouldn't\) guess\. See `docs/adr/0027-compono-bogus-package-design.md`\.

```csharp
public static class MemberRuleExtensions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → MemberRuleExtensions

| Methods | |
| :--- | :--- |
| [UseBogus&lt;TParent,TMember&gt;\(this CompositionMemberRuleBuilder&lt;TParent,TMember&gt;, Func&lt;Faker,TMember&gt;, string\)](Compono.MemberRuleExtensions.UseBogus_TParent,TMember_(thisCompono.CompositionMemberRuleBuilder_TParent,TMember_,System.Func_Bogus.Faker,TMember_,string).md 'Compono\.MemberRuleExtensions\.UseBogus\<TParent,TMember\>\(this Compono\.CompositionMemberRuleBuilder\<TParent,TMember\>, System\.Func\<Bogus\.Faker,TMember\>, string\)') | Registers a member rule whose value comes from a deterministically\-seeded `Bogus.Faker` \- purely ergonomic sugar over [Use\(Func&lt;ICompositionContext,TMember&gt;\)](../Compono/Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use.md#Compono.CompositionMemberRuleBuilder_TParent,TMember_.Use(System.Func_Compono.ICompositionContext,TMember_) 'Compono\.CompositionMemberRuleBuilder\`2\.Use\(System\.Func\{Compono\.ICompositionContext,\`1\}\)')\. Always wins over [BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider')'s convention guess for the same member, since stage 4 \(configuration rules\) runs before stage 5 \(semantic providers\) unconditionally\. |
