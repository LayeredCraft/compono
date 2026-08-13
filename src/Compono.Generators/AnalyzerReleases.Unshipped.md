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
CMP0012 | Compono.Usage | Error | A collection's element or key type is private/protected and not accessible from the generated top-level collection plan
CMP0013 | Compono.Usage | Error | A [Compose]-family method parameter type is private/protected and not accessible from the generated top-level row-invoker registration (ADR-0041)
CMP0020 | Compono.TestDoubles | Info | A test-double-eligible interface is private/protected and not accessible from the generated top-level double (ADR-0043)
CMP0021 | Compono.TestDoubles | Info | A test-double interface member is an unsupported kind (event, indexer, generic method, ref/out/in parameter, static abstract member)
CMP0022 | Compono.TestDoubles | Info | A test-double interface declares an overloaded member, which a zero-argument configuration extension can't disambiguate
CMP0023 | Compono.TestDoubles | Info | A test-double interface declares its own member named Configure, which would collide with the generated Configure() bridge
CMP0024 | Compono.TestDoubles | Info | A test-double member's generated, always-zero-argument configuration extension collides with an inherited object member
CMP0025 | Compono.TestDoubles | Info | A test-double member has an unsupported return shape (ref-like, by-ref, pointer, function-pointer, or non-nullable reference with no deterministic default)
CMP0026 | Compono.TestDoubles | Info | A test-double member has an unsupported parameter shape (pointer or function-pointer)
CMP0027 | Compono.TestDoubles | Info | A test-double interface declares a set-only property, which is unsupported (no call recording/verification in v1)
