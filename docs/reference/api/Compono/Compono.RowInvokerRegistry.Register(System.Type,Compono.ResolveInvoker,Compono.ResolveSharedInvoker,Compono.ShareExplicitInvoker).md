#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[RowInvokerRegistry](Compono.RowInvokerRegistry.md 'Compono\.RowInvokerRegistry')

## RowInvokerRegistry\.Register\(Type, ResolveInvoker, ResolveSharedInvoker, ShareExplicitInvoker\) Method

Idempotently registers the three dispatch delegates for [type](Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).md#Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).type 'Compono\.RowInvokerRegistry\.Register\(System\.Type, Compono\.ResolveInvoker, Compono\.ResolveSharedInvoker, Compono\.ShareExplicitInvoker\)\.type') \- a second
registration for a [type](Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).md#Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).type 'Compono\.RowInvokerRegistry\.Register\(System\.Type, Compono\.ResolveInvoker, Compono\.ResolveSharedInvoker, Compono\.ShareExplicitInvoker\)\.type') already present \(e\.g\. from another assembly's own
generated module initializer\) is a no\-op, never a throw or an overwrite\.

```csharp
public static void Register(System.Type type, Compono.ResolveInvoker resolve, Compono.ResolveSharedInvoker resolveShared, Compono.ShareExplicitInvoker shareExplicit);
```
#### Parameters

<a name='Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).type'></a>

`type` [System\.Type](https://learn.microsoft.com/en-us/dotnet/api/system.type 'System\.Type')

<a name='Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).resolve'></a>

`resolve` [ResolveInvoker\(CompositionRow, CompositionRequestDescriptor\)](Compono.ResolveInvoker(Compono.CompositionRow,Compono.CompositionRequestDescriptor).md 'Compono\.ResolveInvoker\(Compono\.CompositionRow, Compono\.CompositionRequestDescriptor\)')

<a name='Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).resolveShared'></a>

`resolveShared` [ResolveSharedInvoker\(CompositionRow, CompositionRequestDescriptor\)](Compono.ResolveSharedInvoker(Compono.CompositionRow,Compono.CompositionRequestDescriptor).md 'Compono\.ResolveSharedInvoker\(Compono\.CompositionRow, Compono\.CompositionRequestDescriptor\)')

<a name='Compono.RowInvokerRegistry.Register(System.Type,Compono.ResolveInvoker,Compono.ResolveSharedInvoker,Compono.ShareExplicitInvoker).shareExplicit'></a>

`shareExplicit` [ShareExplicitInvoker\(CompositionRow, CompositionRequestDescriptor, object\)](Compono.ShareExplicitInvoker(Compono.CompositionRow,Compono.CompositionRequestDescriptor,object).md 'Compono\.ShareExplicitInvoker\(Compono\.CompositionRow, Compono\.CompositionRequestDescriptor, object\)')