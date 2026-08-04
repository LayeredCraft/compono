#### [Compono\.Bogus](index.md 'index')
### [Compono](Compono.md 'Compono').[BogusOptions](Compono.BogusOptions.md 'Compono\.BogusOptions')

## BogusOptions\.Locale Property

The Bogus locale used by the package\-wide member\-name convention provider
\([BogusMemberNameProvider](Compono.BogusMemberNameProvider.md 'Compono\.BogusMemberNameProvider')\) only\. `UseBogus<T>()` is independent of this
option and does not read it \- it defaults to `"en"` on its own, or takes an explicit
`locale` parameter\. Defaults to Bogus's own default \(`"en"`\)\.

```csharp
public string Locale { get; set; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')