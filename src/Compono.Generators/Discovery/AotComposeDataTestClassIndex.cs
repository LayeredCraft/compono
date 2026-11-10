using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// Renders xUnit's declared-type index for a <c>[ComposeData]</c> test method's containing type
/// (ADR-0070 Amendment 4 / PLAN-0071 B5.3) - the open-generic-rendered name the Native AOT
/// theory-data-row registration is keyed by, e.g. <c>global::Ns.GenericBase&lt;&gt;</c> or
/// <c>global::Ns.Outer&lt;&gt;.InnerBase</c>.
/// </summary>
/// <remarks>
/// The renderer walks the containing-type chain outermost-first and renders each type's own name
/// (keyword-escaped) with an unbound generic arity marker (<c>&lt;</c> + arity-1 commas + <c>&gt;</c>)
/// rather than the type's display string, which would render the type parameters themselves
/// (<c>&lt;T&gt;</c>) instead of the unbound form xUnit keys by.
/// </remarks>
internal static class AotComposeDataTestClassIndex
{
    private const string GlobalPrefix = "global::";

    // Renders a single type's own name (no containing types, no type parameters) using the same
    // keyword-escaping rules Roslyn's FullyQualifiedFormat applies - reserved keywords get an `@`
    // prefix, contextual keywords (`async`, `record`, `where`, ...) do not. The unbound arity marker
    // is appended separately so an open generic renders as `Name<>` rather than the declared `Name<T>`.
    private static readonly SymbolDisplayFormat TypeNameFormat = new(
        SymbolDisplayGlobalNamespaceStyle.Omitted,
        SymbolDisplayTypeQualificationStyle.NameOnly,
        SymbolDisplayGenericsOptions.None,
        SymbolDisplayFormat.FullyQualifiedFormat.MemberOptions,
        SymbolDisplayFormat.FullyQualifiedFormat.DelegateStyle,
        SymbolDisplayFormat.FullyQualifiedFormat.ExtensionMethodStyle,
        SymbolDisplayFormat.FullyQualifiedFormat.ParameterOptions,
        SymbolDisplayFormat.FullyQualifiedFormat.PropertyStyle,
        SymbolDisplayFormat.FullyQualifiedFormat.LocalOptions,
        SymbolDisplayFormat.FullyQualifiedFormat.KindOptions,
        SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions);

    internal static string Render(INamedTypeSymbol type)
    {
        var builder = new StringBuilder();
        builder.Append(GlobalPrefix);

        if (type.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace)
        {
            var namespaceText = containingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (namespaceText.StartsWith(GlobalPrefix, StringComparison.Ordinal))
                namespaceText = namespaceText.Substring(GlobalPrefix.Length);
            builder.Append(namespaceText);
            builder.Append('.');
        }

        var chain = new List<INamedTypeSymbol>();
        for (var current = type; current is not null; current = current.ContainingType)
            chain.Add(current);

        for (var i = chain.Count - 1; i >= 0; i--)
        {
            if (i != chain.Count - 1)
                builder.Append('.');

            AppendTypeName(builder, chain[i]);
        }

        return builder.ToString();
    }

    private static void AppendTypeName(StringBuilder builder, INamedTypeSymbol type)
    {
        builder.Append(type.ToDisplayString(TypeNameFormat));

        if (type.Arity > 0)
        {
            builder.Append('<');
            builder.Append(',', type.Arity - 1);
            builder.Append('>');
        }
    }
}
