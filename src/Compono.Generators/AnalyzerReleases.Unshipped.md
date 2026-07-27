; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
CMP0001 | Compono.Usage | Error | Ambiguous construction path - see docs/adr/0002-constructor-selection-algorithm.md
CMP0002 | Compono.Usage | Error | No accessible constructor found
CMP0003 | Compono.Usage | Error | Type is abstract and cannot be constructed directly
