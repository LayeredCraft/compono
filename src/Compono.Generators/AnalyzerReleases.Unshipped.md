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
CMP0021 | Compono.TestDoubles | Info | A test-double interface member is an unsupported kind (event, indexer, static abstract member, variable-argument method) - a static abstract member already resolved via a more-derived interface's own concrete implementation (ADR-0046) is not treated as unsupported at all
CMP0022 | Compono.TestDoubles | Info | A test-double member's identity (full signature) is independently declared by two different base interfaces (a diamond collision) - that identity gets no Configure()/Verify() surface, but a real overload gets its own per-overload surface (ADR-0044)
CMP0023 | Compono.TestDoubles | Info | A test-double interface declares its own member named Configure or Verify, which would collide with the generated Configure()/Verify() bridge (ADR-0044 Requirement 3 widened this from Configure-only)
CMP0024 | Compono.TestDoubles | Info | A test-double member's generated configuration extension collides with an inherited object member (ToString/GetHashCode/GetType/Equals)
CMP0025 | Compono.TestDoubles | Info | A test-double member has an unsupported return shape (ref-like, by-ref, pointer, function-pointer, or non-nullable reference with no deterministic default)
CMP0026 | Compono.TestDoubles | Info | A test-double member has an unsupported parameter shape (pointer/function-pointer, always whole-interface; or a ref/out/in parameter with no same-named sibling, also whole-interface - a ref/out/in overload with a sibling instead falls back per-overload, ADR-0044)
CMP0027 | Compono.TestDoubles | Info | A test-double interface declares a set-only property, which is unsupported (no deterministic default to read back through Configure()/Verify())
CMP0028 | Compono.TestDoubles | Info | The same test-double-eligible interface was discovered multiple times with conflicting generic-argument nullability
CMP0029 | Compono.TestDoubles | Info | Two same-named test-double members (e.g. a property and a method, or two methods) both generate a genuinely zero-parameter configuration extension, an unresolvable collision (ADR-0044)
CMP0030 | Compono.TestDoubles | Info | A test-double overload has a ref/out/in parameter but a same-named sibling exists - this overload's own Configure() surface is withheld, but it still dispatches and the rest of the interface is unaffected (ADR-0044, scoped counterpart to CMP0026)
CMP0031 | Compono.TestDoubles | Info | A generic test-double method's return type references its own type parameter in a shape with no constructible fallback body - deeper nesting, multiple type parameters, `allows ref struct`, a value-type-constrained `T?`, a ref-like/self-referencing real parameter, or a derived-name collision; whole-interface rejection, same bucket as CMP0025 (ADR-0044 Requirement 2 / Amendment 13). The narrower, directly-self-referencing shape (`T`, or the sole type argument of `Task<T>`/`Task<T?>`/`ValueTask<T>`/`ValueTask<T?>`, `T` constrained to a reference type for the nullable variants) is supported instead, with independent per-closed-`T` `Configure<T>()`/`Verify<T>()` (ADR-0049)
CMP0032 | Compono.TestDoubles | Info | An interface has one or more members that require explicit Configure().Member(...).Returns(...)/.Throws(...) configuration before use - one diagnostic per interface (a count), not one per member (ADR-0045)
CMP0033 | Compono.Usage | Error | Two different UseConstructor(...) selections for the same type exist anywhere in the compilation - only one construction path is allowed per type per compilation (ADR-0052)
CMP0034 | Compono.Usage | Error | An explicit UseConstructor(...) selection's parameter-type list doesn't match any accessible constructor of the target type (ADR-0052)
