; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
CMP0001 | Compono.Usage | Error | Ambiguous construction path - see docs/adr/0002-constructor-selection-algorithm.md
CMP0002 | Compono.Usage | Error | No accessible constructor found
CMP0003 | Compono.Usage | Error | Type is abstract, or a delegate type, and cannot be constructed directly
CMP0004 | Compono.Usage | Error | Constructor parameter passed by ref/out, or of a ref-like/pointer type, cannot be composed
CMP0005 | Compono.Usage | Error | Type argument contains an unresolved type parameter (not a closed type)
CMP0006 | Compono.Usage | Error | Type argument isn't a named type (array, pointer, or other unsupported shape)
CMP0007 | Compono.Usage | Error | Required member is of an unsupported type shape (ref-like or pointer), which cannot be a generic type argument
CMP0008 | Compono.Usage | Error | Assembly-level [Composable] is missing its typeof(...) target type argument
CMP0009 | Compono.Usage | Error | Requested type is a ref struct (ref-like type), which cannot be a type argument for ICompositionPlan<T>/PlanCache<T>
CMP0010 | Compono.Usage | Error | The same type was discovered multiple times with conflicting composition metadata (e.g. differing nullable-reference annotations across call sites)
CMP0011 | Compono.Usage | Error | The same closed collection type was discovered multiple times with conflicting element/key nullability
