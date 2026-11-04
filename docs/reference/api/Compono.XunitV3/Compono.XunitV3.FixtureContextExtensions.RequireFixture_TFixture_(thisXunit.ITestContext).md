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
Prefer reading the fixture from a registration factory \(for example inside
`builder.Register(() => ...)`\) rather than once from a profile constructor or a
`Configure` body\. The factory runs per row at test execution time, so it always
reads the fixture belonging to the test that is actually running\. A profile instance can
outlive a single row \- under the JIT engine `Configure` runs once per attribute
instance and any value it captured is reused for every row that instance composes \- so a
value captured in the constructor or `Configure` can be stale or leak state from
another test\. \(`TestContext.Current` is ambient and available during construction
and `Configure` too; this is about lifecycle correctness, not availability\.\)

This method never waits for fixture initialization. If the fixture has not finished
initializing by the time the test executes, it throws immediately rather than blocking; a
fixture must be declared concretely (`IClassFixture<T>`,
`ICollectionFixture<T>`, or `[assembly: AssemblyFixture(typeof(T))]`) so
xUnit initializes it before the test runs.