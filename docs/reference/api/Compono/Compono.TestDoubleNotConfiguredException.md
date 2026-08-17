#### [Compono](index.md 'index')
### [Compono](Compono.md 'Compono')

## TestDoubleNotConfiguredException Class

Thrown by a generated test double's configuration\-required member when it's invoked before
`Configure().Member(...).Returns(...)`/`.Throws(...)` configures it \- see ADR\-0045\.

```csharp
public sealed class TestDoubleNotConfiguredException : System.Exception
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception') → TestDoubleNotConfiguredException

| Constructors | |
| :--- | :--- |
| [TestDoubleNotConfiguredException\(string\)](Compono.TestDoubleNotConfiguredException.TestDoubleNotConfiguredException(string).md 'Compono\.TestDoubleNotConfiguredException\.TestDoubleNotConfiguredException\(string\)') | Creates a [TestDoubleNotConfiguredException](Compono.TestDoubleNotConfiguredException.md 'Compono\.TestDoubleNotConfiguredException') with the given message\. |
