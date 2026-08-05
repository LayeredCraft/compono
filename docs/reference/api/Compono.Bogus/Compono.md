#### [Compono\.Bogus](index.md 'index')

## Compono Namespace

| Classes | |
| :--- | :--- |
| [BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider') | A stage\-5 semantic value provider that matches an exact, conservative allowlist of `string`\-typed member names \(`FirstName`, `Email`, etc\.\) against a real, deterministically\-seeded `Bogus.Faker` value\. Registered via `CompositionBuilderExtensions.UseBogus()`\. See `docs/adr/0027-compono-bogus-package-design.md`\. |
| [BogusOptions](Compono.BogusOptions.md 'Compono\.BogusOptions') | Configuration for [BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider'), set via `CompositionBuilderExtensions.UseBogus(Action{BogusOptions})`\. See `docs/adr/0027-compono-bogus-package-design.md` and `docs/adr/0028-configurable-bogus-member-name-conventions.md`\. |
| [CompositionBuilderExtensions](Compono.CompositionBuilderExtensions.md 'Compono\.CompositionBuilderExtensions') | Activates [BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider') on a [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder'), and registers a whole\-object `Bogus.Faker&lt;&gt;`\-backed exact registration for one type\. See `docs/adr/0027-compono-bogus-package-design.md`\. |
| [MemberRuleExtensions](Compono.MemberRuleExtensions.md 'Compono\.MemberRuleExtensions') | Adds Bogus\-backed sugar to [CompositionMemberRuleBuilder&lt;TParent,TMember&gt;](../Compono/Compono.CompositionMemberRuleBuilder_TParent,TMember_.md 'Compono\.CompositionMemberRuleBuilder\`2') \- an explicit member rule whose value comes from a deterministically\-seeded `Bogus.Faker`, for a member the conservative [BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider') convention allowlist doesn't \(or shouldn't\) guess\. See `docs/adr/0027-compono-bogus-package-design.md`\. |

| Enums | |
| :--- | :--- |
| [BogusConvention](Compono.BogusConvention.md 'Compono\.BogusConvention') | One of Compono\.Bogus's fixed set of built\-in, conservative member\-name conventions \- see `docs/adr/0027-compono-bogus-package-design.md`'s Model 1\. Deliberately not extensible: a new built\-in convention requires a new enum member, a generator mapping, documentation, and tests, not a value a consumer can define themselves \- custom behavior belongs in [AddConvention\(string, Func&lt;Faker,string&gt;\)](Compono.BogusOptions.AddConvention(string,System.Func_Bogus.Faker,string_).md 'Compono\.BogusOptions\.AddConvention\(string, System\.Func\<Bogus\.Faker,string\>\)'), not in this enum\. See `docs/adr/0028-configurable-bogus-member-name-conventions.md`\. |
