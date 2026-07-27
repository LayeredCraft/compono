using Microsoft.CodeAnalysis;

namespace Compono.Generators.Diagnostics;

// CMP000x: Constructor selection diagnostics (docs/adr/0002-constructor-selection-algorithm.md)
internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor AmbiguousConstructor = new(
        "CMP0001",
        "Ambiguous construction path",
        "'{0}' has {1} accessible constructors and no way to disambiguate them - Compono requires " +
        "exactly one accessible constructor per docs/adr/0002-constructor-selection-algorithm.md",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoAccessibleConstructor = new(
        "CMP0002",
        "No accessible constructor",
        "'{0}' has no accessible instance constructor Compono can invoke",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TypeNotConstructible = new(
        "CMP0003",
        "Type cannot be constructed",
        "'{0}' is abstract and cannot be constructed directly",
        "Compono.Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
