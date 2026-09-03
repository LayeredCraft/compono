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
                // CallLogAccessExpression's own precedent below. Codex review, PR #106. Also reserved
                // against the method's own type parameter names (a generic eligible member's type
                // parameter is in scope in its dispatch body exactly like a real value parameter is,
                // so it can collide with a synthetic local the same way - Codex review, PR #106,
                // round 4).
                var parameterEscapedNames = m.Parameters.Select(p => p.EscapedName)
                    .Concat(m.TypeParameterNames)
                    .ToArray();
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

                // ADR-0049: a per-closed-T bucket lookup local, reserved the same collision-safe way
                // as every other synthetic local above - a real closed-instantiation-eligible member's
                // own real parameter (or type parameter) can theoretically be named "__bucket".
                var bucketLocalName = SafeLocalName("__bucket", parameterEscapedNames.Append(matchesLocalName));

                // ADR-0049 / PR #107 Codex review: keyed off IsClosedInstantiationEligibleShape, NOT
                // IsClosedInstantiationEligible - the nullable-annotation-stripping fix below is needed
                // for the explicit interface implementation's return-type spelling on EVERY member
                // matching this shape, including one that ends up with no configuration surface at all
                // (a diamond collision, a zero-argument-extension collision, or a ref/out/in overload-
                // set-internal fallback, ADR-0044 Amendment 5) - that member still emits a real explicit
                // implementation (a deterministic-default-only fallback body), and it hits the exact
                // same CS9334/CS0453 cascade if its return type isn't spelled the same stripped way.
                // See TestDoubleMemberInfo.IsClosedInstantiationEligibleShape's own XML doc.
                var closedInstantiationTypeParameterName = m.IsClosedInstantiationEligibleShape ? m.TypeParameterNames[0] : "";
                var closedInstantiationExplicitImplementationReturnType = m.IsClosedInstantiationEligibleShape
                    ? m.ReturnTypeFullyQualifiedName.Replace($"{closedInstantiationTypeParameterName}?", closedInstantiationTypeParameterName)
                    : m.ReturnTypeFullyQualifiedName;

                // ADR-0049: the bucket lookup method's own local ("boxed" in the template) - reserved
                // collision-safely against the method's own type parameter, the only other identifier
                // in scope (the bucket method has no real value parameters). Codex review, PR #107
                // (round 9): `T Create<boxed>()` otherwise produced `out var boxed` inside a method
                // whose own type parameter is *also* literally named "boxed" - CS0412 ("a local
                // variable named 'boxed' cannot be declared... that name is used... to denote a type
                // parameter"). Computed in C#, not hardcoded in the template, matching every other
                // synthetic local's precedent above.
                var boxedLocalName = SafeLocalName("__boxed", new[] { closedInstantiationTypeParameterName });

                // ADR-0050: multi-entry response configuration - the reverse-scan loop's
                // own locals, same collision-safe allocation as every other synthetic local above.
                var entryLocalName = SafeLocalName("__entry", parameterEscapedNames.Append(matchesLocalName));
                var entryIndexLocalName = SafeLocalName("__i", parameterEscapedNames.Append(entryLocalName));
                var callbackLocalName = SafeLocalName("__callback", parameterEscapedNames.Append(entryLocalName));
                var configuredCallbackLocalName = SafeLocalName(
                    "configuredCallback",
                    parameterEscapedNames.Append(entryLocalName).Append(callbackLocalName));
                var callbackPatternLocalName = SafeLocalName(
                    "callback",
                    parameterEscapedNames.Append(entryLocalName).Append(callbackLocalName).Append(configuredCallbackLocalName));

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
                    m.IsOverloadMatchingEligible,
                    m.MatchingMemberName,
                    IsCallbackEligible = m.Kind == TestDoubleMemberKind.Method && !m.IsVoid &&
                        m.HasConfigurationSurface && (!m.IsGenericMethod || m.IsClosedInstantiationEligible),
                    CallbackDelegateName = m.CallbackDelegateName,
                    CallbackBuilderName = m.CallbackBuilderName,
                    CallbackFieldName = m.CallbackFieldName,
                    CallbackLocalName = callbackLocalName,
                    ConfiguredCallbackLocalName = configuredCallbackLocalName,
                    CallbackPatternLocalName = callbackPatternLocalName,
                    m.IsClosedInstantiationEligible,
                    m.ExtensionReceiverName,
                    m.GenericSuffix,
                    m.ExtensionIsGeneric,
                    m.ConstraintClausesText,
                    m.OriginalName,
                    m.IsForwarding,
                    m.ForwardsToInterfaceFullyQualifiedName,
                    m.IsDimFallbackTarget,
                    DimFallbackHelperClassName = m.DimFallbackHelperClassName,
                    DimFallbackSiblings = m.DimFallbackSiblings
                        .Select(s => new
                        {
                            s.EscapedName,
                            s.DeclaringInterfaceFullyQualifiedName,
                            Kind = s.Kind.ToString(),
                            AccessorKind = s.AccessorKind.ToString(),
                            s.ReturnTypeFullyQualifiedName,
                            s.IsVoid,
                            s.GenericSuffix,
                            Parameters = s.Parameters
                                .Select(p => new { p.EscapedName, p.FullyQualifiedTypeName, p.RefKindPrefix, p.CallSiteRefKindPrefix })
                                .ToArray(),
                        })
                        .ToArray(),
                    Kind = m.Kind.ToString(),
                    AccessorKind = m.AccessorKind.ToString(),
                    MatchesLocalName = matchesLocalName,
                    CountLocalName = countLocalName,
                    CallLoopVariableName = callLoopVariableName,
                    // ADR-0049: the closed-instantiation-eligible member's own generated nested state
                    // class/bucket names (TestDoubleMemberInfo's own derived-name properties, computed
                    // from FieldName the same collision-safe way FieldName itself is already reserved),
                    // its single method type parameter's escaped name (its return type's own text
                    // already carries this name literally - see TestDoubleMemberInfo.SlotTypeFullyQualifiedName's
                    // XML doc), the bucket lookup local, and whether it has any real (non-T) parameter
                    // to match/track at all (a zero-real-parameter closed-instantiation-eligible member
                    // still gets independent per-closed-T state, just no matcher fields/call log - Verify()
                    // reads ReturnConfig<T>.ConfiguredCallCount directly instead, mirroring the plain,
                    // non-matching-eligible member's own "compatibility" Verify() shape).
                    ClosedInstantiationStateClassName = m.ClosedInstantiationStateClassName,
                    ClosedInstantiationBucketFieldName = m.ClosedInstantiationBucketFieldName,
                    ClosedInstantiationBucketMethodName = m.ClosedInstantiationBucketMethodName,
                    ClosedInstantiationTypeParameterName = closedInstantiationTypeParameterName,
                    ClosedInstantiationHasParameters = m.IsClosedInstantiationEligible && m.Parameters.Count > 0,
                    // Narrower than ClosedInstantiationHasParameters above: an *overloaded* closed-
                    // instantiation-eligible member's Configure<T>()/Verify<T>() never sets a matcher
                    // (it uses the real-parameter-discriminator shape, no Match<TParam> wrapping - see
                    // the is_overloaded branch of both extensions below), so its state class must not
                    // declare Matcher_*/Calls/Lock fields nobody ever writes (an unused-field warning,
                    // CS0649) and its dispatch must not run a matcher-evaluation loop that could only
                    // ever see unset (always-matching) fields. This flag gates state-class field
                    // declaration and dispatch-body shape; ClosedInstantiationHasParameters above still
                    // gates the (unrelated) zero-vs-real-parameter Configure<T>()/Verify<T>() signature
                    // split, which applies regardless of overload status.
                    ClosedInstantiationHasMatchedParameters = m.IsClosedInstantiationEligible && !m.IsOverloaded && m.Parameters.Count > 0,
                    // A constrained (e.g. `where T : class`) closed-instantiation-eligible member's
                    // return type contains "T?" (a nullable-annotated reference to the method's own
                    // type parameter) - real compile spike (2026-08-23): an EXPLICIT interface
                    // implementation can never restate "where T : class" (CS0460), and without it the
                    // compiler can't tell whether "T?" means a nullable-annotated reference or
                    // System.Nullable<T> (CS9334/CS0453/CS0452 cascade, reproduced with a hand-written
                    // minimal repro before this fix). The only C#-legal way to satisfy the interface's
                    // signature from an explicit implementation is to declare the *unannotated* "T"
                    // instead - nullable annotations don't change the underlying runtime type
                    // (Task<T?> and Task<T> are the same CLR type), so this is purely a compile-time
                    // spelling difference - wrapped in #pragma warning disable/restore CS8616 (the
                    // signature-nullability-mismatch warning) and CS8619 (the same mismatch at each
                    // return statement inside the body, found by a second spike round) so it produces
                    // zero consumer-visible warnings. Deliberately pragma-based, not #nullable
                    // disable/restore - #nullable restore reverts to the *project's* default annotation
                    // context, not back to this file's own leading #nullable enable, which left every
                    // subsequent member in the file oblivious (a real CS8669 regression this task's own
                    // SampleTests build caught). A no-op (same text, no pragma emitted) for every
                    // non-nullable-annotated shape.
                    ClosedInstantiationExplicitImplementationReturnType = closedInstantiationExplicitImplementationReturnType,
                    ClosedInstantiationNeedsNullableSuppression =
                        closedInstantiationExplicitImplementationReturnType != m.ReturnTypeFullyQualifiedName,
                    BucketLocalName = bucketLocalName,
                    BoxedLocalName = boxedLocalName,
                    EntryClassName = m.EntryClassName,
                    EntriesFieldName = m.EntriesFieldName,
                    EntryLocalName = entryLocalName,
                    EntryIndexLocalName = entryIndexLocalName,
                    Parameters = m.Parameters
                        .Select((p, i) => new
                        {
                            p.EscapedName,
                            p.OriginalName,
                            p.FullyQualifiedTypeName,
                            p.RefKindPrefix,
                            p.CallSiteRefKindPrefix,
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
            GeneratorVersion = GeneratorVersion.Current,
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
