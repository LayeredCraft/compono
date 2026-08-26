using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// Resolves one <c>(Name, CanonicalSignature)</c> identity group (two or more members reaching a
/// generated test double's closure that share a name and full-signature identity,
/// <see cref="Emitters.TestDoubleOverloadIdentity"/>) to either a single dominant declaration or a
/// genuine collision - ADR-0044 Amendment 20's correction to Amendment 3 Finding 8's original blanket
/// "reached more than once ⇒ diamond" rule, which didn't distinguish a real diamond (two unrelated
/// base interfaces independently declaring the same shape) from a base interface's abstract
/// declaration resolved by a more-derived interface's own concrete (default-interface-member)
/// redeclaration via <see langword="new"/>.
///
/// Deliberately isolated behind this named helper, not inlined into <c>TestDoubleAnalyzer</c>'s
/// existing identity-group LINQ - this rule is subtle (a "unique dominant declaration" test, not a
/// pairwise-relatedness test; see <see cref="TryResolve"/>'s own remarks for the convergent-diamond
/// counterexample a pairwise rule gets wrong) and now owns a real effective-interface-contract
/// decision, so it stays independently readable and independently unit-testable against bare
/// <see cref="ISymbol"/> declarations, with no dependency on the rest of the analyzer's state.
///
/// Not the same mechanism as <see cref="ITypeSymbol.FindImplementationForInterfaceMember"/> (ADR-0046's
/// static-abstract-member resolution) - that API returns <see langword="null"/> for an instance member
/// reached via <see langword="new"/>-hiding (verified with a real Roslyn spike, ADR-0044 Amendment 20),
/// so this resolver reimplements the equivalent "most specific declaration" rule directly against
/// <c>INamedTypeSymbol.AllInterfaces</c> relationships.
/// </summary>
internal static class TestDoubleMemberIdentityResolver
{
    /// <summary>
    /// Attempts to resolve an identity group - all members reaching the double's closure that share a
    /// <c>(Name, CanonicalSignature)</c> identity - to the single declaration that dominates every
    /// other member in the group.
    /// </summary>
    /// <param name="group">
    /// Every declaration sharing this identity, in any order - a group of one is trivially not a
    /// collision case at all (callers should never call this for a single-member group; behavior for
    /// one is unspecified since <see cref="TestDoubleAnalyzer"/> only ever calls this for
    /// <c>group.Count() &gt; 1</c>).
    /// </param>
    /// <returns>
    /// The single dominant declaration, or <see langword="null"/> if the group is a genuine collision
    /// (no unique dominant declaration exists).
    /// </returns>
    /// <remarks>
    /// The rule is a <b>unique dominant declaration</b> test, not "every pair in the group must have a
    /// base/derived relationship" - a pairwise rule was spiked first and found too restrictive: it
    /// misclassifies a <i>convergent</i> diamond (two genuinely unrelated concrete sibling branches off
    /// a common abstract ancestor - a real collision on their own) as still a collision even when a
    /// leaf interface directly redeclares the member itself, which C# treats as an unambiguous
    /// resolution (the leaf's own declaration is unrelated to neither branch pairwise, yet still
    /// unambiguously dominates the whole group). Concretely:
    /// <list type="number">
    /// <item>
    /// <c>candidates</c> = declarations in the group whose containing interface is not itself a base
    /// interface of any <i>other</i> declaration's containing interface in the group (nothing in the
    /// group is "more derived than" them, from this member's perspective).
    /// </item>
    /// <item>If <c>candidates.Count != 1</c>, the group is a genuine collision.</item>
    /// <item>
    /// Otherwise, the sole candidate resolves the group only if its containing interface is derived
    /// from <i>every other</i> declaration's containing interface in the group (a defensive second
    /// check, kept explicit rather than assumed redundant with step 1) - if that fails, the group is
    /// still a genuine collision.
    /// </item>
    /// </list>
    /// Spiked directly (Roslyn symbols, <c>INamedTypeSymbol.AllInterfaces.Contains(...)</c>) against:
    /// base abstract → derived concrete DIM; base concrete DIM → derived concrete DIM with a different
    /// body; a three-level abstract→concrete→concrete chain; two unrelated concrete siblings with no
    /// leaf resolution (stays a collision); and the convergent-diamond shape above, both with and
    /// without a resolving leaf redeclaration - every case matched this rule's prediction.
    /// </remarks>
    public static ISymbol? TryResolve(IReadOnlyCollection<ISymbol> group)
    {
        var candidates = new List<ISymbol>();

        foreach (var declaration in group)
        {
            var isBaseOfAnotherDeclaration = false;

            foreach (var other in group)
            {
                if (ReferenceEquals(other, declaration))
                    continue;

                if (IsBaseInterfaceOf(declaration.ContainingType, other.ContainingType))
                {
                    isBaseOfAnotherDeclaration = true;
                    break;
                }
            }

            if (!isBaseOfAnotherDeclaration)
                candidates.Add(declaration);
        }

        if (candidates.Count != 1)
            return null;

        var candidate = candidates[0];

        foreach (var other in group)
        {
            if (ReferenceEquals(other, candidate))
                continue;

            if (!IsBaseInterfaceOf(other.ContainingType, candidate.ContainingType))
                return null;
        }

        return candidate;
    }

    private static bool IsBaseInterfaceOf(INamedTypeSymbol? maybeBase, INamedTypeSymbol? maybeDerived) =>
        maybeBase is not null && maybeDerived is not null &&
        !SymbolEqualityComparer.Default.Equals(maybeBase, maybeDerived) &&
        maybeDerived.AllInterfaces.Contains(maybeBase, SymbolEqualityComparer.Default);
}
