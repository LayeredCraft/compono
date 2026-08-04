#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

## CompositionBuilder\.WithSeed\(int\) Method

Sets this composer's explicit root seed \- the same seed produces the same composed output
\(for a given `Compono` package version\) across every [Create&lt;T&gt;\(\)](Compono.Composer.Create.md#Compono.Composer.Create_T_() 'Compono\.Composer\.Create\<T\>\(\)')/
[CreateMany&lt;T&gt;\(int\)](Compono.Composer.CreateMany_T_(int).md 'Compono\.Composer\.CreateMany\<T\>\(int\)') call this composer ever serves\. Without this call, each
root composition operation generates its own seed\.

```csharp
public Compono.CompositionBuilder WithSeed(int seed);
```
#### Parameters

<a name='Compono.CompositionBuilder.WithSeed(int).seed'></a>

`seed` [System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

The explicit root seed\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

### Remarks
Calling this more than once \(directly, or once directly and once from a profile, or from two
different profiles\) is a configuration conflict, not last\-write\-wins \- surfaced as a
[CompositionConfigurationException](Compono.CompositionConfigurationException.md 'Compono\.CompositionConfigurationException') once [Create\(Action&lt;CompositionBuilder&gt;\)](Compono.Composer.Create.md#Compono.Composer.Create(System.Action_Compono.CompositionBuilder_) 'Compono\.Composer\.Create\(System\.Action\<Compono\.CompositionBuilder\>\)')'s
validation runs, not immediately\. See
`docs/adr/0017-immutable-composer-configuration-and-builder-model.md`'s Amendment for why:
two different seeds configured for the same composer has no coherent "effective" value the way
a typical options\-builder's last\-write\-wins convention would assume\.