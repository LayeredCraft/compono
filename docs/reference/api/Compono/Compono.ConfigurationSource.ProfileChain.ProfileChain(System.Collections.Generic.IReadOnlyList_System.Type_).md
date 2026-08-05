#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ConfigurationSource](Compono.ConfigurationSource.md 'Compono\.ConfigurationSource').[ProfileChain](Compono.ConfigurationSource.ProfileChain.md 'Compono\.ConfigurationSource\.ProfileChain')

## ProfileChain\(IReadOnlyList\<Type\>\) Constructor

Creates a [ProfileChain](Compono.ConfigurationSource.ProfileChain.md 'Compono\.ConfigurationSource\.ProfileChain') source\.

```csharp
public ProfileChain(System.Collections.Generic.IReadOnlyList<System.Type> profiles);
```
#### Parameters

<a name='Compono.ConfigurationSource.ProfileChain.ProfileChain(System.Collections.Generic.IReadOnlyList_System.Type_).profiles'></a>

`profiles` [System\.Collections\.Generic\.IReadOnlyList&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')[System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1 'System\.Collections\.Generic\.IReadOnlyList\`1')

The applied profile types, outermost first \- always at least one; this source represents
"made inside a profile," so an empty chain has no coherent meaning\. Copied into an
immutable snapshot \- mutating a list passed here after this constructor returns has no
effect on [Profiles](Compono.ConfigurationSource.ProfileChain.Profiles.md 'Compono\.ConfigurationSource\.ProfileChain\.Profiles')\.

#### Exceptions

[System\.ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception 'System\.ArgumentException')  
[profiles](Compono.ConfigurationSource.ProfileChain.ProfileChain(System.Collections.Generic.IReadOnlyList_System.Type_).md#Compono.ConfigurationSource.ProfileChain.ProfileChain(System.Collections.Generic.IReadOnlyList_System.Type_).profiles 'Compono\.ConfigurationSource\.ProfileChain\.ProfileChain\(System\.Collections\.Generic\.IReadOnlyList\<System\.Type\>\)\.profiles') is empty\.