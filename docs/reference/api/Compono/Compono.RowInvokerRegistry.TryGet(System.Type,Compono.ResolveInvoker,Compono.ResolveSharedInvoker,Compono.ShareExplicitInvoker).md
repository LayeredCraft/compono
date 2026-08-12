#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[RowInvokerRegistry](Compono.RowInvokerRegistry.md 'Compono\.RowInvokerRegistry')

## RowInvokerRegistry\.TryGet\(Type, ResolveInvoker, ResolveSharedInvoker, ShareExplicitInvoker\) Method

Looks up the three dispatch delegates registered for [type](Compono.RowInvokerRegistry.TryGet(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).md#Compono.RowInvokerRegistry.TryGet(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).type 'Compono\.RowInvokerRegistry\.TryGet\(System\.Type, Compono\.ResolveInvoker, Compono\.ResolveSharedInvoker, Compono\.ShareExplicitInvoker\)\.type'), or
[false](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/builtin\-types/bool') if none has been registered \- either because [type](Compono.RowInvokerRegistry.TryGet(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).md#Compono.RowInvokerRegistry.TryGet(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).type 'Compono\.RowInvokerRegistry\.TryGet\(System\.Type, Compono\.ResolveInvoker, Compono\.ResolveSharedInvoker, Compono\.ShareExplicitInvoker\)\.type') was
never discovered at a dispatch\-eligible `[Compose]`\-family parameter, or because the
consuming assembly's module initializers haven't run yet\.

```csharp
public static bool TryGet(System.Type type, out Compono.ResolveInvoker resolve, out Compono.ResolveSharedInvoker resolveShared, out Compono.ShareExplicitInvoker shareExplicit);
```
#### Parameters

<a name='Compono.RowInvokerRegistry.TryGet(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).type'></a>

`type` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

<a name='Compono.RowInvokerRegistry.TryGet(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).resolve'></a>

`resolve` [ResolveInvoker\(CompositionRow, CompositionRequestDescriptor\)](Compono.ResolveInvoker(Compono.CompositionRow,Compono.CompositionRequestDescriptor).md 'Compono\.ResolveInvoker\(Compono\.CompositionRow, Compono\.CompositionRequestDescriptor\)')

<a name='Compono.RowInvokerRegistry.TryGet(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).resolveShared'></a>

`resolveShared` [ResolveSharedInvoker\(CompositionRow, CompositionRequestDescriptor\)](Compono.ResolveSharedInvoker(Compono.CompositionRow,Compono.CompositionRequestDescriptor).md 'Compono\.ResolveSharedInvoker\(Compono\.CompositionRow, Compono\.CompositionRequestDescriptor\)')

<a name='Compono.RowInvokerRegistry.TryGet(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).shareExplicit'></a>

`shareExplicit` [ShareExplicitInvoker\(CompositionRow, CompositionRequestDescriptor, object\)](Compono.ShareExplicitInvoker(Compono.CompositionRow,Compono.CompositionRequestDescriptor,object).md 'Compono\.ShareExplicitInvoker\(Compono\.CompositionRow, Compono\.CompositionRequestDescriptor, object\)')

#### Returns
[System\.Boolean](https://learn.microsoft.com/en-us/dotnet/api/system.boolean 'System\.Boolean')