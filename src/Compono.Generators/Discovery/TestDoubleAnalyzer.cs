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

        // A member literally named "Configure" only actually shadows the generated, always-zero-
        // argument Configure() bridge extension when it's itself applicable to a zero-argument call -
        // a property/field/event named Configure always collides (member lookup finds it and never
        // falls back to extension methods for that name at all), but a *method* named Configure does
        // so only if a zero-argument call is actually applicable to it: C# only falls back to
        // extension-method resolution when ordinary member lookup finds no *applicable* candidate, not
        // merely "no candidate with this name" - verified directly with a real compile spike (an
        // explicitly-implemented `IFoo.Configure(int mode)` alongside a zero-argument
        // `Configure(this IFoo)` extension: calling `foo.Configure()` on an IFoo-typed receiver
        // resolves to the extension without ambiguity). Amendment 3, Finding E; corrected to compare
        // arity, not just name, PR #83 review round 2 - and corrected again to check *applicability*,
        // not raw parameter count, PR #83 review round 4: `Configure(int mode = 0)` and
        // `Configure(params int[] modes)` both have Parameters.Length > 0 but are still applicable to
        // a zero-argument call, so they collide exactly like a genuinely zero-parameter method does.
        // ADR-0044 Amendment 14: a *generic* Configure<T>() interface member has nothing to infer T
        // from at a bare, no-explicit-type-argument call, so it's never applicable to zero arguments
        // either, and doesn't collide - IsApplicableToZeroArguments itself now excludes any generic
        // method.
        if (closure.SelectMany(i => i.GetMembers())
            .Any(m => m.Name == "Configure" && (m is not IMethodSymbol method || IsApplicableToZeroArguments(method))))
        {
            return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                DiagnosticDescriptors.TestDoubleConfigureMemberCollision, location, interfaceType.ToDisplayString()));
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
        var eligibleCandidates = closure
            .SelectMany(i => i.GetMembers())
            .Where(m => m is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol { IsIndexer: false })
            .Where(m => m.IsAbstract || (!m.IsStatic && m.DeclaredAccessibility == Accessibility.Public))
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
        // end up with a genuinely zero-parameter generated extension, the two extensions are the
        // exact same C# signature (`Foo(this Double)`), a duplicate-declaration compile error
        // (CS0111). A property's extension is always zero-parameter; a method's is zero-parameter
        // unless it's part of a real (surfaced) overload set, in which case its own real parameter
        // list disambiguates it instead. Codex review, PR #88.
        var zeroArgExtensionSharers = new Dictionary<string, List<ISymbol>>();

        foreach (var candidate in eligibleCandidates)
        {
            if (diamondCollisionIdentities.Contains((candidate.Name, Canonical: IdentityFor(candidate))))
                continue;

            int effectiveArity;

            if (candidate is IPropertySymbol)
            {
                effectiveArity = 0;
            }
            else if (candidate is IMethodSymbol candidateMethod)
            {
                if (candidateMethod.Parameters.Any(p => p.RefKind != RefKind.None))
                    continue;

                effectiveArity = overloadedNames.Contains(candidateMethod.Name) ? candidateMethod.Parameters.Length : 0;
            }
            else
            {
                continue;
            }

            if (effectiveArity != 0)
                continue;

            if (!zeroArgExtensionSharers.TryGetValue(candidate.Name, out var sharers))
                zeroArgExtensionSharers[candidate.Name] = sharers = new List<ISymbol>();

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

        foreach (var candidate in eligibleCandidates)
        {
            if (!overloadedNames.Contains(candidate.Name))
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

        var reportedDiamondIdentities = new HashSet<(string Name, string Canonical)>();
        var reportedZeroArgCollisionNames = new HashSet<string>();
        var members = new List<TestDoubleMemberInfo>();
        var infoDiagnostics = new List<DiagnosticInfo>();

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
                    }:
                        return Failure(fullyQualifiedName, safeIdentifier,
                            UnsupportedMember(interfaceType, member, "a static abstract member", location));

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

                        // ADR-0044 Requirement 2: a generic method is supported only when its return
                        // type doesn't reference its own type parameter(s) anywhere in its symbol
                        // graph (generic type arguments, array element types, recursively) - a
                        // syntax-tree check would silently miss a metadata-defined interface like the
                        // real ILogger<T> (no syntax tree in the consumer's own compilation at all).
                        // A return type that does depend on its own type parameter has no
                        // constructible body at any granularity (Amendment 13) - the same
                        // no-constructible-body bucket a non-nullable-no-default return already
                        // occupies, so it triggers whole-interface rejection, not member-scoped
                        // exclusion.
                        if (method.IsGenericMethod && TypeReferencesOwnTypeParameter(method.ReturnType, method))
                        {
                            return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                DiagnosticDescriptors.UnsupportedTestDoubleGenericReturnShape, location,
                                interfaceType.ToDisplayString(), method.Name));
                        }

                        // Amendment 6 Finding 15: an unconstrained type parameter used as `T?` in a
                        // parameter can require a C# 9+ "default constraint" on the explicit
                        // implementation to disambiguate its inherited, oblivious reference-or-value-
                        // type meaning - correctly modeling exactly when that constraint is *required*
                        // (vs. merely permitted, or unnecessary) isn't something this ADR has a
                        // verified answer for, so this shape is diagnosed and excluded rather than
                        // guessed at. A type parameter with any constraint (class/class?/struct/
                        // unmanaged/notnull) is unaffected - only genuinely unconstrained.
                        if (method.IsGenericMethod)
                        {
                            var unconstrainedNullableParameter = method.Parameters.FirstOrDefault(
                                p => ContainsUnconstrainedNullableTypeParameter(p.Type, method));

                            if (unconstrainedNullableParameter is not null)
                            {
                                return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                    DiagnosticDescriptors.UnsupportedTestDoubleParameterShape, location,
                                    interfaceType.ToDisplayString(), method.Name, unconstrainedNullableParameter.Name,
                                    "as an unconstrained generic type parameter used with a nullable annotation (T?), " +
                                    "which may require a `default` constraint Compono cannot correctly determine"));
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

                        if (!isVoid)
                        {
                            returnTypeFullyQualifiedName = method.ReturnType.ToDisplayString(TestDoubleDefaults.NullableAwareFullyQualifiedFormat);

                            if (!TestDoubleDefaults.TryGetDefaultExpression(method.ReturnType, out defaultExpression))
                            {
                                return Failure(fullyQualifiedName, safeIdentifier, ReturnShapeUnsupported(
                                    interfaceType, method, "a non-nullable reference type with no deterministic default", location));
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

                                if (!TestDoubleDefaults.TryGetDefaultExpression(parameter.Type, out var outDefault))
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
                        if (hasConfigurationSurface && !(method.IsGenericMethod && isOverloaded))
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

                        // A real overload's own parameter names are never guaranteed to avoid any
                        // particular leading-underscore convention - a real parameter can be named
                        // "self" or even "__self" (RequiredMemberCollector.EscapeIdentifier only
                        // @-escapes reserved keywords, it never renames a leading-underscore
                        // identifier). Only an overloaded, surfaced member's extension carries real
                        // parameters alongside its receiver, so only that case needs a genuinely
                        // collision-checked name. Codex review, PR #88.
                        var extensionReceiverName = hasConfigurationSurface && isOverloaded
                            ? SafeReceiverName(parameters.Select(p => p.EscapedName))
                            : "self";

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

                        var constraintClauses = method.IsGenericMethod && hasConfigurationSurface && isOverloaded
                            ? method.TypeParameters
                                .Select(ConstraintClauseFor)
                                .Where(clause => clause.Length > 0)
                                .ToEquatableArray()
                            : EquatableArray<string>.Empty;

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
                            constraintClauses));

                        break;
                    }

                    case IPropertySymbol property:
                    {
                        if (property.IsStatic)
                        {
                            // A non-abstract static property is a default implementation, not part
                            // of the instance contract a double implements - skip it silently, same
                            // as a non-abstract static method. A static *abstract* property, though,
                            // is exactly as unsupported as a static abstract method or operator - it
                            // was previously skipped unconditionally here, which left the double
                            // failing to implement it (CS0535) instead of getting this diagnostic.
                            if (property.IsAbstract)
                            {
                                return Failure(fullyQualifiedName, safeIdentifier,
                                    UnsupportedMember(interfaceType, member, "a static abstract member", location));
                            }

                            continue;
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

                        if (!TestDoubleDefaults.TryGetDefaultExpression(property.Type, out var propertyDefault))
                        {
                            return Failure(fullyQualifiedName, safeIdentifier, ReturnShapeUnsupported(
                                interfaceType, property, "a non-nullable reference type with no deterministic default", location));
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
                            !isDiamondCollision && !isZeroArgCollision,
                            false,
                            ""));

                        break;
                    }
                }
            }
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

    // Same symbol-graph walk as TypeReferencesOwnTypeParameter, but looking specifically for a
    // nullable-annotated (`T?`) reference to an unconstrained one of the owning method's own type
    // parameters (ADR-0044 Amendment 6 Finding 15) - a type parameter with any constraint
    // (class/class?/struct/unmanaged/notnull) already has a well-defined `T?` meaning and isn't
    // affected.
    private static bool ContainsUnconstrainedNullableTypeParameter(ITypeSymbol type, IMethodSymbol method)
    {
        if (type is ITypeParameterSymbol typeParameter)
        {
            if (type.NullableAnnotation != NullableAnnotation.Annotated)
                return false;

            if (!method.TypeParameters.Any(tp => SymbolEqualityComparer.Default.Equals(tp, typeParameter)))
                return false;

            return !typeParameter.HasReferenceTypeConstraint && !typeParameter.HasValueTypeConstraint &&
                   !typeParameter.HasUnmanagedTypeConstraint && !typeParameter.HasNotNullConstraint;
        }

        if (type is IArrayTypeSymbol array)
            return ContainsUnconstrainedNullableTypeParameter(array.ElementType, method);

        if (type is INamedTypeSymbol named)
            return named.TypeArguments.Any(argument => ContainsUnconstrainedNullableTypeParameter(argument, method));

        return false;
    }

    // Full "where T : ..." clause text for one type parameter, verbatim - only ever spliced onto a
    // generated generic *extension* method (an overloaded generic member, ADR-0044 Amendment 1),
    // never onto the explicit interface implementation (CS0460, Amendment 2 Finding 2). Primary
    // constraint first (mutually exclusive: unmanaged supersedes struct, which is otherwise
    // implied), then each constraint type, then the constructor constraint last - the only order C#
    // itself accepts. Empty when the type parameter has no constraint at all.
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
