#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ICompositionProfile](Compono.ICompositionProfile.md 'Compono\.ICompositionProfile')

## ICompositionProfile\.Configure\(CompositionBuilder\) Method

Applies this profile's configuration to [builder](Compono.ICompositionProfile.Configure(Compono.CompositionBuilder).md#Compono.ICompositionProfile.Configure(Compono.CompositionBuilder).builder 'Compono\.ICompositionProfile\.Configure\(Compono\.CompositionBuilder\)\.builder') \- called synchronously,
immediately, during the `AddProfile` call that applies this profile\.

```csharp
void Configure(Compono.CompositionBuilder builder);
```
#### Parameters

<a name='Compono.ICompositionProfile.Configure(Compono.CompositionBuilder).builder'></a>

`builder` [CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

The same [CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder') instance the surrounding
[Create\(Action&lt;CompositionBuilder&gt;\)](Compono.Composer.Create.md#Compono.Composer.Create(System.Action_Compono.CompositionBuilder_) 'Compono\.Composer\.Create\(System\.Action\<Compono\.CompositionBuilder\>\)') callback is already configuring\.