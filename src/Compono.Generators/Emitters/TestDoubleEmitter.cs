using Compono.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Emitters;

/// <summary>
/// Renders a <see cref="DiscoveredTestDoubleInfo"/> into the generated double type, its zero-argument
/// configuration extensions, its <c>Configure()</c> bridge, and its module-initializer registration -
/// one file, no namespace declaration (global namespace, ADR-0043 Amendment 11, Finding AA).
/// </summary>
internal static class TestDoubleEmitter
{
    // Same rationale as CompositionPlanEmitter.GeneratorVersion - read once from this assembly's own
    // metadata so the emitted GeneratedCodeAttribute stays accurate as the generator's version changes.
    private static readonly string GeneratorVersion =
        typeof(TestDoubleEmitter).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Select(a => a.InformationalVersion)
            .FirstOrDefault()
        ?? typeof(TestDoubleEmitter).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public static void Generate(SourceProductionContext context, DiscoveredTestDoubleInfo testDouble)
    {
        var model = new
        {
            InterfaceFullyQualifiedName = testDouble.InterfaceFullyQualifiedName,
            SafeIdentifier = testDouble.SafeIdentifier,
            Members = testDouble.Members.Select(m =>
            {
                // ADR-0048's eligible-member dispatch/verification bodies declare locals
                // (__matches/per-parameter matcher pattern variables/__count/the foreach loop
                // variable) v1/v2 never needed - a real parameter can be named anything a synthetic
                // identifier could be (__matches, __count, call, ...), so these are allocated
                // collision-safely against this member's own real (escaped) parameter names, the
                // same lengthening algorithm TestDoubleAnalyzer.SafeReceiverName already uses for the
                // extension receiver. Computed here, in C#, rather than in the template, matching
                // CallLogAccessExpression's own precedent below. Codex review, PR #106.
                var parameterEscapedNames = m.Parameters.Select(p => p.EscapedName).ToArray();
                var matchesLocalName = SafeLocalName("__matches", parameterEscapedNames);
                var countLocalName = SafeLocalName("__count", parameterEscapedNames);
                var callLoopVariableName = SafeLocalName("call", parameterEscapedNames.Append(countLocalName));

                var matcherLocalNames = new string[m.Parameters.Count];
                var reservedForMatchers = new List<string>(parameterEscapedNames) { matchesLocalName };

                for (var i = 0; i < m.Parameters.Count; i++)
                {
                    var name = SafeLocalName($"__m_{m.Parameters[i].OriginalName}", reservedForMatchers);
                    matcherLocalNames[i] = name;
                    reservedForMatchers.Add(name);
                }

                return new
                {
                    m.FieldName,
                    m.EscapedName,
                    m.DeclaringInterfaceFullyQualifiedName,
                    m.SlotTypeFullyQualifiedName,
                    m.ReturnTypeFullyQualifiedName,
                    m.IsVoid,
                    m.DefaultExpression,
                    m.HasConfigurationSurface,
                    m.IsConfigurationRequired,
                    m.IsOverloaded,
                    m.IsEligibleForMatching,
                    m.ExtensionReceiverName,
                    m.GenericSuffix,
                    m.ExtensionIsGeneric,
                    m.ConstraintClausesText,
                    m.OriginalName,
                    Kind = m.Kind.ToString(),
                    AccessorKind = m.AccessorKind.ToString(),
                    MatchesLocalName = matchesLocalName,
                    CountLocalName = countLocalName,
                    CallLoopVariableName = callLoopVariableName,
                    Parameters = m.Parameters
                        .Select((p, i) => new
                        {
                            p.EscapedName,
                            p.OriginalName,
                            p.FullyQualifiedTypeName,
                            p.RefKindPrefix,
                            p.IsParams,
                            p.DefaultValueExpression,
                            // A one-parameter member's call log is a plain List<T> - "(T)" isn't a tuple
                            // type in C#, it's just T in parentheses - so a single real parameter needs a
                            // different read expression ("call" itself) than a multi-parameter one
                            // ("call.Item1", "call.Item2", ...). Computed here rather than in the template
                            // so the arity branch lives in one place, in C#, not duplicated Scriban logic.
                            CallLogAccessExpression = m.Parameters.Count == 1 ? callLoopVariableName : $"{callLoopVariableName}.Item{i + 1}",
                            MatcherLocalName = matcherLocalNames[i],
                        })
                        .ToArray(),
                    OutParameterAssignments = m.OutParameterAssignments.ToArray(),
                    MemberDescription = $"{m.DeclaringInterfaceFullyQualifiedName}.{m.OriginalName}",
                    CallLogTypeText = m.Parameters.Count == 1
                        ? m.Parameters[0].FullyQualifiedTypeName
                        : $"({string.Join(", ", m.Parameters.Select(p => p.FullyQualifiedTypeName))})",
                    CallLogConstructExpression = m.Parameters.Count == 1
                        ? m.Parameters[0].EscapedName
                        : $"({string.Join(", ", m.Parameters.Select(p => p.EscapedName))})",
                };
            }).ToArray(),
            GeneratorVersion,
        };

        var source = TemplateHelper.Render("TestDouble.scriban", model);

        context.AddSource($"{GeneratedFileNaming.HintNameFor(testDouble.InterfaceFullyQualifiedName)}.TestDouble.g.cs", source);
    }

    // Same growth algorithm as TestDoubleAnalyzer.SafeReceiverName (lengthen by prepending "_" until
    // the candidate isn't one of the reserved names) - duplicated rather than shared across the
    // assembly boundary between Discovery (Roslyn-symbol-driven) and Emitters (string-driven, no
    // Roslyn symbols needed here), since the two call sites' inputs are already different shapes
    // (escaped parameter names either way, but TestDoubleAnalyzer's version also folds in type
    // parameter names for the overloaded-generic-extension case, which never applies to a local
    // variable name). Terminates because reserved is finite and each iteration strictly lengthens the
    // candidate. Codex review, PR #106.
    private static string SafeLocalName(string candidate, IEnumerable<string> reserved)
    {
        var used = new HashSet<string>(reserved, StringComparer.Ordinal);

        while (used.Contains(candidate))
            candidate = "_" + candidate;

        return candidate;
    }
}
