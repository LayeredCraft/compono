#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[ICompositionContext](Compono.ICompositionContext.md 'Compono\.ICompositionContext')

## ICompositionContext\.Resolve Method

| Overloads | |
| :--- | :--- |
| [Resolve&lt;TValue&gt;\(\)](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_() 'Compono\.ICompositionContext\.Resolve\<TValue\>\(\)') | Resolves a value of type [TValue](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_().TValue 'Compono\.ICompositionContext\.Resolve\<TValue\>\(\)\.TValue') from inside a registration or configuration\-rule factory, or a public [TryProvide\(CompositionProviderRequest, ICompositionContext\)](Compono.ICompositionValueProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md 'Compono\.ICompositionValueProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)') invocation \- the hand\-written counterpart to [Resolve&lt;TValue&gt;\(CompositionRequestDescriptor\)](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_(Compono.CompositionRequestDescriptor) 'Compono\.ICompositionContext\.Resolve\<TValue\>\(Compono\.CompositionRequestDescriptor\)'), which only generated code calls\. Only valid while one of those three is actively being invoked\. |
| [Resolve&lt;TValue&gt;\(CompositionRequestDescriptor\)](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_(Compono.CompositionRequestDescriptor) 'Compono\.ICompositionContext\.Resolve\<TValue\>\(Compono\.CompositionRequestDescriptor\)') | Resolves a value of type [TValue](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_(Compono.CompositionRequestDescriptor).TValue 'Compono\.ICompositionContext\.Resolve\<TValue\>\(Compono\.CompositionRequestDescriptor\)\.TValue') for one constructor parameter or required member\. |

<a name='Compono.ICompositionContext.Resolve_TValue_()'></a>

## ICompositionContext\.Resolve\<TValue\>\(\) Method

Resolves a value of type [TValue](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_().TValue 'Compono\.ICompositionContext\.Resolve\<TValue\>\(\)\.TValue') from inside a registration or
configuration\-rule factory, or a public [TryProvide\(CompositionProviderRequest, ICompositionContext\)](Compono.ICompositionValueProvider.TryProvide(Compono.CompositionProviderRequest,Compono.ICompositionContext).md 'Compono\.ICompositionValueProvider\.TryProvide\(Compono\.CompositionProviderRequest, Compono\.ICompositionContext\)')
invocation \- the hand\-written counterpart to
[Resolve&lt;TValue&gt;\(CompositionRequestDescriptor\)](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_(Compono.CompositionRequestDescriptor) 'Compono\.ICompositionContext\.Resolve\<TValue\>\(Compono\.CompositionRequestDescriptor\)'), which only generated code
calls\. Only valid while one of those three is actively being invoked\.

```csharp
TValue Resolve<TValue>();
```
#### Type parameters

<a name='Compono.ICompositionContext.Resolve_TValue_().TValue'></a>

`TValue`

The requested value's type\.

#### Returns
[TValue](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_().TValue 'Compono\.ICompositionContext\.Resolve\<TValue\>\(\)\.TValue')

#### Exceptions

[System\.InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception 'System\.InvalidOperationException')  
No registration/configuration\-rule factory or public provider invocation is currently in
progress\.

[CompositionException](Compono.CompositionException.md 'Compono\.CompositionException')  
No explicit value, shared value, registration, provider, or generated plan could satisfy the
request\.

<a name='Compono.ICompositionContext.Resolve_TValue_(Compono.CompositionRequestDescriptor)'></a>

## ICompositionContext\.Resolve\<TValue\>\(CompositionRequestDescriptor\) Method

Resolves a value of type [TValue](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_(Compono.CompositionRequestDescriptor).TValue 'Compono\.ICompositionContext\.Resolve\<TValue\>\(Compono\.CompositionRequestDescriptor\)\.TValue') for one constructor parameter or
required member\.

```csharp
TValue Resolve<TValue>(in Compono.CompositionRequestDescriptor descriptor);
```
#### Type parameters

<a name='Compono.ICompositionContext.Resolve_TValue_(Compono.CompositionRequestDescriptor).TValue'></a>

`TValue`

The requested value's type\.
#### Parameters

<a name='Compono.ICompositionContext.Resolve_TValue_(Compono.CompositionRequestDescriptor).descriptor'></a>

`descriptor` [CompositionRequestDescriptor](Compono.CompositionRequestDescriptor.md 'Compono\.CompositionRequestDescriptor')

The compact, compile\-time\-constructed request metadata\.

#### Returns
[TValue](Compono.ICompositionContext.Resolve.md#Compono.ICompositionContext.Resolve_TValue_(Compono.CompositionRequestDescriptor).TValue 'Compono\.ICompositionContext\.Resolve\<TValue\>\(Compono\.CompositionRequestDescriptor\)\.TValue')

#### Exceptions

[CompositionException](Compono.CompositionException.md 'Compono\.CompositionException')  
No explicit value, shared value, registration, provider, or generated plan could satisfy the
request\.