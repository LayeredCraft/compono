#### [Compono\.Bogus](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionBuilderExtensions](Compono.CompositionBuilderExtensions.md 'Compono\.CompositionBuilderExtensions')

## CompositionBuilderExtensions\.UseBogus Method

| Overloads | |
| :--- | :--- |
| [UseBogus\(this CompositionBuilder\)](Compono.CompositionBuilderExtensions.UseBogus.md#Compono.CompositionBuilderExtensions.UseBogus(thisCompono.CompositionBuilder) 'Compono\.CompositionBuilderExtensions\.UseBogus\(this Compono\.CompositionBuilder\)') | Registers a [BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider') with default [BogusOptions](Compono.BogusOptions.md 'Compono\.BogusOptions')\. |
| [UseBogus\(this CompositionBuilder, Action&lt;BogusOptions&gt;\)](Compono.CompositionBuilderExtensions.UseBogus.md#Compono.CompositionBuilderExtensions.UseBogus(thisCompono.CompositionBuilder,System.Action_Compono.BogusOptions_) 'Compono\.CompositionBuilderExtensions\.UseBogus\(this Compono\.CompositionBuilder, System\.Action\<Compono\.BogusOptions\>\)') | Registers a [BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider'), configured by [configure](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseBogus(thisCompono.CompositionBuilder,System.Action_Compono.BogusOptions_).configure 'Compono\.CompositionBuilderExtensions\.UseBogus\(this Compono\.CompositionBuilder, System\.Action\<Compono\.BogusOptions\>\)\.configure')\. |
| [UseBogus&lt;T&gt;\(this CompositionBuilder, string, Action&lt;Faker&lt;T&gt;&gt;\)](Compono.CompositionBuilderExtensions.UseBogus.md#Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,string,System.Action_Bogus.Faker_T__) 'Compono\.CompositionBuilderExtensions\.UseBogus\<T\>\(this Compono\.CompositionBuilder, string, System\.Action\<Bogus\.Faker\<T\>\>\)') | Registers [T](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,string,System.Action_Bogus.Faker_T__).T 'Compono\.CompositionBuilderExtensions\.UseBogus\<T\>\(this Compono\.CompositionBuilder, string, System\.Action\<Bogus\.Faker\<T\>\>\)\.T') as exclusively Bogus\-generated, via a [Bogus\.Faker&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker-1 'Bogus\.Faker\`1') instance [configureFaker](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,string,System.Action_Bogus.Faker_T__).configureFaker 'Compono\.CompositionBuilderExtensions\.UseBogus\<T\>\(this Compono\.CompositionBuilder, string, System\.Action\<Bogus\.Faker\<T\>\>\)\.configureFaker') configures, using [locale](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,string,System.Action_Bogus.Faker_T__).locale 'Compono\.CompositionBuilderExtensions\.UseBogus\<T\>\(this Compono\.CompositionBuilder, string, System\.Action\<Bogus\.Faker\<T\>\>\)\.locale')\. Purely ergonomic sugar over [Register&lt;T&gt;\(Func&lt;ICompositionContext,T&gt;\)](../Compono/Compono.CompositionBuilder.Register.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_) 'Compono\.CompositionBuilder\.Register\`\`1\(System\.Func\{Compono\.ICompositionContext,\`\`0\}\)') \(stage 3\) \- no hidden pipeline stage, no special runtime behavior of its own\. Independent of `UseBogus()`/`UseBogus(Action{BogusOptions})` \- never reads [Locale](Compono.BogusOptions.Locale.md 'Compono\.BogusOptions\.Locale')\. |
| [UseBogus&lt;T&gt;\(this CompositionBuilder, Action&lt;Faker&lt;T&gt;&gt;\)](Compono.CompositionBuilderExtensions.UseBogus.md#Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,System.Action_Bogus.Faker_T__) 'Compono\.CompositionBuilderExtensions\.UseBogus\<T\>\(this Compono\.CompositionBuilder, System\.Action\<Bogus\.Faker\<T\>\>\)') | Registers [T](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,System.Action_Bogus.Faker_T__).T 'Compono\.CompositionBuilderExtensions\.UseBogus\<T\>\(this Compono\.CompositionBuilder, System\.Action\<Bogus\.Faker\<T\>\>\)\.T') as exclusively Bogus\-generated, via a [Bogus\.Faker&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker-1 'Bogus\.Faker\`1') instance [configureFaker](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,System.Action_Bogus.Faker_T__).configureFaker 'Compono\.CompositionBuilderExtensions\.UseBogus\<T\>\(this Compono\.CompositionBuilder, System\.Action\<Bogus\.Faker\<T\>\>\)\.configureFaker') configures\. Purely ergonomic sugar over [Register&lt;T&gt;\(Func&lt;ICompositionContext,T&gt;\)](../Compono/Compono.CompositionBuilder.Register.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_) 'Compono\.CompositionBuilder\.Register\`\`1\(System\.Func\{Compono\.ICompositionContext,\`\`0\}\)') \(stage 3\) \- no hidden pipeline stage, no special runtime behavior of its own\. Independent of `UseBogus()`/`UseBogus(Action{BogusOptions})` \- defaults to the locale `"en"` on its own, never reads [Locale](Compono.BogusOptions.Locale.md 'Compono\.BogusOptions\.Locale')\. |

<a name='Compono.CompositionBuilderExtensions.UseBogus(thisCompono.CompositionBuilder)'></a>

## CompositionBuilderExtensions\.UseBogus\(this CompositionBuilder\) Method

Registers a [BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider') with default [BogusOptions](Compono.BogusOptions.md 'Compono\.BogusOptions')\.

```csharp
public static Compono.CompositionBuilder UseBogus(this Compono.CompositionBuilder builder);
```
#### Parameters

<a name='Compono.CompositionBuilderExtensions.UseBogus(thisCompono.CompositionBuilder).builder'></a>

`builder` [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

#### Returns
[CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

<a name='Compono.CompositionBuilderExtensions.UseBogus(thisCompono.CompositionBuilder,System.Action_Compono.BogusOptions_)'></a>

## CompositionBuilderExtensions\.UseBogus\(this CompositionBuilder, Action\<BogusOptions\>\) Method

Registers a [BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider'), configured by [configure](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseBogus(thisCompono.CompositionBuilder,System.Action_Compono.BogusOptions_).configure 'Compono\.CompositionBuilderExtensions\.UseBogus\(this Compono\.CompositionBuilder, System\.Action\<Compono\.BogusOptions\>\)\.configure')\.

```csharp
public static Compono.CompositionBuilder UseBogus(this Compono.CompositionBuilder builder, System.Action<Compono.BogusOptions> configure);
```
#### Parameters

<a name='Compono.CompositionBuilderExtensions.UseBogus(thisCompono.CompositionBuilder,System.Action_Compono.BogusOptions_).builder'></a>

`builder` [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

<a name='Compono.CompositionBuilderExtensions.UseBogus(thisCompono.CompositionBuilder,System.Action_Compono.BogusOptions_).configure'></a>

`configure` [System\.Action&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')[BogusOptions](Compono.BogusOptions.md 'Compono\.BogusOptions')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')

Sets the provider's [BogusOptions](Compono.BogusOptions.md 'Compono\.BogusOptions')\.

#### Returns
[CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

<a name='Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,string,System.Action_Bogus.Faker_T__)'></a>

## CompositionBuilderExtensions\.UseBogus\<T\>\(this CompositionBuilder, string, Action\<Faker\<T\>\>\) Method

Registers [T](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,string,System.Action_Bogus.Faker_T__).T 'Compono\.CompositionBuilderExtensions\.UseBogus\<T\>\(this Compono\.CompositionBuilder, string, System\.Action\<Bogus\.Faker\<T\>\>\)\.T') as exclusively Bogus\-generated, via a
[Bogus\.Faker&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker-1 'Bogus\.Faker\`1') instance [configureFaker](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,string,System.Action_Bogus.Faker_T__).configureFaker 'Compono\.CompositionBuilderExtensions\.UseBogus\<T\>\(this Compono\.CompositionBuilder, string, System\.Action\<Bogus\.Faker\<T\>\>\)\.configureFaker') configures, using
[locale](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,string,System.Action_Bogus.Faker_T__).locale 'Compono\.CompositionBuilderExtensions\.UseBogus\<T\>\(this Compono\.CompositionBuilder, string, System\.Action\<Bogus\.Faker\<T\>\>\)\.locale')\. Purely ergonomic sugar over
[Register&lt;T&gt;\(Func&lt;ICompositionContext,T&gt;\)](../Compono/Compono.CompositionBuilder.Register.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_) 'Compono\.CompositionBuilder\.Register\`\`1\(System\.Func\{Compono\.ICompositionContext,\`\`0\}\)') \(stage 3\) \- no
hidden pipeline stage, no special runtime behavior of its own\. Independent of
`UseBogus()`/`UseBogus(Action{BogusOptions})` \- never reads
[Locale](Compono.BogusOptions.Locale.md 'Compono\.BogusOptions\.Locale')\.

```csharp
public static Compono.CompositionBuilder UseBogus<T>(this Compono.CompositionBuilder builder, string locale, System.Action<Bogus.Faker<T>> configureFaker)
    where T : class;
```
#### Type parameters

<a name='Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,string,System.Action_Bogus.Faker_T__).T'></a>

`T`

The type [Bogus\.Faker&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker-1 'Bogus\.Faker\`1') generates\.
#### Parameters

<a name='Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,string,System.Action_Bogus.Faker_T__).builder'></a>

`builder` [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

<a name='Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,string,System.Action_Bogus.Faker_T__).locale'></a>

`locale` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The Bogus locale this registration's own [Bogus\.Faker&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker-1 'Bogus\.Faker\`1') uses\.

<a name='Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,string,System.Action_Bogus.Faker_T__).configureFaker'></a>

`configureFaker` [System\.Action&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')[Bogus\.Faker&lt;](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker-1 'Bogus\.Faker\`1')[T](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,string,System.Action_Bogus.Faker_T__).T 'Compono\.CompositionBuilderExtensions\.UseBogus\<T\>\(this Compono\.CompositionBuilder, string, System\.Action\<Bogus\.Faker\<T\>\>\)\.T')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker-1 'Bogus\.Faker\`1')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')

Configures the [Bogus\.Faker&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker-1 'Bogus\.Faker\`1') instance in place \(e\.g\. via `RuleFor`\) \- does not
need to, and should not, return a different instance\.

#### Returns
[CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

<a name='Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,System.Action_Bogus.Faker_T__)'></a>

## CompositionBuilderExtensions\.UseBogus\<T\>\(this CompositionBuilder, Action\<Faker\<T\>\>\) Method

Registers [T](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,System.Action_Bogus.Faker_T__).T 'Compono\.CompositionBuilderExtensions\.UseBogus\<T\>\(this Compono\.CompositionBuilder, System\.Action\<Bogus\.Faker\<T\>\>\)\.T') as exclusively Bogus\-generated, via a
[Bogus\.Faker&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker-1 'Bogus\.Faker\`1') instance [configureFaker](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,System.Action_Bogus.Faker_T__).configureFaker 'Compono\.CompositionBuilderExtensions\.UseBogus\<T\>\(this Compono\.CompositionBuilder, System\.Action\<Bogus\.Faker\<T\>\>\)\.configureFaker') configures\. Purely
ergonomic sugar over [Register&lt;T&gt;\(Func&lt;ICompositionContext,T&gt;\)](../Compono/Compono.CompositionBuilder.Register.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_) 'Compono\.CompositionBuilder\.Register\`\`1\(System\.Func\{Compono\.ICompositionContext,\`\`0\}\)')
\(stage 3\) \- no hidden pipeline stage, no special runtime behavior of its own\. Independent of
`UseBogus()`/`UseBogus(Action{BogusOptions})` \- defaults to the
locale `"en"` on its own, never reads [Locale](Compono.BogusOptions.Locale.md 'Compono\.BogusOptions\.Locale')\.

```csharp
public static Compono.CompositionBuilder UseBogus<T>(this Compono.CompositionBuilder builder, System.Action<Bogus.Faker<T>> configureFaker)
    where T : class;
```
#### Type parameters

<a name='Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,System.Action_Bogus.Faker_T__).T'></a>

`T`

The type [Bogus\.Faker&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker-1 'Bogus\.Faker\`1') generates\.
#### Parameters

<a name='Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,System.Action_Bogus.Faker_T__).builder'></a>

`builder` [CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

<a name='Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,System.Action_Bogus.Faker_T__).configureFaker'></a>

`configureFaker` [System\.Action&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')[Bogus\.Faker&lt;](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker-1 'Bogus\.Faker\`1')[T](Compono.CompositionBuilderExtensions.md#Compono.CompositionBuilderExtensions.UseBogus_T_(thisCompono.CompositionBuilder,System.Action_Bogus.Faker_T__).T 'Compono\.CompositionBuilderExtensions\.UseBogus\<T\>\(this Compono\.CompositionBuilder, System\.Action\<Bogus\.Faker\<T\>\>\)\.T')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker-1 'Bogus\.Faker\`1')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')

Configures the [Bogus\.Faker&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/bogus.faker-1 'Bogus\.Faker\`1') instance in place \(e\.g\. via `RuleFor`\) \- does not
need to, and should not, return a different instance\.

#### Returns
[CompositionBuilder](../Compono/Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')