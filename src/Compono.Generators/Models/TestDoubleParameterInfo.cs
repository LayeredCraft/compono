namespace Compono.Generators.Models;

/// <summary>
/// One parameter of a generated test double's method member - <see cref="EscapedName"/> is
/// keyword-@-escaped (ADR-0043 Amendment 10, Finding X), since the explicit-interface-implementation
/// method this backs needs a full, real parameter list, unlike the zero-argument configuration
/// extension (ADR-0043 Amendment 2, Finding 4).
/// </summary>
internal sealed record TestDoubleParameterInfo(string EscapedName, string FullyQualifiedTypeName);
