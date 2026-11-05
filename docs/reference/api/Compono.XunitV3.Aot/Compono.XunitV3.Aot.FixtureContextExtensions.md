#### [Compono\.XunitV3\.Aot](index.md 'index')
### [Compono\.XunitV3\.Aot](Compono.XunitV3.Aot.md 'Compono\.XunitV3\.Aot')

## FixtureContextExtensions Class

Adds `Xunit.ITestContext`\-based fixture access for composition profiles\. See
`docs/adr/0069-xunitv3-require-fixture-accessor.md`\.

```csharp
public static class FixtureContextExtensions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → FixtureContextExtensions

### Remarks
Named `FixtureContextExtensions` rather than `TestContextExtensions` so it can't be
confused with xUnit's own `Xunit.TestContextExtensions` \(which ships
`GetFixture<TFixture>()` in every supported xUnit version\)\.

| Methods | |
| :--- | :--- |
| [RequireFixture&lt;TFixture&gt;\(this ITestContext\)](Compono.XunitV3.Aot.FixtureContextExtensions.RequireFixture_TFixture_(thisXunit.ITestContext).md 'Compono\.XunitV3\.Aot\.FixtureContextExtensions\.RequireFixture\<TFixture\>\(this Xunit\.ITestContext\)') | Returns the xUnit fixture of exact type [TFixture](Compono.XunitV3.Aot.FixtureContextExtensions.RequireFixture_TFixture_(thisXunit.ITestContext).md#Compono.XunitV3.Aot.FixtureContextExtensions.RequireFixture_TFixture_(thisXunit.ITestContext).TFixture 'Compono\.XunitV3\.Aot\.FixtureContextExtensions\.RequireFixture\<TFixture\>\(this Xunit\.ITestContext\)\.TFixture') that is active for the currently executing test class, its test collection, or its test assembly\. |
