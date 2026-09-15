using Microsoft.CodeAnalysis;

namespace Compono.Generators.Diagnostics;

// CMP000x: Constructor selection diagnostics (docs/adr/0002-constructor-selection-algorithm.md)
internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor AmbiguousConstructor = new(
        "CMP0001",
        "Ambiguous construction path",
        "{0} has {1} accessible constructors and no way to disambiguate them",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoAccessibleConstructor = new(
        "CMP0002",
        "No accessible constructor",
        "{0} has no accessible instance constructor Compono can invoke",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TypeNotConstructible = new(
        "CMP0003",
        "Type cannot be constructed",
        "{0} is {1} and cannot be constructed directly",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedParameterKind = new(
        "CMP0004",
        "Unsupported constructor parameter kind",
        "{0} takes parameter '{1}' {2}, which Compono cannot compose a value for",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor OpenGenericTypeArgument = new(
        "CMP0005",
        "Type argument is not closed",
        "'{0}' is not a closed type - Compono requires a fully constructed type, " +
        "not one containing an unresolved type parameter from an enclosing generic method or type",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedTypeArgumentShape = new(
        "CMP0006",
        "Unsupported type argument shape",
        "'{0}' is not a type Compono can compose - Compono requires a named type " +
        "(a class, struct, record, or interface), not an array, pointer, or other type shape",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedRequiredMemberKind = new(
        "CMP0007",
        "Unsupported required member kind",
        "{0} has required member '{1}' {2}, which Compono cannot compose a value for",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AssemblyComposableMissingType = new(
        "CMP0008",
        "Assembly-level [Composable] has no target type",
        "Assembly-level [Composable] requires a type argument identifying the type to compose - " +
        "use [assembly: Composable(typeof(SomeType))]",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RefLikeTypeArgument = new(
        "CMP0009",
        "Type argument is a ref struct",
        "'{0}' is a ref struct (ref-like type), which cannot be used as a type argument for " +
        "Compono's generated ICompositionPlan<T>/PlanCache<T>",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConflictingCompositionMetadata = new(
        "CMP0010",
        "Conflicting composition metadata across discoveries",
        "'{0}' was discovered multiple times with different composition metadata (for example, " +
        "Create<Box<string>>() and Create<Box<string?>>() in the same compilation) - Compono " +
        "generates exactly one plan per type and can't guarantee it correctly reflects every " +
        "discovery. Request this type with consistent nullability everywhere it's composed.",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConflictingCollectionMetadata = new(
        "CMP0011",
        "Conflicting collection metadata across discoveries",
        "'{0}' was discovered multiple times with different element/key nullability (for example, " +
        "a List<string> member and a List<string?> member reaching the same closed collection type " +
        "in the same compilation) - Compono generates exactly one collection plan per closed type " +
        "and can't guarantee it correctly reflects every discovery. Request this collection type " +
        "with consistent element/key nullability everywhere it's composed.",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InaccessibleCollectionElementType = new(
        "CMP0012",
        "Collection element or key type is not accessible",
        "'{0}' cannot be an element or key type of a generated collection plan for '{1}' - every " +
        "generated collection plan is emitted as a top-level type outside any containing type, so a " +
        "private or protected element/key type can never be referenced from it, even from a call " +
        "site that could otherwise see it. Use a collection of an accessible type, or widen '{0}''s " +
        "accessibility.",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InaccessibleRowInvokerParameterType = new(
        "CMP0013",
        "Compose-attributed parameter type is not accessible",
        "'{0}' cannot be registered for row-binding dispatch on '{1}' - the generated dispatch " +
        "registration is emitted as a top-level type outside any containing type, so a private or " +
        "protected parameter type can never be referenced from it, even from a test method that could " +
        "otherwise see it. Use a parameter of an accessible type, or widen '{0}''s accessibility.",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // CMP002x: Generated-test-double diagnostics (ADR-0043) - a leaf interface that hits any of
    // these still defers to the unchanged runtime-provider path (context.Resolve<T>()), never a hard
    // generator error; the opt-in only ever adds a double, it never removes the fallback.

    public static readonly DiagnosticDescriptor InaccessibleTestDoubleInterface = new(
        "CMP0020",
        "Test-double interface is not accessible",
        "'{0}' cannot have a generated test double - the double is emitted as a top-level type " +
        "outside any containing type, so a private or protected interface can never be implemented " +
        "by it, even from a call site that could otherwise see it. This leaf falls back to the " +
        "ordinary runtime-provider path.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedTestDoubleMemberKind = new(
        "CMP0021",
        "Unsupported test-double member kind",
        "'{0}' declares member '{1}' {2}, which Compono cannot generate a test double for. This " +
        "leaf falls back to the ordinary runtime-provider path.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor OverloadedTestDoubleMember = new(
        "CMP0022",
        "Overloaded test-double member",
        "'{0}' declares member '{1}{2}', whose signature is also independently declared by another " +
        "base interface (a diamond collision) - Compono can't tell the two identities apart, so " +
        "neither gets a Configure() surface. Every other member of '{0}', including any " +
        "other overload of '{1}', is unaffected.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TestDoubleConfigureMemberCollision = new(
        "CMP0023",
        "Test-double interface member collides with a generated Configure()/Verify()/ReceivedCalls()/ClearCalls() bridge",
        "'{0}' declares its own member named '{1}', which would silently shadow the generated " +
        "{1}() extension the double's configuration/verification surface depends on. This leaf falls " +
        "back to the ordinary runtime-provider path.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TestDoubleObjectMemberCollision = new(
        "CMP0024",
        "Test-double member collides with an inherited object member",
        "'{0}' declares member '{1}', whose generated configuration extension collides with an " +
        "inherited 'object.{1}' member of the same arity. This leaf falls back to the ordinary " +
        "runtime-provider path.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedTestDoubleReturnShape = new(
        "CMP0025",
        "Unsupported test-double return shape",
        "'{0}' declares member '{1}' returning {2}, which Compono cannot generate a test double for. " +
        "This leaf falls back to the ordinary runtime-provider path.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedTestDoubleParameterShape = new(
        "CMP0026",
        "Unsupported test-double parameter shape",
        "'{0}' declares member '{1}' with parameter '{2}' {3}, which Compono cannot generate a test " +
        "double for. This leaf falls back to the ordinary runtime-provider path.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SetOnlyTestDoubleProperty = new(
        "CMP0027",
        "Set-only test-double property is unsupported",
        "'{0}' declares set-only property '{1}' - with no call recording or verification in v1, " +
        "nothing could ever observe a value written through it. This leaf falls back to the " +
        "ordinary runtime-provider path.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConflictingTestDoubleMetadata = new(
        "CMP0028",
        "Conflicting test-double metadata across discoveries",
        "'{0}' was discovered multiple times with different generic-argument nullability (for example, " +
        "a member typed IProvider<string> and one typed IProvider<string?> in the same compilation) - " +
        "Compono generates exactly one test double per interface and can't guarantee it correctly " +
        "reflects every discovery. Request this interface with consistent nullability everywhere it's " +
        "composed, or disable ComponoGeneratedTestDoubles for this leaf.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ZeroArgumentTestDoubleExtensionCollision = new(
        "CMP0029",
        "Test-double members generate colliding zero-argument extensions",
        "'{0}' declares member '{1}', whose generated configuration extension has no parameters to " +
        "disambiguate it from another same-named member's own generated extension - Compono can't " +
        "tell them apart, so none of them get a Configure() surface. Every other member of " +
        "'{0}' is unaffected.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    // A distinct descriptor from UnsupportedTestDoubleParameterShape (CMP0026, whole-interface
    // rejection) rather than reusing its message with a different Info-vs-blocking meaning - that
    // shared descriptor unconditionally says "which Compono cannot generate a test double for" and
    // "this leaf falls back to the ordinary runtime-provider path", both false for this scoped case:
    // the double still generates, and every other member (including this one's own dispatch body)
    // is unaffected. Codex review, PR #88.
    public static readonly DiagnosticDescriptor OverloadScopedUnsupportedParameterShape = new(
        "CMP0030",
        "Overload-scoped unsupported test-double parameter shape",
        "'{0}' declares member '{1}' with parameter '{2}' as a ref/out/in parameter. This overload " +
        "has no Configure() surface, but it still dispatches via a deterministic default - its " +
        "sibling overloads, and the rest of the interface, are unaffected.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    // ADR-0044 Requirement 2 / Amendment 13: a generic method whose return type references its own
    // type parameter has no constructible body at any granularity (no concrete slot type, no
    // deterministic default) - the same no-constructible-body bucket a non-nullable-no-default return
    // (CMP0025) already occupies, so it gets the same whole-interface-rejection disposition, just
    // under its own diagnostic code rather than reusing CMP0025's "returning {2}" message shape (a
    // generic-return dependency isn't itself a "shape", it's a relationship to the method's own type
    // parameters).
    public static readonly DiagnosticDescriptor UnsupportedTestDoubleGenericReturnShape = new(
        "CMP0031",
        "Unsupported test-double generic return shape",
        "'{0}' declares generic method '{1}' whose return type references its own type parameter, " +
        "which Compono cannot generate a deterministic default for. This leaf falls back to the " +
        "ordinary runtime-provider path.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    // ADR-0045: a member with a non-nullable-reference return and no deterministic default no
    // longer rejects its whole interface (CMP0025) provided it would otherwise have a real
    // Configure()/Verify() surface - it generates as configuration-required instead, throwing
    // Compono.TestDoubleNotConfiguredException if invoked before Returns(...)/Throws(...). This
    // diagnostic is interface-scoped (one per interface, a count), not member-scoped (Amendment 1)
    // - the exact member identity is already reported precisely by the thrown exception at the
    // point an unconfigured member is actually invoked, so this diagnostic doesn't need to
    // enumerate members by name to stay useful, and stays quiet on a large real-world interface
    // like IAmazonS3 rather than emitting one diagnostic per member.
    public static readonly DiagnosticDescriptor TestDoubleMemberRequiresConfiguration = new(
        "CMP0032",
        "Test-double member(s) require explicit configuration",
        "'{0}' has {1} member(s) that require explicit configuration before use - each throws " +
        "Compono.TestDoubleNotConfiguredException if invoked before Configure().Member(...).Returns(...) " +
        "or .Throws(...). This does not block generation; every other member is unaffected.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    // ADR-0052 (Part B, explicit constructor selection).
    public static readonly DiagnosticDescriptor ConflictingConstructorSelection = new(
        "CMP0033",
        "Conflicting explicit constructor selection",
        "'{0}' has more than one explicit UseConstructor(...) selection in this compilation " +
        "({1} and {2}) - only one construction path is allowed per type per compilation " +
        "(ADR-0052)",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidConstructorSelection = new(
        "CMP0034",
        "Invalid explicit constructor selection",
        "'{0}' has no accessible constructor matching the requested parameter types ({1})",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TestDoubleDimHelperNameCollision = new(
        "CMP0035",
        "Test-double DIM fallback helper name collides with another member",
        "'{0}' declares default-interface member '{1}', whose generated fallback dispatch-helper " +
        "field/class name collides with another member of the same interface. '{1}' falls back to " +
        "the ordinary computed-default behavior instead of its real default-interface-member body.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TestDoubleDimHelperUnresolvedStaticAbstractMember = new(
        "CMP0036",
        "Test-double DIM fallback helper cannot satisfy an unresolved static abstract member",
        "'{0}' declares default-interface member '{1}', whose declaring interface inherits a static " +
        "abstract member that only a more-derived interface in this double's own closure resolves - " +
        "the generated dispatch helper implements only '{1}''s declaring interface and can't supply " +
        "that static member itself. '{1}' falls back to the ordinary computed-default behavior " +
        "instead of its real default-interface-member body.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TestDoubleUnrecognizedExplicitInterfaceReimplementation = new(
        "CMP0037",
        "Test-double member resolved only by an unrecognized explicit interface reimplementation",
        "'{0}' inherits member '{1}', which is resolved elsewhere in the closure via an explicit " +
        "interface reimplementation - a shape Compono does not yet recognize during effective-" +
        "declaration resolution. '{1}''s unconfigured fallback may not match the interface's own " +
        "resolved behavior.",
        "Compono.TestDoubles",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LoggingRuntimeSymbolsUnavailable = new(
        "CMP0038",
        "ComponoGeneratedLogging is enabled but Compono.Logging's runtime types are unavailable",
        "ComponoGeneratedLogging is enabled (explicitly, or by Compono.Logging's own package default), " +
        "but Compono.Logging.LoggingFactoryRegistry, Compono.Logging.CapturingLogger<T>, and/or " +
        "Compono.Logging.LoggingOptions could not be resolved in this compilation. Is Compono.Logging " +
        "referenced? No logging activation is generated while this condition holds.",
        "Compono.Logging",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InaccessibleLoggingCategoryType = new(
        "CMP0039",
        "ILogger<T> category type is not accessible",
        "'{0}' cannot have a generated Compono.Logging activation - the activation is emitted as a " +
        "top-level type outside any containing type, so a private or protected category type can " +
        "never be referenced from it, even from a call site that could otherwise see it. Composing " +
        "ILogger<{0}> still compiles; requesting it at runtime falls back to LoggingProvider's own " +
        "missing-activation diagnostic instead of a generated capturing logger.",
        "Compono.Logging",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    // CMP004x: Compono.XunitV3.Aot diagnostics (ADR-0066/PLAN-0066) - unlike the other four
    // [Compose]-family attribute families (Compono.XunitV3/TUnit/MSTest/NUnit), an unsupported
    // signature shape here has no runtime BindingPlan.ValidateSignature-equivalent to fall back on:
    // Compono.XunitV3.Aot.ComposeAttribute is a marker only (RESEARCH-0032 §2/§9), never invoked at
    // runtime under xUnit's AOT pipeline, so a silently-skipped registration would leave the test
    // simply never discovered instead of failing with a clear message - a compile-time error is the
    // only place this can be caught at all.

    public static readonly DiagnosticDescriptor UnsupportedAotComposeMethodSignature = new(
        "CMP0040",
        "Compono.XunitV3.Aot-attributed test method has an unsupported signature",
        "'{0}' cannot be registered for Native AOT theory-data generation: {1}. Compono.XunitV3.Aot's " +
        "Phase 1 supports only ordinary, non-generic parameters (no generic test methods, no " +
        "ref/out/in/params parameters) - see docs/packages/compono-xunitv3-aot.md.",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // CMP0041-CMP0043: Compono.XunitV3.Aot.ComposeAttribute<TProfile, TConfig> compile-time shape/
    // argument validation (ADR-0067/PLAN-0067) - the compile-time counterpart to
    // Compono.XunitV3.Binding.ConfigProfileBinder's identical runtime checks (ADR-0036). Performed
    // here, not at runtime, for the same reason CMP0040 exists: this attribute family has no
    // DataAttribute.GetData runtime fallback to report through.

    // PR #140 Codex review round 12/13: both messages below originally said "it has {3}" meaning the
    // raw public-constructor count - accurate for the first gate (ambiguous/zero/non-named-type count),
    // but actively misleading for the second gate (round 3's by-ref exclusion, round 6's dynamic
    // exclusion, round 8's prohibited-AOT-attribute exclusion): a TConfig/TProfile with exactly one
    // public constructor that happens to have a ref/out/in parameter reports "it has 0", when the type
    // in fact has 1 public constructor - it just isn't *usable* for AOT's direct-construction codegen.
    // Round 12's first attempt reworded the message to always say "usable", reasoning the raw and usable
    // counts are "identical when the count itself is the problem" - true for the *single*-constructor
    // case (0 or 1), but round 13 caught that this breaks down for the *ambiguous* case: a TConfig with
    // two public constructors, one ordinary and one disqualified by a by-ref/dynamic parameter, hits the
    // first gate with the RAW count (2), which the "usable" wording then falsely claims are both usable
    // when only one is. Fixed properly this time: `{4}` carries the noun phrase itself, so each gate
    // supplies wording that actually matches what `{3}` counts - "public constructor(s)" for the first
    // (raw-ambiguity) gate, "usable public constructor(s)" plus the disqualifying-shapes explanation for
    // the second (usability) gate. Round 14 caught that round 13's own fix, in turn, surfaced a
    // pre-existing "abstract TConfig synthesizes as 0 constructors" shortcut (rounds 1/4) as an outright
    // false claim, once the raw-ambiguity gate started explicitly saying "public constructor(s)" - an
    // abstract TConfig that actually declares one or more public constructors (just can't be `new`'d
    // directly) now reports its TRUE declared count via a separately-computed `rawConfigConstructors`,
    // while the usability gate itself still forces abstract types to fail regardless of that count.
    public static readonly DiagnosticDescriptor InvalidProfileConfigConstructorShape = new(
        "CMP0041",
        "Profile configuration type does not have exactly one usable public constructor",
        "'{0}' is used as the TConfig type argument of [Compose<{1}, {0}>] on '{2}', but must have " +
        "exactly one usable public constructor to be used as profile configuration - it has {3} {4}",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidProfileConstructorShape = new(
        "CMP0042",
        "Profile type does not have exactly one usable public constructor accepting its configuration type",
        "'{0}' is used as the TProfile type argument of [Compose<{0}, {1}>] on '{2}', but must have " +
        "exactly one usable public constructor accepting a single '{1}' parameter - it has {3} usable " +
        "public constructor(s) (a constructor with a ref/out/in parameter, or one marked " +
        "[RequiresDynamicCode]/[RequiresUnreferencedCode]/[RequiresAssemblyFiles] does not count as " +
        "usable)",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ProfileConfigArgumentMismatch = new(
        "CMP0043",
        "Profile configuration argument does not match the configuration type's constructor",
        "[Compose<{0}, {1}>] on '{2}' supplies a profile configuration argument that does not match " +
        "'{1}''s constructor: {3}",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // CMP0044-CMP0046: further compile-time shape validation for Compono.XunitV3.Aot's profile forms
    // (ADR-0067/PLAN-0067), added after PR #140's Codex review found real generator-crash/
    // uncompilable-generated-code gaps in the initial CMP0041-CMP0043 pass - same "no runtime
    // fallback, so this has to be a compile-time diagnostic" reasoning as the rest of this series.

    public static readonly DiagnosticDescriptor InaccessibleProfileSymbol = new(
        "CMP0044",
        "A type referenced by [Compose<TProfile>]/[Compose<TProfile, TConfig>] is not accessible from the generated registration",
        "'{0}' is referenced by [Compose<...>] on '{1}' ({2}), but is not accessible from " +
        "Compono.Generators' generated top-level registration - referencing it there would fail with " +
        "CS0122. Make '{0}' at least internal (with InternalsVisibleTo if it lives in another " +
        "assembly), or public.",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MultipleAotComposeAttributes = new(
        "CMP0045",
        "More than one Compono.XunitV3.Aot Compose-family attribute on one test method",
        "More than one [Compose]/[Compose<TProfile>]/[Compose<TProfile, TConfig>] attribute on '{0}' " +
        "- only one Compose-family attribute per test method is allowed. Unlike Compono.XunitV3, " +
        "these three attribute types share no common base class here, so a second one on the same " +
        "method would otherwise each independently register their own theory-data factory under the " +
        "same generated hint name and crash the generator instead of producing a diagnostic.",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ProfileConstructorRequiredMembersUnsatisfied = new(
        "CMP0046",
        "Selected TConfig/TProfile constructor does not satisfy the type's required members",
        "'{0}''s selected constructor does not satisfy required member '{1}' (used by [Compose<...>] " +
        "on '{2}') - Compono.Generators constructs '{0}' via a direct constructor call, which requires " +
        "either no required members, or the constructor to carry " +
        "[System.Diagnostics.CodeAnalysis.SetsRequiredMembers], or this would fail with CS9035 in the " +
        "generated registration",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // PR #140 Codex review round 6: a selected TConfig/TProfile constructor marked
    // [Obsolete(error: true)] passes every shape/accessibility/required-members check above but
    // produces an uncompilable `new T(...)` call (CS0619) in the generated registration - caught here
    // as its own diagnostic rather than folded into CMP0041/CMP0042's "0 usable constructors" count,
    // since (unlike ref/out/in or dynamic) such a constructor is otherwise a completely normal,
    // JIT-reflectable constructor - this is purely a "the generated call site can't use it" problem,
    // not a shape problem. PR #140 Codex review round 7: generalized to also catch
    // [System.Diagnostics.CodeAnalysis.Experimental("...")] (confirmed by direct compile probe to be a
    // second, independent standard attribute that makes any *use* of the marked constructor a compiler
    // error - always severity Error, with a diagnostic ID the attribute itself supplies) - same
    // underlying problem class as [Obsolete(error: true)], so it's reported through this same CMP0047
    // diagnostic rather than a new one, with the message naming which attribute was actually found.
    public static readonly DiagnosticDescriptor ProhibitedProfileConstructor = new(
        "CMP0047",
        "Selected TConfig/TProfile constructor cannot be used at its generated call site",
        "'{0}''s selected constructor is marked {2}, which makes any use of it a compiler error (used " +
        "by [Compose<...>] on '{1}') - Compono.Generators constructs '{0}' via a direct `new {0}(...)` " +
        "call in the generated registration, which would fail to compile",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // PR #140 Codex review round 8: round 5's fix for the overload-hijack finding (casting every
    // rendered argument to the selected constructor's own declared parameter type) does not defend
    // against [System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute] - confirmed by
    // direct probe that an accessible sibling constructor with a higher priority value still wins
    // ordinary overload resolution even when the call site's argument is explicitly cast to the selected
    // constructor's own parameter type, because C#'s overload-resolution-priority pruning happens
    // *before* applicability/betterness comparison, not after. There's no codegen shape that can defeat
    // this (unlike round 5's fix, which a cast *could* defeat) - the only safe response is to refuse to
    // construct this way at all.
    public static readonly DiagnosticDescriptor ProfileConstructorSupersededByPriority = new(
        "CMP0048",
        "An accessible sibling constructor could supersede the selected TConfig/TProfile constructor via OverloadResolutionPriority",
        "'{0}''s selected constructor could be silently superseded at its generated call site by '{2}', " +
        "which is accessible from the generated registration and marked with a higher " +
        "[OverloadResolutionPriority] (used by [Compose<...>] on '{1}') - Compono.Generators constructs " +
        "'{0}' via a direct `new {0}(...)` call, and overload-resolution-priority pruning would select " +
        "the higher-priority constructor regardless of argument casts, unlike the exact constructor JIT " +
        "mode's ConstructorInfo.Invoke would call",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // PR #140 Codex review round 9: [Compose<TProfile>]'s `new()` constraint guarantees a public
    // parameterless TProfile constructor exists, but not that it's AOT/trim-safe - the generated
    // registration's `AddProfile<TProfile>()` call closes Compono core's own generic `new T()`
    // construction over the real TProfile at that call site, so a constructor marked
    // [RequiresDynamicCode]/[RequiresUnreferencedCode]/[RequiresAssemblyFiles] surfaces its warning
    // there, confirmed by direct probe - the same underlying hazard `HasProhibitedAotAttribute` already
    // excludes for the two-type-parameter form's TConfig/TProfile constructors, just reached through a
    // different generated code shape (a closed generic call, not a direct `new T(...)`), so it needs its
    // own diagnostic rather than folding into an existing "0 usable constructors" count this form has
    // no counterpart of.
    public static readonly DiagnosticDescriptor ProfileConstructorRequiresAotUnsafeFeature = new(
        "CMP0049",
        "TProfile's parameterless constructor is marked with an AOT/trim-unsafe attribute",
        "'{0}''s public parameterless constructor is marked [RequiresDynamicCode]/" +
        "[RequiresUnreferencedCode]/[RequiresAssemblyFiles] (used by [Compose<...>] on '{1}') - " +
        "Compono.Generators' generated registration constructs '{0}' via AddProfile<TProfile>()'s " +
        "generic `new TProfile()`, which would surface an IL3050/IL2026/IL3002 warning for any " +
        "consumer with trim/AOT analysis enabled",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // ADR-0068: generator-wide per-item emission failure isolation (issue #143) - reported by
    // Emitters/EmissionIsolation.cs when an unexpected (non-cancellation) exception escapes one
    // independent item's own emission work, at any of the six call sites ADR-0068 identified as
    // having a natural per-item boundary. Location.None and no stack trace are deliberate (see
    // ADR-0068's Diagnostic model section) - the item's own identity string, not a source
    // location, is what a consumer needs to find the request site themselves. A single shared
    // descriptor (not one per emitter) parameterized by an artifact-kind noun phrase, per ADR-0068's
    // rejection of per-domain diagnostics as architectural symmetry for its own sake.
    public static readonly DiagnosticDescriptor GeneratedSourceEmissionFailed = new(
        "CMP0050",
        "Generated source emission failed unexpectedly",
        "Compono could not emit generated {0} for '{1}' due to an unexpected internal error " +
        "({2}: {3}). Generated output for this item is unavailable.",
        "Compono.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
