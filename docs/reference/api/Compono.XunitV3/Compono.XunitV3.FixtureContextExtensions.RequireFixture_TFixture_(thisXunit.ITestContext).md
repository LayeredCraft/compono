#### [Compono\.XunitV3](index.md 'index')
### [Compono\.XunitV3](Compono.XunitV3.md 'Compono\.XunitV3').[FixtureContextExtensions](Compono.XunitV3.FixtureContextExtensions.md 'Compono\.XunitV3\.FixtureContextExtensions')

## FixtureContextExtensions\.RequireFixture\<TFixture\>\(this ITestContext\) Method

Returns the xUnit fixture of exact type [TFixture](Compono.XunitV3.FixtureContextExtensions.RequireFixture_TFixture_(thisXunit.ITestContext).md#Compono.XunitV3.FixtureContextExtensions.RequireFixture_TFixture_(thisXunit.ITestContext).TFixture 'Compono\.XunitV3\.FixtureContextExtensions\.RequireFixture\<TFixture\>\(this Xunit\.ITestContext\)\.TFixture') that is active for
the currently executing test class, its test collection, or its test assembly\.

```csharp
public static TFixture RequireFixture<TFixture>(this Xunit.ITestContext context)
    where TFixture : class;
```
#### Type parameters

<a name='Compono.XunitV3.FixtureContextExtensions.RequireFixture_TFixture_(thisXunit.ITestContext).TFixture'></a>

`TFixture`

The fixture type to look up\. Lookup matches the exact type only \- a base type or an
implemented interface never matches\.
#### Parameters

<a name='Compono.XunitV3.FixtureContextExtensions.RequireFixture_TFixture_(thisXunit.ITestContext).context'></a>

`context` `Xunit.ITestContext`

#### Returns
[TFixture](Compono.XunitV3.FixtureContextExtensions.RequireFixture_TFixture_(thisXunit.ITestContext).md#Compono.XunitV3.FixtureContextExtensions.RequireFixture_TFixture_(thisXunit.ITestContext).TFixture 'Compono\.XunitV3\.FixtureContextExtensions\.RequireFixture\<TFixture\>\(this Xunit\.ITestContext\)\.TFixture')  
The fixture instance xUnit already initialized\.

#### Exceptions

[System\.ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception 'System\.ArgumentNullException')  
The test context is [null](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/null 'https://docs\.microsoft\.com/en\-us/dotnet/csharp/language\-reference/keywords/null')\.

[System\.InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception 'System\.InvalidOperationException')  
No xUnit test class is currently active, the fixture has not finished initializing, the
test class has no fixture of the exact requested type, or the looked\-up value is not a
[TFixture](Compono.XunitV3.FixtureContextExtensions.RequireFixture_TFixture_(thisXunit.ITestContext).md#Compono.XunitV3.FixtureContextExtensions.RequireFixture_TFixture_(thisXunit.ITestContext).TFixture 'Compono\.XunitV3\.FixtureContextExtensions\.RequireFixture\<TFixture\>\(this Xunit\.ITestContext\)\.TFixture')\. Each case throws its own message describing the fix\.

### Example

```csharp
sealed class WebAppProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => TestContext.Current.RequireFixture<WebApplicationFactory<Program>>());
}
```

### Remarks
Call this from a registration factory \(for example inside
`builder.Register(() => ...)`\), not from a profile constructor or from
`Configure`: xUnit initializes fixtures before a test runs, but the profile may be
constructed before that point, so the value read during construction can be missing\.

This method never waits for fixture initialization. If the fixture has not finished
initializing by the time the test executes, it throws immediately rather than blocking; a
fixture must be declared concretely (`IClassFixture<T>`,
`ICollectionFixture<T>`, or `[assembly: AssemblyFixture(typeof(T))]`) so
xUnit initializes it before the test runs.