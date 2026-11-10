#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot').[ComposeDataRows](Compono.XunitV3.Aot.ComposeDataRows.md 'Compono\.XunitV3\.Aot\.ComposeDataRows')

## ComposeDataRows\.MemberReadFailure\(string, string, Exception\) Method

Builds the failure for a data member that threw while being read\. A
[System\.Reflection\.TargetInvocationException](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.targetinvocationexception 'System\.Reflection\.TargetInvocationException') is unwrapped by one level so the
underlying cause is reported; the cause's [Diagnostic](../Compono/Compono.CompositionException.Diagnostic.md 'Compono\.CompositionException\.Diagnostic') is
carried through when present\. No seed line is appended, because the failure occurs at the
member level before any row's composition starts\. The message is byte\-identical on both engines\.

```csharp
public static Compono.CompositionException MemberReadFailure(string member, string methodDisplayName, System.Exception exception);
```
#### Parameters

<a name='Compono.XunitV3.Aot.ComposeDataRows.MemberReadFailure(string,string,System.Exception).member'></a>

`member` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The member display name\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.MemberReadFailure(string,string,System.Exception).methodDisplayName'></a>

`methodDisplayName` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

The test method display name\.

<a name='Compono.XunitV3.Aot.ComposeDataRows.MemberReadFailure(string,string,System.Exception).exception'></a>

`exception` [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')

The exception thrown while reading the member\.

#### Returns
[CompositionException](../Compono/Compono.CompositionException.md 'Compono\.CompositionException')  
The failure to throw\.