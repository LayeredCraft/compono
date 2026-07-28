using Microsoft.CodeAnalysis;

namespace Compono.Generators.Diagnostics;

// CMP000x: Constructor selection diagnostics (docs/adr/0002-constructor-selection-algorithm.md)
internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor AmbiguousConstructor = new(
        "CMP0001",
        "Ambiguous construction path",
        "{0} has {1} accessible constructors and no way to disambiguate them - Compono requires " +
        "exactly one accessible constructor per docs/adr/0002-constructor-selection-algorithm.md",
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

    public static readonly DiagnosticDescriptor UnassignedRequiredMembers = new(
        "CMP0007",
        "Required members cannot be composed yet",
        "{0} has required members that its selected constructor doesn't set (no " +
        "[SetsRequiredMembers]) - Compono can't compose required-member initialization yet",
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
}
