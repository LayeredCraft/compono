using Compono.Generators.Diagnostics;
using Compono.Generators.Emitters;
using Compono.Generators.Models;
using Compono.Generators.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Compono.Generators.Discovery;

/// <summary>
/// Analyzes one generated-test-double-eligible interface leaf (ADR-0043): walks its full transitive
/// base-interface closure (<see cref="ITypeSymbol.AllInterfaces"/>, Amendment 11 Finding Z), validates
/// every member against the supported-shape list, and produces either a
/// <see cref="DiscoveredTestDoubleInfo"/> ready for <c>TestDoubleEmitter</c> or a single diagnostic
/// explaining why it can't be emitted - fail-fast on the first unsupported shape found, the same
/// convention <see cref="RequiredMemberCollector"/>/<c>ConstructorSelector</c> already use. A leaf that
/// fails here still defers entirely to the unchanged runtime-provider path; nothing here is a hard
/// generator error.
///
/// ADR-0044 (v2) narrows the granularity of two of these checks from whole-interface to per-overload:
/// an overloaded member (two members sharing a name but not a full signature identity,
/// <see cref="TestDoubleOverloadIdentity"/>) gets its own <c>Configure()</c>/<c>Verify()</c> surface per
/// overload instead of being rejected outright, and a <see langword="ref"/>/<see langword="out"/>/
/// <see langword="in"/> parameter withholds only that one overload's surface (an
/// overload-set-internal-unsupported shape, Amendment 5) rather than the whole interface. Both keep
/// producing a dispatch body - see <see cref="DiscoveredTestDoubleInfo.InfoDiagnostics"/>.
/// </summary>
internal static class TestDoubleAnalyzer
{
    public static DiscoveredTestDoubleInfo Analyze(INamedTypeSymbol interfaceType, Compilation compilation, LocationInfo? location)
    {
        var fullyQualifiedName = interfaceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var safeIdentifier = TestDoubleIdentifierNaming.SafeIdentifierFor(fullyQualifiedName);

        // A top-level generated double can never implement a private/protected nested interface,
        // even from a call site that could otherwise see it - the same accessibility-domain check
        // already used for generated collection plans/row-invoker registrations. Amendment 8, Finding T.
        if (!compilation.IsSymbolAccessibleWithin(interfaceType, compilation.Assembly))
        {
            return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                DiagnosticDescriptors.InaccessibleTestDoubleInterface, location, interfaceType.ToDisplayString()));
        }

        var closure = new List<INamedTypeSymbol> { interfaceType };
        closure.AddRange(interfaceType.AllInterfaces);

        // A member literally named "Configure" or "Verify" only actually shadows the generated,
        // always-zero-argument Configure()/Verify() bridge extension when it's itself applicable to a
        // zero-argument call - a property/field/event named Configure/Verify always collides (member
        // lookup finds it and never falls back to extension methods for that name at all), but a
        // *method* named Configure/Verify does so only if a zero-argument call is actually applicable
        // to it: C# only falls back to extension-method resolution when ordinary member lookup finds
        // no *applicable* candidate, not merely "no candidate with this name" - verified directly with
        // a real compile spike (an explicitly-implemented `IFoo.Configure(int mode)` alongside a
        // zero-argument `Configure(this IFoo)` extension: calling `foo.Configure()` on an IFoo-typed
        // receiver resolves to the extension without ambiguity). Amendment 3, Finding E; corrected to
        // compare arity, not just name, PR #83 review round 2 - and corrected again to check
        // *applicability*, not raw parameter count, PR #83 review round 4: `Configure(int mode = 0)`
        // and `Configure(params int[] modes)` both have Parameters.Length > 0 but are still applicable
        // to a zero-argument call, so they collide exactly like a genuinely zero-parameter method
        // does. ADR-0044 Amendment 14: a *generic* Configure<T>() interface member has nothing to
        // infer T from at a bare, no-explicit-type-argument call, so it's never applicable to zero
        // arguments either, and doesn't collide - IsApplicableToZeroArguments itself now excludes any
        // generic method. ADR-0044 Requirement 3 widens this reserved-name set to also cover "Verify",
        // reused by the new Verify() bridge - an interface declaring its own Verify member would
        // otherwise silently shadow it exactly like an undiagnosed Configure collision would have.
        var reservedNameCollision = closure.SelectMany(i => i.GetMembers())
            .Where(m => m.Name is "Configure" or "Verify")
            .FirstOrDefault(m => m is not IMethodSymbol method || IsApplicableToZeroArguments(method));
        if (reservedNameCollision is not null)
        {
            return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                DiagnosticDescriptors.TestDoubleConfigureMemberCollision, location, interfaceType.ToDisplayString(), reservedNameCollision.Name));
        }

        // Per-overload identity (ADR-0044): grouped by full signature identity across the whole
        // transitive closure, not scoped to one declaring interface at a time - two real overloads
        // within one interface never share a full-signature identity (the compiler enforces that),
        // but two same-named, same-shaped members inherited from *different* base interfaces (a
        // diamond) genuinely do (Amendment 3 Finding 8). Filtered to the same instance-contract
        // eligibility the emission loop below applies (abstract, or a public non-static default
        // implementation) - a private or non-abstract-static default-interface member never gets its
        // own field/extension at all, so counting it here would falsely flag a real, public, emitted
        // member as "overloaded" against a same-named member that generates nothing. PR #83 review
        // round 3.
        // ADR-0046 (Codex review, PR #99): a static abstract member already resolved by a more-
        // derived interface in the closure (see the emission loop's own FindImplementationForInterfaceMember
        // check below) is IsAbstract: true on the raw symbol reached here - the closure walk visits
        // the *declaring* interface's own unresolved declaration, not the resolved override - so
        // without this exclusion it would still enter collision preprocessing below. Its canonical
        // signature only encodes arity/parameter types (TestDoubleOverloadIdentity.CanonicalSignatureFor),
        // never return type or static-ness, so a resolved static abstract member sharing a name and
        // arity with a same-named *instance* member (e.g. a zero-parameter static member and a
        // zero-parameter instance property/method) would be misclassified as a diamond-colliding or
        // zero-argument-extension-colliding identity, silently withholding the real instance member's
        // Configure()/Verify() surface - or, combined with ADR-0045, incorrectly rejecting the whole
        // interface if that instance member also has no deterministic default. Excluded here so this
        // preprocessing only ever sees the members the emission loop below might actually surface.
        var eligibleCandidates = closure
            .SelectMany(i => i.GetMembers())
            .Where(m => m is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol { IsIndexer: false })
            .Where(m => m.IsAbstract || (!m.IsStatic && m.DeclaredAccessibility == Accessibility.Public))
            .Where(m => !(m.IsStatic && m.IsAbstract && interfaceType.FindImplementationForInterfaceMember(m) is not null))
            .ToArray();

        var identityGroups = eligibleCandidates
            .GroupBy(m => (m.Name, Canonical: IdentityFor(m)))
            .ToArray();

        // A diamond collision: the *same* full-signature identity reached more than once (two
        // different base interfaces independently declaring the same-named, same-shaped member).
        // This identity gets no Configure()/Verify() surface at all - not a whole-interface rejection
        // (Amendment 3 Finding 8, a real improvement over v1's blanket rejection for this case).
        var diamondCollisionIdentities = identityGroups
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        // A name shared by more than one member that would *otherwise* get a Configure()/Verify()
        // surface - a zero-argument configuration extension can only fail to disambiguate when more
        // than one surfaced identity shares it. A name shared only with a diamond-colliding or
        // ref/out/in-fallback sibling (which never gets an extension at all, surfaced or not) has
        // nothing to disambiguate against, so it keeps the ordinary, non-overloaded zero-argument
        // extension shape.
        var overloadedNames = eligibleCandidates
            .Where(m => WouldGetConfigurationSurface(m, diamondCollisionIdentities))
            .GroupBy(m => m.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        // A ref/out/in parameter only gets the fallback-body-without-surface treatment (below) when
        // this member actually has a same-named sibling of *some* shape - "an overload... does not
        // reject its sibling overloads" (this ADR's Decision Outcome, "Overload-set-internal partial
        // support") presupposes an overload set exists. A solo ref/out/in member (no sibling at all)
        // isn't part of any set to preserve and keeps v1's original whole-interface-rejection
        // disposition, unchanged. Deliberately broader than `overloadedNames` above (which only
        // counts *surface-worthy* siblings) - a solo ref/out/in member paired only with a diamond-
        // colliding sibling still has a real sibling, just not a surfaced one. Codex review, PR #88.
        var namesWithAnySibling = eligibleCandidates
            .GroupBy(m => m.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        // A property and a method (or two methods with no shared full signature) sharing a name can
        // still collide even though the diamond-collision check above doesn't catch them - if both
        // end up with a genuinely zero-*value*-parameter generated extension of the same generic
        // arity, the two extensions are the exact same C# signature (`Foo(this Double)` or
        // `Foo<T>(this Double)`), a duplicate-declaration compile error (CS0111). A property's
        // extension is always zero-parameter and non-generic; a method's is zero-value-parameter
        // unless it's part of a real (surfaced) overload set, in which case its own real parameter
        // list disambiguates it instead. Codex review, PR #88. Generic arity is part of that same
        // disambiguation once an overloaded member can also be generic (ADR-0044 Amendment 1) - two
        // overloaded, zero-value-parameter generic methods of *different* arity (`M<T>()`/
        // `M<T, U>()`) emit distinguishable `M<T>(this Double)`/`M<T, U>(this Double)` extensions,
        // not a real collision, so the generic arity the extension will actually carry (zero unless
        // this member is both generic and overloaded, mirroring TestDoubleMemberInfo.ExtensionIsGeneric)
        // is folded into the same grouping key. Codex review, PR #89.
        var zeroArgExtensionSharers = new Dictionary<(string Name, int GenericArity), List<ISymbol>>();

        foreach (var candidate in eligibleCandidates)
        {
            if (diamondCollisionIdentities.Contains((candidate.Name, Canonical: IdentityFor(candidate))))
                continue;

            int effectiveArity;
            var effectiveGenericArity = 0;

            if (candidate is IPropertySymbol)
            {
                effectiveArity = 0;
            }
            else if (candidate is IMethodSymbol candidateMethod)
            {
                if (candidateMethod.Parameters.Any(p => p.RefKind != RefKind.None))
                    continue;

                // ADR-0049 / PR #107 Codex review round 3: a closed-instantiation-eligible member's
                // generated Configure<T>()/Verify<T>() is generic even when it isn't overloaded (unlike
                // an ordinary solo generic member, whose extension stays non-generic per Requirement 2
                // - ExtensionIsGeneric's own rule), and - unlike an ADR-0048 matching-eligible member,
                // which always additionally gets a zero-argument "compatibility" overload alongside its
                // value-parameter one - a closed-instantiation-eligible member with real parameters gets
                // ONLY its real-parameter extension, no zero-arg counterpart at all. So its effective
                // arity must reflect its actual parameter count (not be forced to zero) the same way an
                // overloaded candidate's already is, or a real, non-zero-arg extension gets wrongly
                // treated as a zero-arg-collision candidate against an unrelated sibling.
                var isOverloadedCandidate = overloadedNames.Contains(candidateMethod.Name);
                var isClosedInstantiationCandidate = IsClosedInstantiationEligibleCandidate(candidateMethod, compilation);
                var hasRealExtensionArity = isOverloadedCandidate || isClosedInstantiationCandidate;

                effectiveArity = hasRealExtensionArity ? candidateMethod.Parameters.Length : 0;
                effectiveGenericArity = hasRealExtensionArity ? candidateMethod.TypeParameters.Length : 0;
            }
            else
            {
                continue;
            }

            if (effectiveArity != 0)
                continue;

            var key = (candidate.Name, GenericArity: effectiveGenericArity);

            if (!zeroArgExtensionSharers.TryGetValue(key, out var sharers))
                zeroArgExtensionSharers[key] = sharers = new List<ISymbol>();

            sharers.Add(candidate);
        }

        var zeroArgCollisionMembers = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var sharers in zeroArgExtensionSharers.Values.Where(s => s.Count > 1))
        {
            foreach (var sharer in sharers)
                zeroArgCollisionMembers.Add(sharer);
        }

        // FieldName discriminator suffixes, assigned once up front per (Name, canonical signature)
        // identity - a genuine overload's own suffix must be unique even when two *different*
        // canonical signatures happen to hash to the same 8 hex characters (the hash is a naming
        // convenience, not an identity - see TestDoubleOverloadIdentity's own warning). Walking
        // eligibleCandidates in its already-deterministic order (closure order, then each
        // interface's own GetMembers() order) makes the disambiguation counter's assignment stable
        // across incremental-generator re-runs. Codex review, PR #88.
        //
        // usedFieldNames is reserved *globally*, not per overloaded-name group - a differently-named
        // real member can literally be named after another overload's generated hash suffix (e.g. an
        // interface declaring both M(int)/M(string) and a solo member M_<thatHash>()) - so plain,
        // non-overloaded field names are reserved first (stable, never renamed for a rare collision
        // elsewhere), and every generated suffix is checked against the same shared pool. Codex
        // review, PR #88.
        var usedFieldNames = new HashSet<string>(StringComparer.Ordinal);

        // Only a candidate that would actually get a configuration surface ever emits its own
        // `__{Name}` field at all - a diamond-colliding member (or any other WouldGetConfigurationSurface
        // exclusion) gets no field, no matter how many base interfaces declare it. Reserving its name
        // here regardless was harmless before ADR-0049 (the only consumer of usedFieldNames was the
        // ADR-0048 collision-detection loop, comparing against genuinely-derived _calls/_lock/_m_{param}
        // names - a phantom top-level reservation never fed into that), but now cross-contaminates with
        // the closed-instantiation reservation pass below: a diamond-colliding member literally named
        // "Get_State" reserves "__Get_State" here even though it emits no field under that name, and an
        // unrelated closed-instantiation-eligible member "Get" deriving that exact same suffixed name
        // ("__Get_State", its own real, actually-emitted nested state class) then gets falsely flagged
        // as colliding with a name nothing ever actually emits. Codex review, PR #107 (round 12).
        //
        // A closed-instantiation-eligible candidate ALSO never emits the ordinary `__{Name}` field,
        // regardless of its own configuration-surface outcome - it emits `__{Name}_State`/`_buckets`/
        // `_Bucket` instead (the closed-instantiation reservation pass below). Left unexcluded, a
        // candidate like `U B_State<U>()` (itself closed-instantiation-eligible, WITH a configuration
        // surface - so round 12's fix alone doesn't catch it) reserves "__B_State" here on its own
        // behalf, which then falsely collides with an UNRELATED closed-instantiation member `T B<T>()`'s
        // own real, actually-emitted derived state-class name (also "__B_State", from "B" + "_State").
        // The mirror image of round 12's finding - that one was a non-emitting member polluting a real
        // one; this one is a DIFFERENT real emitter polluting another real one via an unrelated naming
        // scheme. Codex review, PR #107 (round 13).
        //
        // ADR-0050: a matching-eligible-SHAPED candidate (real parameters, non-overloaded, not a
        // self-referencing generic method, no ref-like-typed parameter, not `Equals(object)`) ALSO
        // never emits the plain `__{Name}` field any more IF it actually ends up matching-eligible -
        // it gets the `__{Name}_Entry`/`__{Name}_entries` layout instead (reserved separately,
        // below). Left unexcluded, an unrelated matching-eligible sibling literally named e.g.
        // "Foo_calls" reserves the phantom "__Foo_calls" here, which then falsely collides with a
        // DIFFERENT matching-eligible member "Foo"'s own real, actually-emitted "__Foo_calls"
        // call-log field name - disabling argument matching and multi-entry configuration for "Foo"
        // even though the final layout has no real declaration collision. This mirrors the closed-
        // instantiation exclusion just above. Codex review, PR #108 (round 7).
        //
        // BUT shape alone isn't final eligibility - a matching-eligible-SHAPED candidate can still
        // end up in `derivedNameCollisionMembers` below (computed from THIS candidate's own OTHER
        // derived names, e.g. its `_calls`/`_lock` auxiliary names colliding with something else),
        // in which case it falls back to the plain `__{Name}` field after all - and by then it's too
        // late to add it to `usedFieldNames` through this loop. Deferred candidates are recorded here
        // and their bare names are retroactively reserved (with one extra collision-detection pass)
        // once `derivedNameCollisionMembers` is fully known, below. Closed-instantiation-eligible-
        // SHAPED candidates need no equivalent deferral - a closed-instantiation-shaped candidate can
        // only fail to be closed-instantiation-eligible by failing `WouldGetConfigurationSurface`
        // itself, which ALSO excludes it from the plain-field fallback shape, so there is no fallback
        // scenario to protect against for that path. Codex review, PR #108 (round 8).
        var pendingMatchingEligibleShapedFallbacks = new List<(ISymbol Candidate, string Name)>();
        foreach (var candidate in eligibleCandidates)
        {
            if (overloadedNames.Contains(candidate.Name) || !WouldGetConfigurationSurface(candidate, diamondCollisionIdentities))
                continue;

            if (candidate is not IMethodSymbol candidateAsMethod)
            {
                usedFieldNames.Add($"__{candidate.Name}");
                continue;
            }

            if (IsClosedInstantiationEligibleCandidate(candidateAsMethod, compilation))
                continue;

            var isMatchingEligibleShaped = candidateAsMethod.Parameters.Length > 0 &&
                !(candidateAsMethod.IsGenericMethod &&
                  candidateAsMethod.Parameters.Any(p => TypeReferencesOwnTypeParameter(p.Type, candidateAsMethod))) &&
                !candidateAsMethod.Parameters.Any(p => p.Type.IsRefLikeType) &&
                !(candidateAsMethod.Name == "Equals" && candidateAsMethod.Parameters.Length == 1);

            if (isMatchingEligibleShaped)
                pendingMatchingEligibleShapedFallbacks.Add((candidateAsMethod, $"__{candidate.Name}"));
            else
                usedFieldNames.Add($"__{candidate.Name}");
        }

        var discriminatorSuffixByIdentity = new Dictionary<(string Name, string Canonical), string>();

        foreach (var candidate in eligibleCandidates)
        {
            if (candidate is not IMethodSymbol candidateMethod || !overloadedNames.Contains(candidateMethod.Name))
                continue;

            var key = (candidateMethod.Name, Canonical: IdentityFor(candidateMethod));

            if (discriminatorSuffixByIdentity.ContainsKey(key))
                continue;

            var baseHash = TestDoubleOverloadIdentity.StableHash(key.Canonical);
            var suffix = $"_{baseHash}";
            var disambiguator = 2;

            while (!usedFieldNames.Add($"__{candidateMethod.Name}{suffix}"))
                suffix = $"_{baseHash}_{disambiguator++}";

            discriminatorSuffixByIdentity[key] = suffix;
        }

        // A member's own derived auxiliary names (_calls/_lock/_m_{param}, spliced from FieldName by
        // the scriban template for a matching-eligible member) must be reserved against every OTHER
        // member's own derived names, not just against usedFieldNames' literal top-level names above -
        // two independently-unremarkable members can derive the exact same auxiliary name (a member
        // Foo whose parameter is named x_calls derives matcher field __Foo_m_x_calls; a sibling member
        // literally named Foo_m_x derives call-log field __Foo_m_x_calls from its own, different,
        // FieldName) without either name ever colliding with anything reserved in usedFieldNames
        // itself - the single-pass "check against what's already reserved" approach the first fix
        // round used can never catch a collision between two names computed independently of each
        // other. The real invariant is "reserve every name any candidate could ever produce before
        // checking any of them," not "check each new name against what's been reserved so far."
        // Computed as a prospective, over-approximate pass - every non-overloaded method with at least
        // one parameter is included regardless of whether it will actually turn out eligible for
        // matching (the generic-own-type-parameter/ref-like/Equals-arity exclusions are still applied
        // in isEligibleForMatching below) - reserving a name a member never actually emits is
        // harmless, and folding the two passes into one exact-eligibility-only pre-pass would mean
        // duplicating every other exclusion's logic a second time. Codex review, PR #106 (round 2).
        var derivedAuxiliaryNameOwners = new Dictionary<string, List<ISymbol>>(StringComparer.Ordinal);

        foreach (var candidate in eligibleCandidates)
        {
            // A same-named sibling that would never actually get a configuration surface (e.g. a
            // ref/out/in-shaped overload, ADR-0044's "overload-set-internal partial support") never
            // reaches this pre-pass's real counterpart below either - counting it here would treat two
            // members that merely share a raw Name, only one of which ever emits anything, as if both
            // derive real auxiliary names, spuriously flagging the one real member as self-colliding
            // with a sibling that was never going to emit those names in the first place. Same filter
            // overloadedNames itself already applies above, for the identical reason. Codex review,
            // PR #106 (round 2).
            //
            // A closed-instantiation-eligible candidate (ADR-0049) never emits this pre-pass's
            // _calls/_lock/_m_{param} outer-field names at all, regardless of configuration-surface
            // outcome - its own Calls/Lock/Matcher_* fields live inside its nested _State<T> class
            // instead (see the closed-instantiation-specific reservation pass below, which reserves
            // its own, differently-named set: _State/_buckets/_Bucket). Counting it here reserved a
            // name it never actually produces, spuriously flagging an unrelated real member that
            // happens to be literally named e.g. "Get_calls" as colliding with it - which, left
            // unfixed, incorrectly fed into isClosedInstantiationEligibleShape's own
            // !derivedNameCollisionMembers.Contains(method) gate and rejected the whole interface.
            // Codex review, PR #107 (round 3).
            // A generic method with a real parameter that itself references the method's own type
            // parameter (`void M<T>(T x)`) is categorically ineligible for ADR-0048 matching (a
            // per-member call log can't hold an open type parameter's value - the same exclusion
            // isEligibleForMatching applies below) - it will NEVER emit any of this pre-pass's
            // _calls/_lock/_m_{param} names, for ANY of its parameters, not just the self-referencing
            // one. Missing this exclusion meant a phantom reservation (e.g. __M_m_x_State, derived
            // from a parameter merely named "x_State") could exactly match an UNRELATED closed-
            // instantiation-eligible member's own real, actually-emitted derived state-class name,
            // producing a false collision that rejected the whole interface even though the two
            // members' generated names never actually conflict. Codex review, PR #107 (round 9) -
            // this pre-pass's own "reserving a name a member never actually emits is harmless" design
            // assumption (see comment above) doesn't hold when the phantom name happens to exactly
            // match another real member's own derived name.
            if (candidate is not IMethodSymbol candidateMethod || overloadedNames.Contains(candidateMethod.Name) ||
                candidateMethod.Parameters.Length == 0 ||
                IsClosedInstantiationEligibleCandidate(candidateMethod, compilation) ||
                (candidateMethod.IsGenericMethod && candidateMethod.Parameters.Any(p => TypeReferencesOwnTypeParameter(p.Type, candidateMethod))) ||
                !WouldGetConfigurationSurface(candidateMethod, diamondCollisionIdentities))
                continue;

            // ADR-0050: multi-entry response configuration replaces the per-parameter
            // "__{Name}_m_{param}" matcher fields with a generated per-entry class
            // ("__{Name}_Entry", carrying its own "Matcher_{param}" fields scoped inside that
            // nested type) and a backing ordered list ("__{Name}_entries") at this member's own
            // scope. The old per-parameter fields are no longer emitted here at all, so they must
            // NOT be reserved any more - doing so both reserves a name nothing produces AND, worse,
            // can mark an unrelated member with a real parameter merely named e.g. "x_calls" as a
            // phantom collision, silently excluding it from argument matching entirely. Codex
            // review, PR #108 (round 1). Reserve only what this layout actually emits at this scope:
            // "_calls"/"_lock"/"_Entry"/"_entries".
            var derivedNames = new[]
                {
                    $"__{candidateMethod.Name}_calls", $"__{candidateMethod.Name}_lock",
                    $"__{candidateMethod.Name}_Entry", $"__{candidateMethod.Name}_entries",
                };

            foreach (var name in derivedNames)
            {
                if (!derivedAuxiliaryNameOwners.TryGetValue(name, out var owners))
                    derivedAuxiliaryNameOwners[name] = owners = new List<ISymbol>();

                owners.Add(candidateMethod);
            }
        }

        // Closed-instantiation-eligible candidates (ADR-0049): a generic method whose return type
        // depends on its own type parameter (T, Task<T>/Task<T?>, ValueTask<T>/ValueTask<T?>) gets a
        // generator-emitted nested state class and Dictionary<Type, object> bucket instead of the
        // ADR-0048 matching fields above - reserved through the SAME derivedAuxiliaryNameOwners pool,
        // so a collision between the two mechanisms (or between two closed-instantiation-eligible
        // candidates) is caught by the same two-pass discipline this file already uses, not by an
        // independent pass that could miss a cross-mechanism collision.
        var derivedNameCollisionMembers = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var candidate in eligibleCandidates)
        {
            if (candidate is not IMethodSymbol candidateMethod ||
                !IsClosedInstantiationEligibleCandidate(candidateMethod, compilation) ||
                !WouldGetConfigurationSurface(candidateMethod, diamondCollisionIdentities))
                continue;

            var baseFieldName = overloadedNames.Contains(candidateMethod.Name)
                ? $"__{candidateMethod.Name}{discriminatorSuffixByIdentity[(candidateMethod.Name, Canonical: IdentityFor(candidateMethod))]}"
                : $"__{candidateMethod.Name}";

            var derivedClosedInstantiationNames = new[] { $"{baseFieldName}_State", $"{baseFieldName}_buckets", $"{baseFieldName}_Bucket" };

            // The generated state class's own type parameter can't be renamed away from the real
            // method's type parameter identifier (its declared name is baked verbatim into every
            // pre-rendered type-string this candidate's slot/parameter types already use - e.g.
            // ReturnTypeFullyQualifiedName literally spells "Task<__Get_State?>" via Roslyn's own
            // ToDisplayString, not a name this code chooses or can substitute) - so if the consumer's
            // own type parameter happens to be named identically to OUR derived state-class name
            // (e.g. `T Get<__Get_State>()`, an admittedly obscure but real, legal interface), the
            // resulting `class __Get_State<__Get_State>` is CS0694 ("type parameter has the same name
            // as the type"), a real compiler error, not a warning. This is deliberately a direct,
            // SELF-scoped check against this candidate's own derived name - not a reservation into the
            // shared usedFieldNames/derivedAuxiliaryNameOwners pool. Round 6's first attempt did the
            // latter and was itself a real bug (Codex review, PR #107 round 7): reserving the type
            // parameter's raw name globally meant an UNRELATED method's own, differently-named type
            // parameter merely happening to share that same string (e.g. method Get deriving
            // "__Get_State" while an entirely separate method Other<__Get_State>() has its own type
            // parameter literally named that) would wrongly flag Get as colliding too, even though
            // Other's type parameter is scoped to Other's own, differently-named state class and never
            // appears anywhere near Get's declaration. The two names only ever actually collide when
            // they belong to the SAME state class declaration.
            //
            // Also checked against the bucket METHOD's own derived name (Codex review, PR #107 round
            // 8): a generic method's own name colliding with its own type parameter is CS0694 too, not
            // just a type's - `internal __Get_State<__Get_Bucket> __Get_Bucket<__Get_Bucket>()` is
            // exactly as illegal as the class case above when the consumer's own type parameter is
            // literally named "__Get_Bucket". The bucket dictionary FIELD's own derived name
            // ("_buckets") needs no equivalent check - a field has no type parameter of its own to
            // collide with, so a type parameter merely sharing its name elsewhere in the same
            // declaration is never a CS0694 risk for it.
            // Also checked against the STATE CLASS's own MEMBER names (Codex review, PR #107 round
            // 10): the state class's own type parameter is in scope for its own member declarations
            // too, not just its own class/method names above - `class __Create_State<Config> {
            // internal ReturnConfig<Config> Config; }` collides a member named "Config" with its
            // enclosing type's own type parameter "Config" when the consumer's type parameter is
            // literally named that.
            //
            // ADR-0050 moved "Config"/"Matcher_{param}" one level deeper for a matched-parameter,
            // non-overloaded state class (closed_instantiation_has_matched_parameters in the
            // template/emitter, TestDouble.scriban lines 15-25): they now live inside the nested
            // "Entry" class, not directly on the state class itself, whose own direct members become
            // just "Entries"/"Calls"/"Lock". Checking "Config"/"Matcher_{param}" against the state
            // class's own type parameter for this branch was itself a phantom check - those names
            // are scoped to Entry, a different declaration the outer type parameter isn't in scope
            // for, so it could never actually be a real CS0694 collision - and, worse, it can
            // falsely disqualify an otherwise-fully-supported member and reject the whole interface
            // over a collision that was never real. The non-matched-parameter branch (zero
            // parameters, or overloaded) keeps "Config" directly on the state class exactly as
            // before (template lines 26-27) - only the matched-parameter branch relocates it. Codex
            // review, PR #108 (round 9).
            var stateClassMemberNames = candidateMethod.Parameters.Length > 0 && !overloadedNames.Contains(candidateMethod.Name)
                ? new List<string> { "Entry", "Entries", "Calls", "Lock" }
                : new List<string> { "Config" };

            if (candidateMethod.TypeParameters[0].Name is var typeParameterName &&
                (typeParameterName == $"{baseFieldName}_State" || typeParameterName == $"{baseFieldName}_Bucket" ||
                 stateClassMemberNames.Contains(typeParameterName)))
                derivedNameCollisionMembers.Add(candidateMethod);

            foreach (var name in derivedClosedInstantiationNames)
            {
                if (!derivedAuxiliaryNameOwners.TryGetValue(name, out var owners))
                    derivedAuxiliaryNameOwners[name] = owners = new List<ISymbol>();

                owners.Add(candidateMethod);
            }
        }

        foreach (var (name, owners) in derivedAuxiliaryNameOwners)
        {
            if (owners.Count <= 1 && !usedFieldNames.Contains(name))
                continue;

            foreach (var owner in owners)
                derivedNameCollisionMembers.Add(owner);
        }

        // Codex review, PR #108 (round 8): a matching-eligible-SHAPED candidate deferred above can
        // have just been added to `derivedNameCollisionMembers` by the pass immediately above (via
        // its OWN `_calls`/`_lock`/etc. auxiliary names colliding with something else) - meaning it
        // will actually fall back to the plain `__{Name}` field after all, a name never reserved in
        // `usedFieldNames` because the earlier loop assumed it would stay matching-eligible. Reserve
        // those now-known-real fallback names, then re-run the SAME collision-detection loop once
        // more so a fallback name that newly collides with some other real emitter is still caught
        // (rather than silently producing a duplicate-member CS0102).
        //
        // A single extra pass isn't enough (Codex review, PR #108 round 9): reserving one pending
        // candidate's fallback name can itself newly disqualify a DIFFERENT pending candidate (e.g.
        // reserving "__Bar_State_calls" collides with a sibling's own derived name, which disqualifies
        // "Bar_State_calls" from matching eligibility - but "Bar_State_calls" is itself one of THIS
        // loop's pending candidates, and its own fallback name was never reserved because the single
        // extra pass had already moved on). Loop reservation + re-detection to a fixed point instead
        // of a fixed one extra pass - each iteration can only ever ADD to `usedFieldNames` (monotonic,
        // bounded by the finite candidate set), so this always terminates.
        bool addedFallbackReservation;
        do
        {
            addedFallbackReservation = false;
            foreach (var (candidate, name) in pendingMatchingEligibleShapedFallbacks)
            {
                if (derivedNameCollisionMembers.Contains(candidate) && usedFieldNames.Add(name))
                    addedFallbackReservation = true;
            }

            if (addedFallbackReservation)
            {
                foreach (var (name, owners) in derivedAuxiliaryNameOwners)
                {
                    if (owners.Count <= 1 && !usedFieldNames.Contains(name))
                        continue;

                    foreach (var owner in owners)
                        derivedNameCollisionMembers.Add(owner);
                }
            }
        } while (addedFallbackReservation);

        var reportedDiamondIdentities = new HashSet<(string Name, string Canonical)>();
        var reportedZeroArgCollisionNames = new HashSet<string>();
        var members = new List<TestDoubleMemberInfo>();
        var infoDiagnostics = new List<DiagnosticInfo>();
        var configurationRequiredCount = 0;

        foreach (var declaringInterface in closure)
        {
            var declaringInterfaceFullyQualifiedName = declaringInterface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            foreach (var member in declaringInterface.GetMembers())
            {
                switch (member)
                {
                    case IEventSymbol:
                        return Failure(fullyQualifiedName, safeIdentifier,
                            UnsupportedMember(interfaceType, member, "an event", location));

                    case IPropertySymbol { IsIndexer: true }:
                        return Failure(fullyQualifiedName, safeIdentifier,
                            UnsupportedMember(interfaceType, member, "an indexer", location));

                    // Checked before the MethodKind filter below, not after: a static abstract
                    // operator (`static abstract IFoo operator +(...)`) reports MethodKind.UserDefinedOperator,
                    // not Ordinary, so the filter would otherwise silently `continue` past it before
                    // this diagnostic ever got a chance to see it - producing an incomplete double
                    // that fails CS0535 instead of the promised clean fallback diagnostic. Excludes
                    // property/event accessor MethodKinds - a static abstract property's own
                    // get_X/set_X accessor methods are handled by the IPropertySymbol case below
                    // instead, so its diagnostic names the property, not its ugly accessor method.
                    case IMethodSymbol
                    {
                        IsStatic: true,
                        IsAbstract: true,
                        MethodKind: not (MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove),
                    } staticAbstractMethod:
                    {
                        // ADR-0046: a static abstract member with no body, declared on some
                        // interface in the closure, might already be resolved by a MORE-DERIVED
                        // interface in the same closure re-implementing it with a real, concrete
                        // body - C#'s own "most specific implementation" rule for static interface
                        // members (ITypeSymbol.FindImplementationForInterfaceMember, verified with a
                        // real Roslyn spike). Real-world example, and the actual Gate-B motivation:
                        // AWSSDK's IAmazonS3 re-implements its base
                        // IAmazonService.CreateDefaultClientConfig() with a concrete body, even
                        // though IAmazonService itself only declares it abstract - IAmazonS3 was
                        // never genuinely unimplemented, and the old per-interface closure walk was
                        // incorrectly treating it as if it were (inspecting IAmazonService's own raw
                        // abstract declaration in isolation, never noticing IAmazonS3 had already
                        // resolved it). When interfaceType (the leaf this double implements) already
                        // resolves the member, it's not part of this double's unimplemented contract
                        // at all - skipped silently, the same disposition an ordinary non-abstract
                        // static member already gets below.
                        //
                        // A genuinely unresolved static abstract member (no override anywhere in the
                        // closure) stays whole-interface rejected, unchanged from before this ADR -
                        // a real compile spike proved C# itself forbids using an interface with a
                        // genuinely unresolved static abstract member as a type argument to *any*
                        // generic method, constrained or not (CS8920), and Compono's own
                        // ICompositionContext.Resolve<TValue>() is exactly such a call - so a
                        // generated stub for this case could never actually be reached by a real
                        // consumer; the composition call site itself would fail to compile before
                        // ever reaching the double. Not shipped for that reason.
                        if (interfaceType.FindImplementationForInterfaceMember(staticAbstractMethod) is not null)
                            continue;

                        return Failure(fullyQualifiedName, safeIdentifier,
                            UnsupportedMember(interfaceType, member, "a static abstract member", location));
                    }

                    case IMethodSymbol { MethodKind: not MethodKind.Ordinary }:
                        continue;

                    case IMethodSymbol method:
                    {
                        // Static abstract methods are already handled above - a static method
                        // reaching here is necessarily non-abstract (a concrete default
                        // implementation), not part of the instance contract a double implements.
                        if (method.IsStatic)
                            continue;

                        // A non-abstract instance member with a default implementation (C# 8+ default
                        // interface members) that isn't public - most commonly `private` - is never
                        // part of any implementing type's contract at all; it's only callable from
                        // within the interface's own other default implementations. Explicitly
                        // implementing it (`ReturnType IFoo.Helper()`) is both unnecessary and invalid,
                        // since a private interface member isn't accessible outside the interface to
                        // begin with - skip it silently, same as a non-abstract static member.
                        // PR #83 review round 2.
                        if (!method.IsAbstract && method.DeclaredAccessibility != Accessibility.Public)
                            continue;

                        // ADR-0044 Requirement 2 / ADR-0049: a generic method is supported when its
                        // return type doesn't reference its own type parameter(s) anywhere in its
                        // symbol graph (generic type arguments, array element types, recursively) - a
                        // syntax-tree check would silently miss a metadata-defined interface like the
                        // real ILogger<T> (no syntax tree in the consumer's own compilation at all) -
                        // OR when it matches ADR-0049's narrower, evidenced closed-instantiation-
                        // eligible shape (exactly T, or the sole type argument of Task<T>/Task<T?>/
                        // ValueTask<T>/ValueTask<T?>, for a method with exactly one type parameter,
                        // with no ref-like real parameter and no real parameter itself referencing the
                        // method's own type parameter - the SetContextDataAsync<T>-shaped case ADR-0049
                        // deliberately leaves untouched - and no derived-name collision with another
                        // member's own generated state-class/bucket names). Any other return type that
                        // depends on its own type parameter (deeper nesting, multiple type parameters)
                        // still has no constructible body at any granularity (Amendment 13) - the same
                        // no-constructible-body bucket a non-nullable-no-default return already
                        // occupies, so it still triggers whole-interface rejection, not member-scoped
                        // exclusion.
                        var isClosedInstantiationEligibleShape = false;

                        if (method.IsGenericMethod && TypeReferencesOwnTypeParameter(method.ReturnType, method))
                        {
                            if (IsClosedInstantiationEligibleCandidate(method, compilation) &&
                                !derivedNameCollisionMembers.Contains(method))
                            {
                                isClosedInstantiationEligibleShape = true;
                            }
                            else
                            {
                                return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                    DiagnosticDescriptors.UnsupportedTestDoubleGenericReturnShape, location,
                                    interfaceType.ToDisplayString(), method.Name));
                            }
                        }

                        // Amendment 6 Finding 15, withdrawn-and-unified by Amendment 9: a type
                        // parameter used as `T?` in a parameter can require a C# 9+ constraint
                        // restatement on the explicit implementation to disambiguate its inherited,
                        // oblivious reference-or-value-type meaning - correctly modeling exactly when
                        // that's *required*, and with which exact keyword, isn't something this ADR
                        // has a verified answer for (two review rounds gave conflicting answers for
                        // the permitted keyword set even in the constrained case), so this shape is
                        // diagnosed and excluded rather than guessed at, regardless of whether the
                        // type parameter carries a constraint at all. Codex review, PR #89.
                        if (method.IsGenericMethod)
                        {
                            var nullableTypeParameterParameter = method.Parameters.FirstOrDefault(
                                p => ContainsNullableOwnTypeParameter(p.Type, method));

                            if (nullableTypeParameterParameter is not null)
                            {
                                return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                    DiagnosticDescriptors.UnsupportedTestDoubleParameterShape, location,
                                    interfaceType.ToDisplayString(), method.Name, nullableTypeParameterParameter.Name,
                                    "as a generic type parameter used with a nullable annotation (T?), " +
                                    "which may require a constraint restatement Compono cannot correctly determine"));
                            }
                        }

                        // A C-style variable-argument method (`void M(int x, __arglist)`) - IMethodSymbol
                        // .Parameters excludes the __arglist sentinel entirely, so every check below
                        // would silently treat this as an ordinary fixed-arity method and emit an
                        // explicit implementation with the wrong signature (CS0535 - it doesn't actually
                        // implement the vararg interface member). Checked before any parameter-shape
                        // logic runs, verified with a real compile spike (`IsVararg` is true,
                        // `Parameters.Length` doesn't include the sentinel). Codex review, PR #88.
                        if (method.IsVararg)
                        {
                            return Failure(fullyQualifiedName, safeIdentifier,
                                UnsupportedMember(interfaceType, member, "a variable-argument (__arglist) method", location));
                        }

                        var identity = (method.Name, Canonical: IdentityFor(method));
                        var isDiamondCollision = diamondCollisionIdentities.Contains(identity);
                        var isOverloaded = overloadedNames.Contains(method.Name);

                        // Hoisted ahead of the return-type default-lookup check below (ADR-0045
                        // Amendment 6) - a method-shaped object-member collision must keep today's
                        // whole-interface CMP0025 rejection when it also has no deterministic
                        // default, not fall through and get relabeled CMP0024 by the (unchanged)
                        // object-collision check further down. Same shape that check already uses
                        // (extensionArity mirrors the generated extension's real arity: zero unless
                        // this member is overloaded), just evaluated independent of
                        // hasConfigurationSurface/defaultExpression so it's available this early.
                        var objectCollisionExtensionArity = isOverloaded ? method.Parameters.Length : 0;
                        // Exemption widened to also cover a solo (non-overloaded) closed-instantiation-
                        // eligible member (Codex review, PR #107 round 10) - `T ToString<T>() where T :
                        // class` matches the exact same "solo generic member gets a genuinely generic,
                        // distinguishable extension" reasoning the isOverloaded branch already used,
                        // but this HOISTED copy of the object-collision check (evaluated before
                        // hasConfigurationSurface/defaultExpression, specifically so a member with no
                        // deterministic default still gets diagnosed correctly) was never updated to
                        // know about ADR-0049 at all - it kept treating a solo closed-instantiation
                        // member's extension as the ordinary zero-arity, non-generic shape, wrongly
                        // colliding it with object.ToString() and rejecting the whole interface with
                        // CMP0025 before ever reaching the later, already-correct exemption further
                        // down. Mirrors that later check's own exemption shape exactly.
                        var isObjectMemberCollisionShaped =
                            !((method.IsGenericMethod && isOverloaded) || (isClosedInstantiationEligibleShape && !isOverloaded)) &&
                            ((method.Name is "ToString" or "GetHashCode" or "GetType" && objectCollisionExtensionArity == 0) ||
                             (method.Name is "Equals" && objectCollisionExtensionArity == 1 &&
                              !method.Parameters[0].Type.IsRefLikeType && !method.Parameters[0].IsParams &&
                              !method.Parameters[0].IsOptional));

                        if (isDiamondCollision)
                        {
                            if (reportedDiamondIdentities.Add(identity))
                            {
                                var signature = $"({string.Join(", ", method.Parameters.Select(p => p.Type.ToDisplayString()))})";

                                // Unlike a whole-interface-rejecting DiagnosticInfo (deliberately
                                // reported at each call site's own request Location - see the merge
                                // comment in ComponoIncrementalGenerator), this describes a structural
                                // property of the interface's own declaration, not the call site -
                                // it must be call-site-*independent* so the same interface discovered
                                // from two different call sites still produces byte-identical
                                // DiscoveredTestDoubleInfo values (DiagnosticInfo.Equals includes
                                // Location) and doesn't spuriously trip the CMP0028
                                // conflicting-metadata merge path.
                                infoDiagnostics.Add(new DiagnosticInfo(
                                    DiagnosticDescriptors.OverloadedTestDoubleMember, null,
                                    interfaceType.ToDisplayString(), method.Name, signature));
                            }
                        }

                        // Pointer/function-pointer parameters are never given a fallback body -
                        // a pointer-typed parameter requires the method to be declared `unsafe`
                        // regardless of whether the body touches it, and this feature never emits
                        // `unsafe` generated code or requires a consumer to set AllowUnsafeBlocks.
                        // Restores ADR-0043 Amendment 10 Finding Y's original v1 disposition (whole-
                        // interface rejection) for this shape. Amendment 5, Finding 12. Checked
                        // recursively through array element types (`int*[]` has TypeKind.Array at the
                        // top level, not Pointer) - C#'s CS0306 already forbids a pointer type as a
                        // generic type argument, so an array of pointers is the only nesting shape
                        // that can hide one. Codex review, PR #88.
                        foreach (var parameter in method.Parameters)
                        {
                            if (ContainsPointerType(parameter.Type))
                            {
                                return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                    DiagnosticDescriptors.UnsupportedTestDoubleParameterShape, location,
                                    interfaceType.ToDisplayString(), method.Name, parameter.Name,
                                    "as a pointer or function-pointer type, which cannot be used as a generic type argument"));
                            }
                        }

                        // A ref/out/in parameter is an overload-set-internal-unsupported shape
                        // (ADR-0044 Amendment 5): this one overload gets a deterministic-default
                        // fallback body and an informational diagnostic, but its sibling overloads
                        // (and the rest of the interface) are unaffected - only when a sibling of
                        // this name actually exists (see namesWithAnySibling above). A *solo*
                        // ref/out/in member has no overload set to preserve and still rejects the
                        // whole interface, matching v1's original disposition and every other no-
                        // constructible-body shape (a pointer parameter, a non-nullable-no-default
                        // return). Codex review, PR #88.
                        var hasRefOutInParameter = method.Parameters.Any(p => p.RefKind != RefKind.None);

                        if (hasRefOutInParameter && !namesWithAnySibling.Contains(method.Name))
                        {
                            return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                DiagnosticDescriptors.UnsupportedTestDoubleParameterShape, location,
                                interfaceType.ToDisplayString(), method.Name,
                                method.Parameters.First(p => p.RefKind != RefKind.None).Name,
                                "as a ref/out/in parameter, which Compono cannot compose a value for"));
                        }

                        var isZeroArgCollision = zeroArgCollisionMembers.Contains(method);
                        var hasConfigurationSurface = !isDiamondCollision && !hasRefOutInParameter && !isZeroArgCollision;

                        if (isZeroArgCollision && reportedZeroArgCollisionNames.Add(method.Name))
                        {
                            infoDiagnostics.Add(new DiagnosticInfo(
                                DiagnosticDescriptors.ZeroArgumentTestDoubleExtensionCollision, null,
                                interfaceType.ToDisplayString(), method.Name));
                        }

                        if (method.ReturnsByRef || method.ReturnsByRefReadonly)
                        {
                            return Failure(fullyQualifiedName, safeIdentifier,
                                ReturnShapeUnsupported(interfaceType, method, "a by-ref return", location));
                        }

                        if (ContainsPointerType(method.ReturnType))
                        {
                            return Failure(fullyQualifiedName, safeIdentifier,
                                ReturnShapeUnsupported(interfaceType, method, "a pointer or function-pointer return type", location));
                        }

                        if (!method.ReturnsVoid && method.ReturnType.IsRefLikeType)
                        {
                            return Failure(fullyQualifiedName, safeIdentifier,
                                ReturnShapeUnsupported(interfaceType, method, "a ref struct (ref-like type) return type", location));
                        }

                        var isVoid = method.ReturnsVoid;
                        var returnTypeFullyQualifiedName = "";
                        var defaultExpression = "";
                        var isConfigurationRequired = false;

                        if (!isVoid)
                        {
                            returnTypeFullyQualifiedName = method.ReturnType.ToDisplayString(TestDoubleDefaults.NullableAwareFullyQualifiedFormat);

                            if (!TestDoubleDefaults.TryGetDefaultExpression(method.ReturnType, compilation, out defaultExpression))
                            {
                                // ADR-0045: a non-nullable-reference-return member with no
                                // deterministic default no longer rejects its whole interface -
                                // it generates as configuration-required instead - but only when
                                // it would otherwise have a real Configure()/Verify() surface
                                // (Amendment 3). A member that also lacks a surface for an
                                // unrelated reason (diamond, ref/out/in, zero-arg collision, or -
                                // for a method specifically - an object-member collision,
                                // Amendment 6) keeps today's unchanged whole-interface CMP0025
                                // rejection, so no member ever ends up throwing unconditionally
                                // with no way to configure it.
                                if (hasConfigurationSurface && !isObjectMemberCollisionShaped)
                                {
                                    isConfigurationRequired = true;
                                }
                                else
                                {
                                    return Failure(fullyQualifiedName, safeIdentifier, ReturnShapeUnsupported(
                                        interfaceType, method, "a non-nullable reference type with no deterministic default", location));
                                }
                            }
                        }

                        // Every out parameter in a fallback body must be definitely assigned before
                        // every return path (CS0177 otherwise) - assigned its own deterministic
                        // default via the same TestDoubleDefaults lookup return types use. If that
                        // lookup fails for even one out parameter, the whole overload has no
                        // constructible body at any granularity and joins whole-interface rejection
                        // instead of silently assigning `default` and risking a non-nullable-contract
                        // violation. ref/in parameters need no such handling - they're never required
                        // to be written. Amendment 8, Finding 20.
                        var outParameterAssignments = new List<string>();

                        if (hasRefOutInParameter)
                        {
                            foreach (var parameter in method.Parameters)
                            {
                                if (parameter.RefKind != RefKind.Out)
                                    continue;

                                if (!TestDoubleDefaults.TryGetDefaultExpression(parameter.Type, compilation, out var outDefault))
                                {
                                    return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                        DiagnosticDescriptors.UnsupportedTestDoubleParameterShape, location,
                                        interfaceType.ToDisplayString(), method.Name, parameter.Name,
                                        "as an out parameter of a non-nullable reference type with no deterministic default"));
                                }

                                outParameterAssignments.Add(
                                    $"{RequiredMemberCollector.EscapeIdentifier(parameter.Name)} = {outDefault};");
                            }

                            // Call-site-independent, same reasoning as the diamond-collision
                            // InfoDiagnostics.Add above - this describes the overload's own
                            // declared shape, not the call site. A distinct descriptor (CMP0030,
                            // not CMP0026) from the whole-interface-rejecting pointer/no-sibling
                            // case above - that shared message wrongly claims the whole leaf falls
                            // back to the runtime-provider path, which isn't true here. Codex
                            // review, PR #88.
                            infoDiagnostics.Add(new DiagnosticInfo(
                                DiagnosticDescriptors.OverloadScopedUnsupportedParameterShape, null,
                                interfaceType.ToDisplayString(), method.Name,
                                method.Parameters.First(p => p.RefKind != RefKind.None).Name));
                        }

                        // Object-collision check compares the generated discriminator extension's
                        // own applicability to an implicit (no explicit type argument) call at its
                        // real arity - not the interface member's own declared signature. A
                        // non-overloaded member's extension is always zero-argument
                        // (ToString/GetHashCode/GetType collide; Equals(object) is one-argument, so a
                        // zero-argument generated "Equals" extension does not collide with it,
                        // Amendment 6 Finding N). An *overloaded* member's extension carries the real
                        // parameter list (Amendment 1), so an overloaded, non-generic, single-
                        // parameter Equals(T) collides too, unless T can never convert to object
                        // (a ref-like type, Amendment 16). Only checked for a member that would
                        // otherwise get a Configure()/Verify() surface - a diamond-colliding or
                        // ref/out/in-fallback member never gets one regardless. The escape hatch a
                        // *generic* member gets (Amendment 14, corrected by Amendment 16) is gated on
                        // the *generated extension's* own genericity, not the real member's: an
                        // explicit-type-argument call can only disambiguate against the non-generic
                        // object member when the extension itself accepts a type argument, which is
                        // true only for an overloaded generic member (Amendment 1 makes that
                        // extension generic too). A solo generic member's extension stays non-generic
                        // and zero-argument (Requirement 2), so it has no escape hatch at all and
                        // keeps v1's original disposition unchanged - Amendment 16's own
                        // "genuinely broken, unreachable-either-way" finding for the naive
                        // method-genericity-gated version of this fix.
                        // ADR-0049: a solo (non-overloaded) closed-instantiation-eligible member's
                        // real generated extension isn't zero-argument when it has real parameters
                        // (they're Match<TParam>-wrapped, the same shape a solo ADR-0048-eligible
                        // member already gets) - the extensionArity formula below assumes a solo
                        // member's extension is always zero-argument, which is only true for a
                        // non-eligible-for-matching, non-closed-instantiation-eligible member. Skipped
                        // here for the same reason a solo eligible-for-matching member's own residual
                        // Equals-collision risk is instead handled by its own targeted, inline
                        // exclusion (IsEligibleForMatching's final `!(method.Name == "Equals" &&
                        // parameters.Count == 1)` clause) rather than this shared check - no real
                        // evidence motivates a closed-instantiation-eligible member literally named
                        // ToString/GetHashCode/GetType/Equals, so this narrow residual gap is left
                        // unhandled rather than guessed at, per this repo's own evidence discipline.
                        // An *overloaded* closed-instantiation-eligible member is unaffected - it
                        // reuses the real-parameter-discriminator extension shape the isOverloaded
                        // branch below already correctly computes extensionArity for.
                        var skipObjectCollisionCheck = (method.IsGenericMethod && isOverloaded) ||
                            (isClosedInstantiationEligibleShape && !isOverloaded);

                        if (hasConfigurationSurface && !skipObjectCollisionCheck)
                        {
                            var extensionArity = isOverloaded ? method.Parameters.Length : 0;

                            if (method.Name is "ToString" or "GetHashCode" or "GetType" && extensionArity == 0)
                            {
                                return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                    DiagnosticDescriptors.TestDoubleObjectMemberCollision, location,
                                    interfaceType.ToDisplayString(), method.Name));
                            }

                            // A params-shaped or optional single parameter (Equals(params int[]
                            // values), Equals(int value = 0)) is not genuinely a one-required-argument
                            // overload - same "applicable to zero arguments" reasoning Amendment 12
                            // already applies to ToString/GetHashCode/GetType's own params case,
                            // mirrored here: object.Equals(object) is inapplicable to a zero-argument
                            // or two-plus-argument call either way, so this overload keeps a reachable
                            // spelling via Configure().Equals() (now that the default value itself is
                            // mirrored onto the extension too) even though a literal one-argument call
                            // still collides. Codex review, PR #88.
                            // IsOptional, not HasExplicitDefaultValue - a parameter can be optional
                            // purely via [Optional] with no explicit default value at all (verified
                            // with a real compile spike: HasExplicitDefaultValue is false but
                            // IsOptional is still true, and Configure().Equals() is still reachable).
                            // Codex review, PR #88.
                            if (method.Name is "Equals" && extensionArity == 1 &&
                                !method.Parameters[0].Type.IsRefLikeType && !method.Parameters[0].IsParams &&
                                !method.Parameters[0].IsOptional)
                            {
                                return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                    DiagnosticDescriptors.TestDoubleObjectMemberCollision, location,
                                    interfaceType.ToDisplayString(), method.Name));
                            }
                        }

                        var parameters = method.Parameters
                            .Select(p => new TestDoubleParameterInfo(
                                RequiredMemberCollector.EscapeIdentifier(p.Name),
                                p.Name,
                                p.Type.ToDisplayString(TestDoubleDefaults.NullableAwareFullyQualifiedFormat),
                                // The explicit implementation's ref-safety contract has to match the
                                // interface member's exactly, or the consumer gets CS8987, not a
                                // supported double or a diagnostic. Three distinct cases, all verified
                                // with real compile spikes (Codex review, PR #88):
                                //  - ScopedKind.ScopedRef on a ref/in/ref-readonly parameter: restate
                                //    "scoped ", *except* for "out" - every out parameter is
                                //    unconditionally ScopedRef even with no "scoped" written in source,
                                //    so restating it there is always redundant (confirmed: `out int x`
                                //    and `scoped out int x` both report ScopedKind.ScopedRef).
                                //  - ScopedKind.ScopedValue on an ordinary by-value ref-like parameter
                                //    (e.g. `scoped Span<int> value`): always restate "scoped " -
                                //    by-value ref-like parameters get *no* implicit scoping by default
                                //    (confirmed: a plain `Span<int> value` reports ScopedKind.None, only
                                //    `scoped Span<int> value` reports ScopedValue), so this one is never
                                //    redundant.
                                //  - The *inverse* of the "out" case above: `[UnscopedRef] out` reports
                                //    ScopedKind.None (confirmed) - the attribute removes out's normal
                                //    implicit scoping. A plain generated "out" parameter would still be
                                //    implicitly scoped, disagreeing with the interface's explicitly
                                //    unscoped contract - the UnscopedRefAttribute itself has to be
                                //    restated on the explicit implementation.
                                (p.RefKind == RefKind.Out && p.ScopedKind == ScopedKind.None
                                    ? "[global::System.Diagnostics.CodeAnalysis.UnscopedRef] "
                                    : "") +
                                ((p.ScopedKind == ScopedKind.ScopedRef && p.RefKind != RefKind.Out) ||
                                 p.ScopedKind == ScopedKind.ScopedValue ? "scoped " : "") +
                                (p.RefKind switch
                                {
                                    RefKind.Ref => "ref ",
                                    RefKind.Out => "out ",
                                    RefKind.In => "in ",
                                    // A C# 12 `ref readonly` parameter - distinct from RefKind.RefReadOnly,
                                    // which describes a by-ref-readonly *return*, not a parameter. Omitting
                                    // this case would silently emit the explicit interface implementation
                                    // with no ref modifier at all, producing a signature that doesn't match
                                    // the interface member it's implementing (CS0535).
                                    RefKind.RefReadOnlyParameter => "ref readonly ",
                                    _ => "",
                                }),
                                p.IsParams,
                                // Mirrored onto an overloaded member's own extension so a real
                                // optional-parameter call shape (M() against M(int value = 0)) stays
                                // reachable through Configure() too - same "keep every real call
                                // shape reachable" reasoning already applied to params. Codex review,
                                // PR #88.
                                DefaultValueExpressionFor(p)))
                            .ToEquatableArray();

                        var discriminatorSuffix = hasConfigurationSurface && isOverloaded
                            ? discriminatorSuffixByIdentity[(method.Name, Canonical: identity.Canonical)]
                            : "";

                        // Type parameters flow onto the explicit interface implementation whenever
                        // the method is generic (ADR-0044 Requirement 2). Constraint clauses only
                        // flow onto the generated *extension* - and only when that extension is
                        // itself generic, i.e. an overloaded generic member (Amendment 1) - never onto
                        // the explicit interface implementation, which can't redeclare an inherited
                        // constraint (CS0460, Amendment 2 Finding 2).
                        var typeParameterNames = method.IsGenericMethod
                            ? method.TypeParameters
                                .Select(tp => RequiredMemberCollector.EscapeIdentifier(tp.Name))
                                .ToEquatableArray()
                            : EquatableArray<string>.Empty;

                        // A real overload's own parameter names are never guaranteed to avoid any
                        // particular leading-underscore convention - a real parameter can be named
                        // "self" or even "__self" (RequiredMemberCollector.EscapeIdentifier only
                        // @-escapes reserved keywords, it never renames a leading-underscore
                        // identifier). Only an overloaded, surfaced member's extension carries real
                        // parameters alongside its receiver, so only that case needs a genuinely
                        // collision-checked name. Codex review, PR #88. When the extension is also
                        // generic (an overloaded generic member, Amendment 1), its own type-parameter
                        // names share the same declaration-level identifier space as the receiver
                        // parameter too - a type parameter named "__self" collides with a receiver
                        // literally named "__self" (CS0412) exactly like a value parameter would.
                        // Codex review, PR #89.
                        // ADR-0048: argument-aware Configure()/Verify() is scoped to a member with at
                        // least one real parameter, not part of an overload set (a real compiler spike
                        // proved wrapping every overload's parameters in Match<T> breaks C# overload
                        // resolution unpredictably), and - for a generic method - no real parameter
                        // referencing the method's own open type parameter (a per-member call log can't
                        // hold an open type parameter's value; reuses the same symbol-graph walk
                        // Requirement 2 already uses for its return-type check, just against parameters).
                        // Computed before extensionReceiverName below - an eligible member's own
                        // Configure()/Verify() extension now carries real parameter names too (wrapped
                        // in Match<T>), so it needs the same collision-checked receiver name an
                        // overloaded member's extension already gets. Codex review, PR #106.
                        //
                        // A ref-like parameter type (Span<T>, or any other ref struct) is excluded here,
                        // not merely diagnosed - it dispatches fine today via the argument-independent,
                        // value-discarded path (a ref-like type is a perfectly ordinary by-value method
                        // parameter, distinct from the ref/out/in *passing-mode* restriction above), but
                        // can never be used as a generic type argument (Match<Span<int>>?, or as an
                        // element of the call-log tuple) - CS0306. Falls back to the member's existing
                        // v1/v2 shape, the same "ineligible, not unsupported" disposition every other
                        // eligibility exclusion here already has. Codex review, PR #106.
                        //
                        // A member is also excluded when its own derived field names (the call log/lock/
                        // per-parameter matcher fields the template will splice from FieldName) collide
                        // with any name derived by another candidate, or with a literal top-level field
                        // name - derivedNameCollisionMembers (computed above, before this loop starts)
                        // already accounts for both. Falls back to the argument-independent path for
                        // just this member, the same "give up the enhancement, keep the member correct"
                        // disposition as every other exclusion here. Codex review, PR #106 (round 2).
                        //
                        // A non-overloaded "Equals" member with exactly one parameter is also excluded -
                        // its would-be Match<T>-typed extension has the same real call-site arity as the
                        // inherited object.Equals(object) instance method (any T implicitly converts to
                        // object, boxing if necessary), and C# always prefers an applicable instance
                        // method over an extension method regardless of conversion cost - the generated
                        // extension would never actually be reachable. ToString/GetHashCode/GetType need
                        // no equivalent check here: they collide with their object counterpart only at
                        // arity zero, and eligibility already requires parameters.Count > 0, so an
                        // eligible member named one of those can never share arity with the inherited
                        // zero-arg version. This is a distinct check from isObjectMemberCollisionShaped
                        // above, which computes a non-overloaded member's *existing* (non-matching)
                        // extension arity as always zero - eligibility's own extension arity is
                        // parameters.Count instead, so that check's answer doesn't transfer here. Codex
                        // review, PR #106 (round 2).
                        // ADR-0049: mutually exclusive with isEligibleForMatching - a closed-
                        // instantiation-eligible member's own state (bucket + nested generic-in-T
                        // class) is a different mechanism from ADR-0048's single-slot Match<T> fields,
                        // and the two must never both apply to the same member.
                        var isClosedInstantiationEligible = isClosedInstantiationEligibleShape && hasConfigurationSurface;

                        var isEligibleForMatching = hasConfigurationSurface && !isOverloaded &&
                            !isClosedInstantiationEligible && parameters.Count > 0 &&
                            !(method.IsGenericMethod &&
                              method.Parameters.Any(p => TypeReferencesOwnTypeParameter(p.Type, method))) &&
                            !method.Parameters.Any(p => p.Type.IsRefLikeType) &&
                            !derivedNameCollisionMembers.Contains(method) &&
                            !(method.Name == "Equals" && parameters.Count == 1);

                        var extensionReceiverName = hasConfigurationSurface && (isOverloaded || isEligibleForMatching || isClosedInstantiationEligible)
                            ? SafeReceiverName(parameters.Select(p => p.EscapedName).Concat(typeParameterNames))
                            : "self";

                        var constraintClauses = method.IsGenericMethod && hasConfigurationSurface && (isOverloaded || isClosedInstantiationEligible)
                            ? method.TypeParameters
                                .Select(ConstraintClauseFor)
                                .Where(clause => clause.Length > 0)
                                .ToEquatableArray()
                            : EquatableArray<string>.Empty;

                        if (isConfigurationRequired)
                            configurationRequiredCount++;

                        members.Add(new TestDoubleMemberInfo(
                            method.Name,
                            RequiredMemberCollector.EscapeIdentifier(method.Name),
                            declaringInterfaceFullyQualifiedName,
                            TestDoubleMemberKind.Method,
                            TestDoublePropertyAccessorKind.None,
                            returnTypeFullyQualifiedName,
                            isVoid,
                            defaultExpression,
                            parameters,
                            hasConfigurationSurface,
                            isOverloaded,
                            discriminatorSuffix,
                            outParameterAssignments.ToEquatableArray(),
                            extensionReceiverName,
                            method.IsGenericMethod,
                            typeParameterNames,
                            constraintClauses,
                            IsConfigurationRequired: isConfigurationRequired,
                            IsEligibleForMatching: isEligibleForMatching,
                            IsClosedInstantiationEligible: isClosedInstantiationEligible,
                            IsClosedInstantiationEligibleShape: isClosedInstantiationEligibleShape));

                        break;
                    }

                    case IPropertySymbol property:
                    {
                        if (property.IsStatic)
                        {
                            // A non-abstract static property is a default implementation, not part
                            // of the instance contract a double implements - skip it silently, same
                            // as a non-abstract static method.
                            if (!property.IsAbstract)
                                continue;

                            // ADR-0046: same most-specific-implementation check as the method
                            // branch above - a static abstract property already resolved by a
                            // more-derived interface in the closure isn't part of this double's
                            // unimplemented contract at all. Skipped silently. A genuinely
                            // unresolved static abstract property stays whole-interface rejected,
                            // for the same CS8920-reachability reason the method branch documents.
                            if (interfaceType.FindImplementationForInterfaceMember(property) is not null)
                                continue;

                            return Failure(fullyQualifiedName, safeIdentifier,
                                UnsupportedMember(interfaceType, member, "a static abstract member", location));
                        }

                        // Same reasoning as the method branch above - a non-public default-implemented
                        // property isn't part of any implementing type's contract and can't be
                        // explicitly implemented. PR #83 review round 2.
                        if (!property.IsAbstract && property.DeclaredAccessibility != Accessibility.Public)
                            continue;

                        var identity = (property.Name, Canonical: IdentityFor(property));
                        var isDiamondCollision = diamondCollisionIdentities.Contains(identity);

                        if (isDiamondCollision && reportedDiamondIdentities.Add(identity))
                        {
                            // Call-site-independent, same reasoning as the method-branch
                            // InfoDiagnostics.Add calls above.
                            infoDiagnostics.Add(new DiagnosticInfo(
                                DiagnosticDescriptors.OverloadedTestDoubleMember, null,
                                interfaceType.ToDisplayString(), property.Name, ""));
                        }

                        // A property's own generated extension is always zero-parameter - it collides
                        // with any same-named method whose own extension also ends up zero-parameter
                        // (a non-overloaded method, or a genuinely zero-parameter overload), the exact
                        // same CS0111 risk the method branch's own zero-arg-collision check guards
                        // against. Codex review, PR #88.
                        var isZeroArgCollision = !isDiamondCollision && zeroArgCollisionMembers.Contains(property);

                        if (isZeroArgCollision && reportedZeroArgCollisionNames.Add(property.Name))
                        {
                            infoDiagnostics.Add(new DiagnosticInfo(
                                DiagnosticDescriptors.ZeroArgumentTestDoubleExtensionCollision, null,
                                interfaceType.ToDisplayString(), property.Name));
                        }

                        // Same object-collision rule as the method branch above (properties can't be
                        // overloaded by type, so their generated extension is always zero-argument).
                        // Also guarded on !isZeroArgCollision - a property already withheld for
                        // colliding with a same-named zero-parameter method (CMP0029) has no surface
                        // to collide with object left either; without this guard, this check
                        // redundantly rejected the whole interface even though the method branch's
                        // own object-collision check already correctly skips itself in that case
                        // (guarded by `hasConfigurationSurface`, which zero-arg collision also clears).
                        // Codex review, PR #88.
                        if (!isDiamondCollision && !isZeroArgCollision && property.Name is "ToString" or "GetHashCode" or "GetType")
                        {
                            return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                DiagnosticDescriptors.TestDoubleObjectMemberCollision, location,
                                interfaceType.ToDisplayString(), property.Name));
                        }

                        // A setter with no getter at all: nothing could ever observe a value written
                        // through it, since v1 has no call recording/verification. Amendment 10, Finding W.
                        if (property.GetMethod is null)
                        {
                            return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                DiagnosticDescriptors.SetOnlyTestDoubleProperty, location,
                                interfaceType.ToDisplayString(), property.Name));
                        }

                        if (property.ReturnsByRef || property.ReturnsByRefReadonly)
                        {
                            return Failure(fullyQualifiedName, safeIdentifier,
                                ReturnShapeUnsupported(interfaceType, property, "a by-ref return", location));
                        }

                        if (ContainsPointerType(property.Type))
                        {
                            return Failure(fullyQualifiedName, safeIdentifier,
                                ReturnShapeUnsupported(interfaceType, property, "a pointer or function-pointer type", location));
                        }

                        if (property.Type.IsRefLikeType)
                        {
                            return Failure(fullyQualifiedName, safeIdentifier,
                                ReturnShapeUnsupported(interfaceType, property, "a ref struct (ref-like type)", location));
                        }

                        // ADR-0045 Amendment 7: unlike the method branch, no additional hoisted
                        // predicate is needed here - the object-collision check above already ran
                        // (and would already have returned Failure with CMP0024) before this
                        // return-type check is ever reached, so hasPropertyConfigurationSurface
                        // accurately reflects "no deterministic default AND would otherwise have a
                        // real surface" without checking for object-collision again.
                        var hasPropertyConfigurationSurface = !isDiamondCollision && !isZeroArgCollision;
                        var isPropertyConfigurationRequired = false;
                        var propertyDefault = "";

                        if (!TestDoubleDefaults.TryGetDefaultExpression(property.Type, compilation, out propertyDefault))
                        {
                            if (hasPropertyConfigurationSurface)
                            {
                                isPropertyConfigurationRequired = true;
                            }
                            else
                            {
                                return Failure(fullyQualifiedName, safeIdentifier, ReturnShapeUnsupported(
                                    interfaceType, property, "a non-nullable reference type with no deterministic default", location));
                            }
                        }

                        // set vs. init are non-interchangeable - the write accessor emitted must match
                        // exactly what the interface declares. Amendment 9, Finding U. A setter that
                        // exists but isn't public (a default-implemented `private set` alongside a
                        // default-implemented `get`, e.g. `int Value { get => 0; private set { ... } }`)
                        // is, like a private default method, not part of the implementable contract -
                        // explicitly implementing it is invalid, so it's treated the same as no setter
                        // at all rather than selecting GetSet. PR #83 review round 5.
                        var accessorKind = property.SetMethod is not { DeclaredAccessibility: Accessibility.Public }
                            ? TestDoublePropertyAccessorKind.GetOnly
                            : property.SetMethod.IsInitOnly
                                ? TestDoublePropertyAccessorKind.GetInit
                                : TestDoublePropertyAccessorKind.GetSet;

                        if (isPropertyConfigurationRequired)
                            configurationRequiredCount++;

                        members.Add(new TestDoubleMemberInfo(
                            property.Name,
                            RequiredMemberCollector.EscapeIdentifier(property.Name),
                            declaringInterfaceFullyQualifiedName,
                            TestDoubleMemberKind.Property,
                            accessorKind,
                            property.Type.ToDisplayString(TestDoubleDefaults.NullableAwareFullyQualifiedFormat),
                            false,
                            propertyDefault,
                            EquatableArray<TestDoubleParameterInfo>.Empty,
                            hasPropertyConfigurationSurface,
                            false,
                            "",
                            IsConfigurationRequired: isPropertyConfigurationRequired));

                        break;
                    }
                }
            }
        }

        // ADR-0045 Amendment 1: one CMP0032 per interface (a count), not one per member - avoids
        // diagnostic-noise blowup on a large real-world interface (IAmazonS3-shaped) where many
        // members might require configuration. The exact member identity is already reported
        // precisely by TestDoubleNotConfiguredException at the point an unconfigured member is
        // actually invoked, so this diagnostic doesn't need to enumerate members by name.
        if (configurationRequiredCount > 0)
        {
            infoDiagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.TestDoubleMemberRequiresConfiguration, null,
                interfaceType.ToDisplayString(), configurationRequiredCount.ToString()));
        }

        return new DiscoveredTestDoubleInfo(
            fullyQualifiedName, safeIdentifier, members.ToEquatableArray(), EquatableArray<DiagnosticInfo>.Empty,
            infoDiagnostics.ToEquatableArray());
    }

    private static bool WouldGetConfigurationSurface(
        ISymbol member, HashSet<(string Name, string Canonical)> diamondCollisionIdentities)
    {
        if (diamondCollisionIdentities.Contains((member.Name, Canonical: IdentityFor(member))))
            return false;

        return member is not IMethodSymbol method || !method.Parameters.Any(p => p.RefKind != RefKind.None);
    }

    // Genuinely collision-proof against this specific overload's own real (escaped) parameter names -
    // unlike a fixed "__self" (which a real parameter can still be named, since EscapeIdentifier only
    // @-escapes reserved keywords, never a leading-underscore identifier), this keeps lengthening the
    // candidate until it isn't one of the real parameter names. Terminates because
    // escapedParameterNames is finite and each iteration strictly lengthens the candidate. Codex
    // review, PR #88.
    private static string SafeReceiverName(IEnumerable<string> escapedParameterNames)
    {
        var used = new HashSet<string>(escapedParameterNames, StringComparer.Ordinal);
        var candidate = "__self";

        while (used.Contains(candidate))
            candidate = "_" + candidate;

        return candidate;
    }

    // Checked recursively through array element types - `int*[]` (an array of pointers) has
    // TypeKind.Array at the top level, not Pointer, so a top-level-only check silently accepts it,
    // emitting both the explicit implementation and (for an overloaded member) a discriminator
    // extension containing a pointer type with no `unsafe` context (CS0214 in the consumer). No
    // other nesting shape can hide a pointer type - C#'s CS0306 already forbids a pointer as a
    // generic type argument (so it can never hide inside a constructed generic type or a tuple
    // element), only an array of them is legal. Codex review, PR #88.
    private static bool ContainsPointerType(ITypeSymbol type)
    {
        while (type is IArrayTypeSymbol array)
            type = array.ElementType;

        return type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer;
    }

    // Walks a type's full symbol graph (generic type arguments, array element types, pointed-at
    // types, recursively) looking for a reference to one of the owning method's own type parameters -
    // deliberately symbol-based, not syntax-based, since a metadata-defined interface (the real
    // ILogger<T> from a referenced assembly) has no syntax tree in the consumer's own compilation at
    // all. ADR-0044 Requirement 2 / Amendment 13.
    private static bool TypeReferencesOwnTypeParameter(ITypeSymbol type, IMethodSymbol method)
    {
        if (type is ITypeParameterSymbol typeParameter)
            return method.TypeParameters.Any(tp => SymbolEqualityComparer.Default.Equals(tp, typeParameter));

        if (type is IArrayTypeSymbol array)
            return TypeReferencesOwnTypeParameter(array.ElementType, method);

        if (type is IPointerTypeSymbol pointer)
            return TypeReferencesOwnTypeParameter(pointer.PointedAtType, method);

        if (type is INamedTypeSymbol named)
            return named.TypeArguments.Any(argument => TypeReferencesOwnTypeParameter(argument, method));

        return false;
    }

    // ADR-0049: the narrower, still-supported subset of "a generic method's return type references
    // its own type parameter" - exactly T itself, or the sole type argument of Task<T>/Task<T?>/
    // ValueTask<T>/ValueTask<T?>, for a method with exactly one type parameter (this ADR's own scope
    // boundary - a multi-type-parameter self-referencing return stays whole-interface-rejected,
    // unevidenced). Deliberately an equality check against the method's own single type parameter,
    // not a reuse of TypeReferencesOwnTypeParameter's recursive walk - that walk would also match
    // Task<List<T>> (T nested deeper), which this ADR's own scope explicitly excludes ("Considered
    // Options," Scope boundary). SymbolEqualityComparer.Default ignores nullable annotation, so this
    // matches both T/Task<T> and T?/Task<T?> alike - the nullable-vs-not distinction is handled
    // downstream by the existing TestDoubleDefaults.TryGetDefaultExpression/ADR-0045 dispatch-branch
    // logic, unchanged.
    //
    // Excludes a type parameter declared `where T : allows ref struct` (C# 13's ref-like-capable
    // anti-constraint, already handled elsewhere in this file for the ordinary generic-constraint-
    // restatement case - see ConstraintClauseFor) - a legal interface member like
    // `T Create<T>() where T : allows ref struct` would otherwise pass this check (T is neither
    // ref-like *itself* as a symbol nor caught by any existing ref-like-parameter guard, since this
    // is a constraint on the type parameter, not a parameter type), but the generated state class's
    // `ReturnConfig<T>`/`ReturnConfigBuilder<T>` fields (Compono's existing, unmodified runtime types)
    // declare no `allows ref struct` on their own T - so a real caller closing this method's T over
    // an actual ref struct would fail to compile with CS9244 inside generated code, not the clean
    // CMP0031 whole-interface-fallback diagnostic every other unsupported shape gets. Codex review,
    // PR #107.
    private static bool IsClosedInstantiationEligibleReturnShape(ITypeSymbol returnType, IMethodSymbol method, Compilation compilation)
    {
        if (method.TypeParameters.Length != 1)
            return false;

        var typeParameter = method.TypeParameters[0];

        if (typeParameter.AllowsRefLikeType)
            return false;

        // ADR-0049 Amendment 1: a nullable-annotated matched position (`T?`) requires `T` constrained
        // to a reference type (`where T : class`) - unevidenced otherwise. SymbolEqualityComparer
        // ignores nullable annotation, so the two equality checks below match `T?`/`Task<T?>`/
        // `ValueTask<T?>` exactly like their non-nullable counterparts; without this extra check, an
        // UNCONSTRAINED `T` used as `T?` (C# legally allows this - unconstrained `T?` stays the same
        // symbol T with an Annotated nullable flag, unlike a value-type-constrained `T?`, which Roslyn
        // represents as the distinct type System.Nullable<T> and which the equality check already
        // rejects on its own) would silently pass and ship the same unevidenced capability round 8's
        // Amendment 1 named and declined for `where T : struct` - the real trivia-platform evidence
        // this ADR cites is exclusively `where T : class`. Codex review, PR #107 (round 9).
        var isNullableAnnotated = returnType.NullableAnnotation == NullableAnnotation.Annotated;

        if (isNullableAnnotated && !typeParameter.HasReferenceTypeConstraint)
            return false;

        if (SymbolEqualityComparer.Default.Equals(returnType, typeParameter))
            return true;

        // Compared against the real BCL Task<T>/ValueTask<T> symbols by identity
        // (TaskWellKnownTypes), not by namespace/simple-name/nesting - round 4's own
        // "ContainingType is null" fix only ruled out a *nested* impostor (a consumer's own
        // System.Threading.Tasks.Container.Task<T>); it still let a genuinely *top-level* consumer
        // type also named "Task<T>", reopening the same namespace, through unchanged - namespace
        // reopening is legal C#, and ContainingType is null for a top-level type regardless of which
        // assembly declares it. Either impostor shape, left unfixed, would let TestDoubleDefaults go
        // on to emit a real global::System.Threading.Tasks.Task.FromResult<T>(...)/
        // new global::System.Threading.Tasks.ValueTask<T>(...) default-value expression for a member
        // whose actual declared return type is the consumer's unrelated type - a genuine type-
        // mismatch compile error in generated code instead of the clean CMP0031 whole-interface
        // fallback this shape should get. Codex review, PR #107 (round 5).
        if (returnType is INamedTypeSymbol { TypeArguments.Length: 1 } named &&
            TaskWellKnownTypes.GetOrCreate(compilation).IsTaskOfTOrValueTaskOfT(named))
        {
            var typeArgumentIsNullableAnnotated = named.TypeArguments[0].NullableAnnotation == NullableAnnotation.Annotated;

            if (typeArgumentIsNullableAnnotated && !typeParameter.HasReferenceTypeConstraint)
                return false;

            if (SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], typeParameter))
                return true;
        }

        return false;
    }

    // ADR-0049 / PR #107 Codex review round 3: the pure shape test for "closed-instantiation
    // eligible," independent of hasConfigurationSurface/derivedNameCollisionMembers (both of which
    // are themselves computed FROM this test at various points, so a candidate this test applies to
    // can't first check either of them without circularity). Every pre-pass that needs to know this
    // ahead of the main per-member loop (the zeroArgExtensionSharers and derivedAuxiliaryNameOwners
    // collision-detection passes below, and the main loop's own isClosedInstantiationEligibleShape)
    // now shares this one definition - round 3's two findings were both a direct consequence of an
    // earlier pre-pass having its OWN inline copy of this test (or, for derivedAuxiliaryNameOwners,
    // no awareness of it at all) that silently fell out of sync with the real eligibility rule.
    private static bool IsClosedInstantiationEligibleCandidate(IMethodSymbol method, Compilation compilation) =>
        method.IsGenericMethod &&
        TypeReferencesOwnTypeParameter(method.ReturnType, method) &&
        IsClosedInstantiationEligibleReturnShape(method.ReturnType, method, compilation) &&
        !method.Parameters.Any(p => p.Type.IsRefLikeType) &&
        !method.Parameters.Any(p => TypeReferencesOwnTypeParameter(p.Type, method));

    // Same symbol-graph walk as TypeReferencesOwnTypeParameter, but looking specifically for a
    // nullable-annotated (`T?`) reference to one of the owning method's own type parameters -
    // constrained or unconstrained, regardless of which constraint (ADR-0044 Amendment 9, which
    // withdrew Amendment 8's narrower constrained-only exception after a second review round
    // disputed the exact permitted keyword set C# allows restating on the explicit interface
    // implementation for that case - two rounds disagreeing with each other is itself strong
    // evidence against guessing a third time). A constrained `T?` (`where T : class`, `where T :
    // struct`, ...) may genuinely be supportable with the exact right syntax, but this ADR has no
    // verified answer for it, so every `T?`-using type parameter is diagnosed and excluded alike.
    // Codex review, PR #89.
    private static bool ContainsNullableOwnTypeParameter(ITypeSymbol type, IMethodSymbol method)
    {
        if (type is ITypeParameterSymbol typeParameter)
        {
            if (type.NullableAnnotation != NullableAnnotation.Annotated)
                return false;

            return method.TypeParameters.Any(tp => SymbolEqualityComparer.Default.Equals(tp, typeParameter));
        }

        if (type is IArrayTypeSymbol array)
            return ContainsNullableOwnTypeParameter(array.ElementType, method);

        if (type is INamedTypeSymbol named)
            return named.TypeArguments.Any(argument => ContainsNullableOwnTypeParameter(argument, method));

        return false;
    }

    // Full "where T : ..." clause text for one type parameter, verbatim - only ever spliced onto a
    // generated generic *extension* method (an overloaded generic member, ADR-0044 Amendment 1),
    // never onto the explicit interface implementation (CS0460, Amendment 2 Finding 2). Primary
    // constraint first (mutually exclusive: unmanaged supersedes struct, which is otherwise
    // implied), then each constraint type, then the constructor constraint, then the C# 13
    // `allows ref struct` anti-constraint last - the only order C# itself accepts. Without it, a
    // real interface method declared `where T : allows ref struct` would let a caller close T over
    // a ref-like type (Span<int>) at the real call site, but the generated extension - missing the
    // anti-constraint - would reject that same call (CS8377), silently narrowing what the real
    // member permits. Empty when the type parameter has no constraint at all. Codex review, PR #89.
    private static string ConstraintClauseFor(ITypeParameterSymbol typeParameter)
    {
        var parts = new List<string>();

        if (typeParameter.HasUnmanagedTypeConstraint)
            parts.Add("unmanaged");
        else if (typeParameter.HasValueTypeConstraint)
            parts.Add("struct");
        else if (typeParameter.HasReferenceTypeConstraint)
            parts.Add(typeParameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
        else if (typeParameter.HasNotNullConstraint)
            parts.Add("notnull");

        parts.AddRange(typeParameter.ConstraintTypes.Select(
            constraintType => constraintType.ToDisplayString(TestDoubleDefaults.NullableAwareFullyQualifiedFormat)));

        if (typeParameter.HasConstructorConstraint)
            parts.Add("new()");

        if (typeParameter.AllowsRefLikeType)
            parts.Add("allows ref struct");

        return parts.Count == 0
            ? ""
            : $"where {RequiredMemberCollector.EscapeIdentifier(typeParameter.Name)} : {string.Join(", ", parts)}";
    }

    // A C# literal expression for a parameter's optional default value, only ever rendered onto an
    // overloaded member's generated extension (never the explicit interface implementation, which
    // can't usefully redeclare one - callers always go through the interface's own default, not the
    // implementation's). `null` covers both a reference-type `= null` default and a non-primitive
    // value-type `= default` default - `default` is a valid literal for either.
    private static string DefaultValueExpressionFor(IParameterSymbol parameter)
    {
        if (!parameter.IsOptional)
            return "";

        // A parameter can be optional purely via [Optional] (common on metadata imported from
        // COM-flavored or VB-compiled assemblies) with no explicit default value at all -
        // HasExplicitDefaultValue is false but IsOptional is still true. Verified with a real compile
        // spike that the real interface still allows omitting the argument entirely (the compiler
        // substitutes default(T) for the caller) - "default" mirrors that substitution exactly, same
        // as the null-default case below. Codex review, PR #88.
        if (!parameter.HasExplicitDefaultValue)
            return "default";

        var value = parameter.ExplicitDefaultValue;

        if (value is null)
            return "default";

        // An enum-typed default is exposed as its boxed *underlying* numeric value, not the enum
        // member itself (e.g. `Mode mode = Mode.Active` surfaces as the boxed int 1) - emitting that
        // raw primitive directly (`Mode mode = 1`) fails consumer compilation (CS1750, no standard
        // conversion from int to Mode). A cast to the underlying enum type is a legal constant
        // default-parameter-value expression regardless of which member (if any) the value names -
        // verified with a real compile spike, including that a cast to the non-nullable enum type
        // (not the nullable wrapper) is what's needed for a `Mode?`-typed parameter, since `(Mode)1`
        // converts implicitly to `Mode?` in this context. `Nullable<T>` has to be unwrapped first -
        // parameter.Type.TypeKind is Struct, not Enum, for a nullable-enum-typed parameter, so the
        // unguarded check missed this shape entirely. Codex review, PR #88.
        var formatted = SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false) ?? "default";
        var underlyingType = parameter.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : parameter.Type;

        return underlyingType.TypeKind == TypeKind.Enum
            ? $"({underlyingType.ToDisplayString(TestDoubleDefaults.NullableAwareFullyQualifiedFormat)}){formatted}"
            : formatted;
    }

    // The full canonical signature text - never the hash - for any identity/equality decision
    // (diamond-collision grouping, zero-arg-extension-collision grouping). A 32-bit hash can collide
    // between two genuinely different signatures; the discriminator-suffix pre-pass above is the only
    // place allowed to hash this text (TestDoubleOverloadIdentity.StableHash), and only with its own
    // collision-disambiguation fallback. Codex review, PR #88.
    private static string IdentityFor(ISymbol member) => member switch
    {
        IMethodSymbol method => TestDoubleOverloadIdentity.CanonicalSignatureFor(method),
        IPropertySymbol property => TestDoubleOverloadIdentity.CanonicalSignatureFor(property),
        _ => "",
    };

    private static DiscoveredTestDoubleInfo Failure(string fullyQualifiedName, string safeIdentifier, DiagnosticInfo diagnostic) =>
        new(fullyQualifiedName, safeIdentifier, EquatableArray<TestDoubleMemberInfo>.Empty, new[] { diagnostic }.ToEquatableArray());

    // Every parameter must either have a default value, or be the trailing `params` parameter (an
    // empty array is a valid argument for it) - the same rule the C# compiler itself applies when
    // deciding whether a zero-argument call is applicable to a method. PR #83 review round 4.
    // A generic method is never applicable to an implicit (no explicit type argument) zero-argument
    // call - there's nothing for the compiler to infer its type parameter(s) from. ADR-0044
    // Amendment 14, corrected by Amendment 16 to key off the *generated extension's* own genericity
    // rather than the real member's - callers of this helper already only ever check it against a
    // real interface member for the Configure()/Verify() bridge-collision check, which is unaffected
    // by overloading, so the real member's own genericity is the right thing to check here.
    private static bool IsApplicableToZeroArguments(IMethodSymbol method)
    {
        if (method.IsGenericMethod)
            return false;

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var parameter = method.Parameters[i];
            var isLast = i == method.Parameters.Length - 1;

            if (!parameter.IsOptional && !(isLast && parameter.IsParams))
                return false;
        }

        return true;
    }

    private static DiagnosticInfo UnsupportedMember(INamedTypeSymbol interfaceType, ISymbol member, string shape, LocationInfo? location) =>
        new(DiagnosticDescriptors.UnsupportedTestDoubleMemberKind, location, interfaceType.ToDisplayString(), member.Name, shape);

    private static DiagnosticInfo ReturnShapeUnsupported(INamedTypeSymbol interfaceType, ISymbol member, string shape, LocationInfo? location) =>
        new(DiagnosticDescriptors.UnsupportedTestDoubleReturnShape, location, interfaceType.ToDisplayString(), member.Name, shape);
}
