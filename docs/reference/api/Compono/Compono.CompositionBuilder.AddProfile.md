#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

## CompositionBuilder\.AddProfile Method

| Overloads | |
| :--- | :--- |
| [AddProfile\(ICompositionProfile\)](Compono.CompositionBuilder.AddProfile.md#Compono.CompositionBuilder.AddProfile(Compono.ICompositionProfile) 'Compono\.CompositionBuilder\.AddProfile\(Compono\.ICompositionProfile\)') | Applies [profile](Compono.CompositionBuilder.md#Compono.CompositionBuilder.AddProfile(Compono.ICompositionProfile).profile 'Compono\.CompositionBuilder\.AddProfile\(Compono\.ICompositionProfile\)\.profile')'s [Configure\(CompositionBuilder\)](Compono.ICompositionProfile.Configure(Compono.CompositionBuilder).md 'Compono\.ICompositionProfile\.Configure\(Compono\.CompositionBuilder\)') to this builder immediately, synchronously \- the instance\-based counterpart to [AddProfile&lt;TProfile&gt;\(\)](Compono.CompositionBuilder.AddProfile.md#Compono.CompositionBuilder.AddProfile_TProfile_() 'Compono\.CompositionBuilder\.AddProfile\<TProfile\>\(\)'), for a profile that needs constructor arguments or is already an instance\. |
| [AddProfile&lt;TProfile&gt;\(\)](Compono.CompositionBuilder.AddProfile.md#Compono.CompositionBuilder.AddProfile_TProfile_() 'Compono\.CompositionBuilder\.AddProfile\<TProfile\>\(\)') | Applies [TProfile](Compono.CompositionBuilder.md#Compono.CompositionBuilder.AddProfile_TProfile_().TProfile 'Compono\.CompositionBuilder\.AddProfile\<TProfile\>\(\)\.TProfile')'s [Configure\(CompositionBuilder\)](Compono.ICompositionProfile.Configure(Compono.CompositionBuilder).md 'Compono\.ICompositionProfile\.Configure\(Compono\.CompositionBuilder\)') to this builder immediately, synchronously \- constructed via an ordinary, reflection\-free `new()`\. |

<a name='Compono.CompositionBuilder.AddProfile(Compono.ICompositionProfile)'></a>

## CompositionBuilder\.AddProfile\(ICompositionProfile\) Method

Applies [profile](Compono.CompositionBuilder.md#Compono.CompositionBuilder.AddProfile(Compono.ICompositionProfile).profile 'Compono\.CompositionBuilder\.AddProfile\(Compono\.ICompositionProfile\)\.profile')'s [Configure\(CompositionBuilder\)](Compono.ICompositionProfile.Configure(Compono.CompositionBuilder).md 'Compono\.ICompositionProfile\.Configure\(Compono\.CompositionBuilder\)') to this builder
immediately, synchronously \- the instance\-based counterpart to
[AddProfile&lt;TProfile&gt;\(\)](Compono.CompositionBuilder.AddProfile.md#Compono.CompositionBuilder.AddProfile_TProfile_() 'Compono\.CompositionBuilder\.AddProfile\<TProfile\>\(\)'), for a profile that needs constructor arguments or is
already an instance\.

```csharp
public Compono.CompositionBuilder AddProfile(Compono.ICompositionProfile profile);
```
#### Parameters

<a name='Compono.CompositionBuilder.AddProfile(Compono.ICompositionProfile).profile'></a>

`profile` [ICompositionProfile](Compono.ICompositionProfile.md 'Compono\.ICompositionProfile')

The profile instance to apply\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

#### Exceptions

[CompositionConfigurationException](Compono.CompositionConfigurationException.md 'Compono\.CompositionConfigurationException')  
Applying [profile](Compono.CompositionBuilder.md#Compono.CompositionBuilder.AddProfile(Compono.ICompositionProfile).profile 'Compono\.CompositionBuilder\.AddProfile\(Compono\.ICompositionProfile\)\.profile') would create a cycle \(by its declared CLR type\) \- thrown
immediately, containing exactly one [ProfileCycle](Compono.CompositionConfigurationError.ProfileCycle.md 'Compono\.CompositionConfigurationError\.ProfileCycle')
error naming the full chain\.

<a name='Compono.CompositionBuilder.AddProfile_TProfile_()'></a>

## CompositionBuilder\.AddProfile\<TProfile\>\(\) Method

Applies [TProfile](Compono.CompositionBuilder.md#Compono.CompositionBuilder.AddProfile_TProfile_().TProfile 'Compono\.CompositionBuilder\.AddProfile\<TProfile\>\(\)\.TProfile')'s [Configure\(CompositionBuilder\)](Compono.ICompositionProfile.Configure(Compono.CompositionBuilder).md 'Compono\.ICompositionProfile\.Configure\(Compono\.CompositionBuilder\)') to this
builder immediately, synchronously \- constructed via an ordinary, reflection\-free `new()`\.

```csharp
public Compono.CompositionBuilder AddProfile<TProfile>()
    where TProfile : Compono.ICompositionProfile, new();
```
#### Type parameters

<a name='Compono.CompositionBuilder.AddProfile_TProfile_().TProfile'></a>

`TProfile`

The profile type to construct and apply\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

#### Exceptions

[CompositionConfigurationException](Compono.CompositionConfigurationException.md 'Compono\.CompositionConfigurationException')  
Applying [TProfile](Compono.CompositionBuilder.md#Compono.CompositionBuilder.AddProfile_TProfile_().TProfile 'Compono\.CompositionBuilder\.AddProfile\<TProfile\>\(\)\.TProfile') would create a cycle \- thrown immediately, containing
exactly one [ProfileCycle](Compono.CompositionConfigurationError.ProfileCycle.md 'Compono\.CompositionConfigurationError\.ProfileCycle') error naming the full chain\.

### Remarks
A profile applied while another profile of the exact same declared type is already applying
\(directly or nested several levels deep\) is a cycle \- see
`docs/adr/0018-composition-profiles.md`\.