#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono').[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

## CompositionBuilder\.Share\<T\>\(\) Method

Declares [T](Compono.CompositionBuilder.Share_T_().md#Compono.CompositionBuilder.Share_T_().T 'Compono\.CompositionBuilder\.Share\<T\>\(\)\.T') graph\-wide shared: within a single composition graph \(one
[Create&lt;T&gt;\(\)](Compono.Composer.Create.md#Compono.Composer.Create_T_() 'Compono\.Composer\.Create\<T\>\(\)') root, one `CreateMany<T>()` item, or one
[CompositionRow](Compono.CompositionRow.md 'Compono\.CompositionRow')\), the first request for [T](Compono.CompositionBuilder.Share_T_().md#Compono.CompositionBuilder.Share_T_().T 'Compono\.CompositionBuilder\.Share\<T\>\(\)\.T') resolves through
the normal pipeline and every subsequent request \- regardless of source: an ordinary
constructor parameter, a nested/transitive dependency, a provider, an exact registration, or an
undecorated `[Compose]` theory parameter \- receives that exact same instance\. No
`[Shared]` attribute is ever required to participate in a type declared shared this way\.
See `docs/adr/0056-composition-builder-share-graph-wide-sharing.md`\.

```csharp
public Compono.CompositionBuilder Share<T>();
```
#### Type parameters

<a name='Compono.CompositionBuilder.Share_T_().T'></a>

`T`

The type to declare graph\-wide shared\.

#### Returns
[CompositionBuilder](Compono.CompositionBuilder.md 'Compono\.CompositionBuilder')

### Remarks
Configuration only \- calling this does not construct [T](Compono.CompositionBuilder.Share_T_().md#Compono.CompositionBuilder.Share_T_().T 'Compono\.CompositionBuilder\.Share\<T\>\(\)\.T'); the first real
request within the graph does\. Calling this more than once for the same
[T](Compono.CompositionBuilder.Share_T_().md#Compono.CompositionBuilder.Share_T_().T 'Compono\.CompositionBuilder\.Share\<T\>\(\)\.T') \(directly, or from more than one profile\) is idempotent, unlike
[Register&lt;T&gt;\(Func&lt;ICompositionContext,T&gt;\)](Compono.CompositionBuilder.Register.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_) 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)')'s strict duplicate\-registration
contract \- there is no "which call wins" question to answer, since every call asserts the
identical fact about the same type\. Orthogonal to [Register&lt;T&gt;\(Func&lt;ICompositionContext,T&gt;\)](Compono.CompositionBuilder.Register.md#Compono.CompositionBuilder.Register_T_(System.Func_Compono.ICompositionContext,T_) 'Compono\.CompositionBuilder\.Register\<T\>\(System\.Func\<Compono\.ICompositionContext,T\>\)'):
which stage produces [T](Compono.CompositionBuilder.Share_T_().md#Compono.CompositionBuilder.Share_T_().T 'Compono\.CompositionBuilder\.Share\<T\>\(\)\.T') is unaffected by this call, and the two may be
combined in either order with no precedence rule between them\.