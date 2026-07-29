using Compono.Generators.Models;
using Compono.Generators.Types;
using Compono.Generators.WellKnownTypes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Compono.Generators.Discovery;

/// <summary>
/// Finds <c>Composer.Create&lt;T&gt;()</c>/<c>Composer.CreateMany&lt;T&gt;()</c> call sites and
/// resolves each into a <see cref="DiscoveredTypeInfo"/>, per
/// <c>docs/adr/0004-composition-plan-discovery-and-dispatch.md</c>'s call-site discovery mechanism.
/// </summary>
/// <remarks>
/// <c>CreateMany&lt;T&gt;()</c> shares this discovery path with <c>Create&lt;T&gt;()</c> rather than
/// getting its own - both resolve the identical <c>T</c> through <c>PlanCache&lt;T&gt;</c>
/// (<c>Composer.CreateMany</c> just repeats the same root resolution <c>count</c> times), so a
/// consumer that only ever calls <c>CreateMany&lt;Customer&gt;(3)</c> - never
/// <c>Create&lt;Customer&gt;()</c> - still needs a generated plan for <c>Customer</c>. Missing this
/// (PR #13 review, Milestone 2 Phase 4) meant the single most-advertised <c>CreateMany&lt;T&gt;</c>
/// usage threw a <c>CompositionException</c> at runtime unless the same type happened to be
/// independently discovered elsewhere.
/// </remarks>
internal static class CreateInvocationDiscovery
{
    public static bool IsCandidate(SyntaxNode node, CancellationToken cancellationToken) =>
        node is InvocationExpressionSyntax
        {
            // MemberAccessExpressionSyntax covers plain `composer.Create<T>()`;
            // MemberBindingExpressionSyntax covers null-conditional `composer?.Create<T>()` - a
            // separate syntax shape (nested inside a ConditionalAccessExpressionSyntax) that the
            // Roslyn syntax walker visits as its own node, so both have to be matched here or the
            // conditional-access form is silently missed by discovery. "Create" or "CreateMany" - see
            // this type's remarks for why both share one discovery path.
            Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "Create" or "CreateMany", TypeArgumentList.Arguments.Count: 1 } }
                     or MemberBindingExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "Create" or "CreateMany", TypeArgumentList.Arguments.Count: 1 } },
        };

    public static TransitiveClosureResult? Transform(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
            return null;

        var wellKnownTypes = WellKnownTypes.WellKnownTypes.GetOrCreate(context.SemanticModel.Compilation);

        if (method.Name is not ("Create" or "CreateMany") || !wellKnownTypes.IsType(method.ContainingType, WellKnownTypeData.WellKnownType.Compono_Composer))
            return null;

        if (method.TypeArguments.Length != 1)
            return null;

        var typeArgument = method.TypeArguments[0];

        // Points diagnostics at the `T` in `composer.Create<T>()` itself, not at T's declaration
        // (which may not even exist - open-generic shapes are diagnosed by ComposedTypeAnalyzer -
        // or may just be a less useful place to highlight than the call site that actually
        // triggered discovery).
        var location = LocationInfo.From(GetTypeArgumentSyntax(invocation) ?? (SyntaxNode)invocation);

        return ComposedTypeAnalyzer.Analyze(typeArgument, context.SemanticModel.Compilation, location);
    }

    private static TypeSyntax? GetTypeArgumentSyntax(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic.TypeArgumentList.Arguments[0],
            MemberBindingExpressionSyntax { Name: GenericNameSyntax generic } => generic.TypeArgumentList.Arguments[0],
            _ => null,
        };
}
