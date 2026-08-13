#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## ReturnConfigBuilder\<T\> Struct

Public write surface over a single [ReturnConfig&lt;T&gt;](Compono.ReturnConfig_T_.md 'Compono\.ReturnConfig\<T\>') slot \- constructed by
generator\-emitted configuration extensions \(`Configure().Member()`\) in the consumer's own
assembly, per ADR\-0043\. A [ref struct](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ref struct 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/ref struct') because it only ever wraps a
[ref](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ref 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/ref') to a field already living on the generated double instance; it's never
stored, only used inline at the call site\.

```csharp
public readonly ref struct ReturnConfigBuilder<T>
```
#### Type parameters

<a name='Compono.ReturnConfigBuilder_T_.T'></a>

`T`

### Remarks
[Returns\(T\)](Compono.ReturnConfigBuilder_T_.Returns(T).md 'Compono\.ReturnConfigBuilder\<T\>\.Returns\(T\)')/[Throws\(Exception\)](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)') are last\-configuration\-wins: each clears the other's
            state, so configuring a return after an earlier [Throws\(Exception\)](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)') \(or vice versa\) doesn't
            leave stale state behind\. See ADR\-0043 Amendment 7, Finding R\.

| Constructors | |
| :--- | :--- |
| [ReturnConfigBuilder\(ReturnConfig&lt;T&gt;\)](Compono.ReturnConfigBuilder_T_.ReturnConfigBuilder(Compono.ReturnConfig_T_).md 'Compono\.ReturnConfigBuilder\<T\>\.ReturnConfigBuilder\(Compono\.ReturnConfig\<T\>\)') | Wraps [slot](Compono.ReturnConfigBuilder_T_.ReturnConfigBuilder(Compono.ReturnConfig_T_).md#Compono.ReturnConfigBuilder_T_.ReturnConfigBuilder(Compono.ReturnConfig_T_).slot 'Compono\.ReturnConfigBuilder\<T\>\.ReturnConfigBuilder\(Compono\.ReturnConfig\<T\>\)\.slot'), the generated double's own backing field for this member\. |

| Methods | |
| :--- | :--- |
| [Returns\(T\)](Compono.ReturnConfigBuilder_T_.Returns(T).md 'Compono\.ReturnConfigBuilder\<T\>\.Returns\(T\)') | Configures the member to return [value](Compono.ReturnConfigBuilder_T_.Returns(T).md#Compono.ReturnConfigBuilder_T_.Returns(T).value 'Compono\.ReturnConfigBuilder\<T\>\.Returns\(T\)\.value'), clearing any prior [Throws\(Exception\)](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)')\. |
| [Throws\(Exception\)](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)') | Configures the member to throw [exception](Compono.ReturnConfigBuilder_T_.Throws(System.Exception).md#Compono.ReturnConfigBuilder_T_.Throws(System.Exception).exception 'Compono\.ReturnConfigBuilder\<T\>\.Throws\(System\.Exception\)\.exception'), clearing any prior [Returns\(T\)](Compono.ReturnConfigBuilder_T_.Returns(T).md 'Compono\.ReturnConfigBuilder\<T\>\.Returns\(T\)')\. |
