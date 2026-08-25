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
        "Test-double interface member collides with a generated Configure()/Verify() bridge",
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
}
