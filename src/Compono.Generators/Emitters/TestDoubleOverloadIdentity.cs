using System.Text;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Emitters;

/// <summary>
/// Sibling helper to <see cref="TestDoubleIdentifierNaming"/> (ADR-0044): a stable, collision-safe
/// discriminator for one overload's full signature - parameter types, each parameter's
/// <see cref="RefKind"/>, and the member's own generic arity - so two overloads that share a name
/// (<c>M(int)</c>/<c>M(string)</c>) or would otherwise collapse under a naive parameter-type-only
/// key (<c>M()</c>/<c>M&lt;T&gt;()</c>, ADR-0044 Amendment 2 Finding 3; <c>M(int)</c>/<c>M(ref int)</c>,
/// Amendment 3 Finding 7) each get their own field/extension identity.
/// </summary>
internal static class TestDoubleOverloadIdentity
{
    /// <summary>
    /// The full-signature discriminator hash for a method overload - parameter <see cref="RefKind"/>s,
    /// each parameter's canonicalized type (see <see cref="AppendCanonical"/>), and the method's own
    /// generic arity, all folded into one FNV-1a hash. Two members with the same name and the same
    /// hash are the same overload identity - either the same real overload (impossible within one
    /// interface, the compiler already prevents it) or a diamond collision: the same signature
    /// inherited from two different base interfaces (ADR-0044 Amendment 3 Finding 8).
    /// </summary>
    public static string DiscriminatorHashFor(IMethodSymbol method)
    {
        var builder = new StringBuilder();
        builder.Append("arity:").Append(method.TypeParameters.Length).Append('|');

        foreach (var parameter in method.Parameters)
        {
            builder.Append(parameter.RefKind).Append(':');
            AppendCanonical(builder, parameter.Type, method);
            builder.Append(';');
        }

        return StableHash(builder.ToString());
    }

    /// <summary>
    /// The discriminator hash for a property - properties can never be overloaded by type in C# (no
    /// parameter list outside an indexer, which is already excluded upstream), so every property's
    /// hash is constant; two properties collide under this key only when they share a name, which is
    /// exactly the diamond-inherited-same-name-property case (ADR-0043's existing
    /// <c>DiamondInheritedSameNameProperty</c> coverage).
    /// </summary>
    public static string DiscriminatorHashFor(IPropertySymbol property) => StableHash("property");

    // Walks the type through every generic type argument, array element type, and tuple element
    // type, at every nesting level, treating anything the C# compiler doesn't consider
    // signature-affecting as identical - an open-ended principle (ADR-0044 Amendment 8 Finding 19),
    // not a closed list of special cases:
    //  - nullable-reference annotation is never read here, so it never affects the hash
    //  - `dynamic` canonicalizes to `object` (both are `object` at the metadata level)
    //  - a named tuple canonicalizes to its underlying `ValueTuple<...>` shape
    //  - a reference to the member's own type parameter canonicalizes to its ordinal position, not
    //    its name (`IA.M<T>(T)` and `IB.M<U>(U)` are the same overload identity)
    //  - `nint`/`nuint` canonicalize to `System.IntPtr`/`System.UIntPtr` (same type, different keyword)
    private static void AppendCanonical(StringBuilder builder, ITypeSymbol type, IMethodSymbol owningMethod)
    {
        if (type is ITypeParameterSymbol typeParameter)
        {
            for (var i = 0; i < owningMethod.TypeParameters.Length; i++)
            {
                if (SymbolEqualityComparer.Default.Equals(owningMethod.TypeParameters[i], typeParameter))
                {
                    builder.Append("T#").Append(i);
                    return;
                }
            }

            // A type parameter belonging to some enclosing type, not this method - not affected by
            // the ordinal-token rule above, but still needs a stable textual identity.
            builder.Append(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            return;
        }

        if (type.SpecialType == SpecialType.System_IntPtr)
        {
            builder.Append("System.IntPtr");
            return;
        }

        if (type.SpecialType == SpecialType.System_UIntPtr)
        {
            builder.Append("System.UIntPtr");
            return;
        }

        if (type.TypeKind == TypeKind.Dynamic)
        {
            builder.Append("object");
            return;
        }

        if (type is IArrayTypeSymbol array)
        {
            builder.Append('[').Append(array.Rank).Append(']');
            AppendCanonical(builder, array.ElementType, owningMethod);
            return;
        }

        if (type is INamedTypeSymbol { IsTupleType: true } tuple)
        {
            type = tuple.TupleUnderlyingType ?? tuple;
        }

        if (type is INamedTypeSymbol named)
        {
            builder.Append(named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            if (named.TypeArguments.Length > 0)
            {
                builder.Append('<');

                for (var i = 0; i < named.TypeArguments.Length; i++)
                {
                    if (i > 0)
                        builder.Append(',');

                    AppendCanonical(builder, named.TypeArguments[i], owningMethod);
                }

                builder.Append('>');
            }

            return;
        }

        builder.Append(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }

    // Same FNV-1a algorithm as TestDoubleIdentifierNaming.StableHash/GeneratedFileNaming.StableHash -
    // never string.GetHashCode(), which is randomized per process on modern runtimes.
    private static string StableHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;

        foreach (var c in value)
            hash = (hash ^ c) * prime;

        return hash.ToString("x8");
    }
}
