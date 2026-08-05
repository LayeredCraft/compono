#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## ICompositionProfile Interface

A reusable, named grouping of [CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder') configuration \- registrations,
service\-provider fallback, configuration rules, and other profiles \- applied via
[AddProfile&lt;TProfile&gt;\(\)](Compono.CompositionBuilder.AddProfile.md#Compono.CompositionBuilder.AddProfile_TProfile_() 'Compono\.CompositionBuilder\.AddProfile\<TProfile\>\(\)')/[AddProfile\(ICompositionProfile\)](Compono.CompositionBuilder.AddProfile.md#Compono.CompositionBuilder.AddProfile(Compono.ICompositionProfile) 'Compono\.CompositionBuilder\.AddProfile\(Compono\.ICompositionProfile\)')\.

```csharp
public interface ICompositionProfile
```

### Remarks
A profile introduces no new engine concept of its own \- [Configure\(CompositionBuilder\)](Compono.ICompositionProfile.Configure(Compono.CompositionBuilder).md 'Compono\.ICompositionProfile\.Configure\(Compono\.CompositionBuilder\)') calls the exact same
builder verbs a direct [Create\(Action&lt;CompositionBuilder&gt;\)](Compono.Composer.Create.md#Compono.Composer.Create(System.Action_Compono.CompositionBuilder_) 'Compono\.Composer\.Create\(System\.Action\<Compono\.CompositionBuilder\>\)') caller would\. See
`docs/adr/0018-composition-profiles.md`\.

| Methods | |
| :--- | :--- |
| [Configure\(CompositionBuilder\)](Compono.ICompositionProfile.Configure(Compono.CompositionBuilder).md 'Compono\.ICompositionProfile\.Configure\(Compono\.CompositionBuilder\)') | Applies this profile's configuration to [builder](Compono.ICompositionProfile.Configure(Compono.CompositionBuilder).md#Compono.ICompositionProfile.Configure(Compono.CompositionBuilder).builder 'Compono\.ICompositionProfile\.Configure\(Compono\.CompositionBuilder\)\.builder') \- called synchronously, immediately, during the `AddProfile` call that applies this profile\. |
