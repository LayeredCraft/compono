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
}
