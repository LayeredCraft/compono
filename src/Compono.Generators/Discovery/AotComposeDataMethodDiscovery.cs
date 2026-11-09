using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Compono.Generators.Diagnostics;
using Compono.Generators.Models;
using Compono.Generators.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Compono.Generators.Discovery;

/// <summary>
/// AOT-side discovery and validation for the <c>[ComposeData]</c> attribute family
/// (<c>Compono.XunitV3.Aot.ComposeDataAttribute</c> and its two generic siblings) - ADR-0070 /
/// PLAN-0071 phase B4. Discovers the member the attribute names, validates its shape, the profile
/// configuration, typed-row binding, <c>[Shared]</c> markers and skip conditions, and produces an
/// equatable <see cref="AotComposeDataMethodInfo"/> model for phase B5's emitter. B4 emits nothing
/// itself; a model carrying any diagnostic suppresses the phase-B5 execution registration.
/// </summary>
/// <remarks>
/// Member resolution mirrors <c>Compono.XunitV3.ComposeDataAttribute.BuildMemberAccessor</c>'s
/// reflection algorithm. Deliberately matched on each closed attribute type's own fully qualified
/// metadata name - never inheritance - matching the marker-only pattern of the AOT Compose family
/// (ADR-0066/ADR-0067 Amendment 1).
/// </remarks>
internal static class AotComposeDataMethodDiscovery
{
    public const string AttributeMetadataName = "Compono.XunitV3.Aot.ComposeDataAttribute";
    public const string GenericAttributeMetadataName = "Compono.XunitV3.Aot.ComposeDataAttribute`1";
    public const string TwoTypeParameterAttributeMetadataName = "Compono.XunitV3.Aot.ComposeDataAttribute`2";

    private const string SharedAttributeMetadataName = "Compono.XunitV3.Aot.SharedAttribute";

    private const string XunitNamespace = "Xunit";
    private const string TheoryDataName = "TheoryData";
    private const string TheoryDataRowName = "TheoryDataRow";
    private const string ITheoryDataRowMetadataName = "Xunit.ITheoryDataRow";
    private const string ITupleMetadataName = "System.Runtime.CompilerServices.ITuple";
    private const string IEnumerableMetadataName = "System.Collections.IEnumerable";
    private const string IEnumerableGenericMetadataName = "System.Collections.Generic.IEnumerable`1";
    private const string IAsyncEnumerableGenericMetadataName = "System.Collections.Generic.IAsyncEnumerable`1";
    private const string TaskMetadataName = "System.Threading.Tasks.Task";
    private const string TaskGenericMetadataName = "System.Threading.Tasks.Task`1";
    private const string ValueTaskMetadataName = "System.Threading.Tasks.ValueTask";
    private const string ValueTaskGenericMetadataName = "System.Threading.Tasks.ValueTask`1";

    public static AotComposeDataMethodInfo? TransformMethod(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not IMethodSymbol method)
            return null;

        var declaringType = method.ContainingType;
        var fullyQualifiedTestClassName = declaringType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var methodDisplayName = $"{declaringType.ToDisplayString()}.{method.Name}";
        var location = LocationInfo.From(method);
        var compilation = context.SemanticModel.Compilation;

        var attributeData = context.Attributes[0];
        var attributeClass = attributeData.AttributeClass!;
        var arity = attributeClass.Arity;
        var profileType = arity >= 1 ? attributeClass.TypeArguments[0] : null;
        var configType = arity == 2 ? attributeClass.TypeArguments[1] : null;
        var profileFqn = profileType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var configFqn = configType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var parameters = method.Parameters
            .Select(static p => new AotComposeDataParameterInfo(
                p.Name,
                p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                p.Ordinal,
                IsNullable(p.Type),
                IsSharedMarker(p)))
            .ToEquatableArray();

        var namedArguments = attributeData.NamedArguments;
        var metadata = BuildAttributeMetadata(namedArguments);

        var memberName = attributeData.ConstructorArguments.Length == 1 && attributeData.ConstructorArguments[0].Value is string name
            ? name
            : null;
        var memberTypeArgument = GetNamedArgument(namedArguments, "MemberType") is { Kind: TypedConstantKind.Type, Value: ITypeSymbol memberType }
            ? memberType
            : null;
        var memberTypeDisplay = memberTypeArgument?.ToDisplayString() ?? declaringType.ToDisplayString();
        var lookupType = (ITypeSymbol?)memberTypeArgument ?? declaringType;
        var lookupFqn = lookupType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var memberDisplayName = $"{memberTypeDisplay}.{memberName}";

        var testClassIndex = AotComposeDataTestClassIndex.Render(declaringType);
        var isGenericTestClass = IsGenericTestClass(declaringType);
        var isTestClassAccessible = compilation.IsSymbolAccessibleWithin(declaringType, compilation.Assembly);
        var lookupIsTestClass = memberTypeArgument is null;
        var asyncRowElementIsValueType = false;

        AotComposeDataMethodInfo Fail(
            DiagnosticInfo diagnostic,
            MemberResolutionKind? memberKind = null,
            string? memberContaining = null,
            AotComposeDataReturnShape? returnShape = null,
            bool rowsAreTyped = false,
            EquatableArray<string> rowElementTypeNames = default,
            AotComposeDataSkipCondition? skipUnless = null,
            AotComposeDataSkipCondition? skipWhen = null)
            => new(
                arity,
                fullyQualifiedTestClassName,
                method.Name,
                lookupFqn,
                profileFqn,
                configFqn,
                memberName,
                memberKind.HasValue ? ToMemberKind(memberKind.Value) : null,
                memberContaining,
                returnShape,
                rowsAreTyped,
                rowElementTypeNames,
                parameters,
                metadata,
                skipUnless,
                skipWhen,
                testClassIndex,
                isGenericTestClass,
                isTestClassAccessible,
                lookupIsTestClass,
                asyncRowElementIsValueType,
                new[] { diagnostic }.ToEquatableArray());

        AotComposeDataMethodInfo Unsupported(string reason)
            => Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataUnsupportedMethodSignature, location, methodDisplayName, reason));

        // Step 1: CMP0045 - more than one Compose-family attribute on the method.
        if (CountAllComposeFamilyAttributes(method) > 1)
        {
            return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataMultipleAttributes, location, methodDisplayName));
        }

        // Step 2: CMP0040 - signature, then duplicate [Shared].
        if (method.IsGenericMethod)
            return Unsupported("generic test methods are not supported");

        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None)
                return Unsupported($"parameter '{parameter.Name}' is ref/out/in, which is not supported");
            if (parameter.IsParams)
                return Unsupported($"parameter '{parameter.Name}' is a params parameter, which is not supported");
            if (!ComposedTypeAnalyzer.IsRowInvokerShapeEligible(parameter.Type, compilation))
                return Unsupported($"parameter '{parameter.Name}' has a type that cannot be composed (an open generic parameter, ref struct, pointer, function pointer, or unsupported array shape)");
        }

        if (FindDuplicateSharedReason(method, methodDisplayName, compilation) is { } duplicateSharedReason)
            return Unsupported(duplicateSharedReason);

        // Step 3: profile.
        if (profileType is not null)
        {
            if (arity == 1)
            {
                if (!compilation.IsSymbolAccessibleWithin(profileType, compilation.Assembly))
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataInaccessibleProfileSymbol, location, profileType.ToDisplayString(), methodDisplayName, "the TProfile type argument"));

                if (profileType is INamedTypeSymbol { InstanceConstructors: var profileConstructors }
                    && profileConstructors.FirstOrDefault(static c => c.Parameters.Length == 0) is { } parameterless
                    && HasProhibitedAotAttribute(parameterless))
                {
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataProfileConstructorRequiresAotUnsafeFeature, location, profileType.ToDisplayString(), methodDisplayName));
                }
            }
            else if (configType is not null)
            {
                if (!compilation.IsSymbolAccessibleWithin(configType, compilation.Assembly))
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataInaccessibleProfileSymbol, location, configType.ToDisplayString(), methodDisplayName, "the TConfig type argument"));

                var usableProfileConstructors = FindUsableProfileConstructors(profileType, configType, compilation);
                if (usableProfileConstructors.Length != 1)
                {
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataInvalidProfileConstructorShape, location, profileType.ToDisplayString(), configType.ToDisplayString(), methodDisplayName, usableProfileConstructors.Length));
                }

                if (!compilation.IsSymbolAccessibleWithin(profileType, compilation.Assembly))
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataInaccessibleProfileSymbol, location, profileType.ToDisplayString(), methodDisplayName, "the TProfile type argument"));

                var selectedProfileConstructor = usableProfileConstructors[0];
                if (!ConstructorSatisfiesRequiredMembers(profileType, selectedProfileConstructor))
                    return Fail(RequiredMembersDiagnostic(profileType, selectedProfileConstructor, methodDisplayName, location));
                if (ProhibitedCallSiteAttribute(selectedProfileConstructor) is { } prohibited)
                    return Fail(ProhibitedConstructorDiagnostic(profileType, methodDisplayName, location, prohibited));
                if (profileType is INamedTypeSymbol profileNamed
                    && FindHigherPriorityAccessibleSibling(profileNamed, selectedProfileConstructor, compilation) is { } superseding)
                {
                    return Fail(SupersededByPriorityDiagnostic(profileType, superseding, methodDisplayName, location));
                }
            }
        }

        // Step 4: CMP0054 - open-generic MemberType.
        if (memberTypeArgument is INamedTypeSymbol { IsUnboundGenericType: true }
            || (memberTypeArgument is INamedTypeSymbol namedMemberType && namedMemberType.TypeArguments.Any(static t => t is ITypeParameterSymbol)))
        {
            return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataOpenGenericMemberType, location, memberTypeDisplay, methodDisplayName));
        }

        // Step 5: member resolution (CMP0051 / CMP0052).
        if (string.IsNullOrEmpty(memberName))
            return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataMemberNotFound, location, memberDisplayName, methodDisplayName));

        var resolution = ResolveMember(lookupType, memberName!, compilation);
        switch (resolution.Kind)
        {
            case MemberResolutionKind.NotFound:
                return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataMemberNotFound, location, memberDisplayName, methodDisplayName));
            case MemberResolutionKind.Ambiguous:
                return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataMemberAmbiguous, location, memberDisplayName, methodDisplayName));
            case MemberResolutionKind.GenericMethod:
                return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataMemberIsGenericMethod, location, memberDisplayName, methodDisplayName));
            case MemberResolutionKind.RequiresArguments:
                return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataMemberRequiresArguments, location, memberDisplayName, methodDisplayName));
            case MemberResolutionKind.NotPublicStatic:
                return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataMemberNotPublicStatic, location, memberDisplayName, methodDisplayName));
        }

        var selectedMember = resolution.Member!;
        var memberContainingType = selectedMember.ContainingType;
        var memberContainingFqn = memberContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Step 6: CMP0044 - MemberType itself and the selected member's holder type.
        if (memberTypeArgument is not null && !compilation.IsSymbolAccessibleWithin(memberTypeArgument, compilation.Assembly))
            return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataInaccessibleProfileSymbol, location, memberTypeArgument.ToDisplayString(), methodDisplayName, "the MemberType type argument"), resolution.Kind, memberContainingFqn);
        if (!compilation.IsSymbolAccessibleWithin(memberContainingType, compilation.Assembly))
            return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataInaccessibleProfileSymbol, location, memberContainingType.ToDisplayString(), methodDisplayName, "the ComposeData member holder type"), resolution.Kind, memberContainingFqn);

        // Step 6a: CMP0054 - the selected member is declared on an open generic type the generated
        // registration cannot name (ADR-0070 Amendment 4).
        if (IsOpenGenericType(memberContainingType))
            return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataOpenGenericMemberHolder, location, memberDisplayName, methodDisplayName, memberContainingType.ToDisplayString()), resolution.Kind, memberContainingFqn);

        // Step 6b: CMP0052 - a static abstract/static virtual interface member cannot be called from
        // generated code (ADR-0070 Amendment 4).
        if (IsStaticInterfaceMember(selectedMember))
            return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataStaticInterfaceMember, location, memberDisplayName, methodDisplayName), resolution.Kind, memberContainingFqn);

        // Step 6c: CMP0047 - the selected member (or, for a property, its getter) is marked with an
        // attribute that makes any use of it a compiler error (ADR-0070 Amendment 4).
        if (ProhibitedCallSiteAttribute(selectedMember) is { } prohibitedMember)
            return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataProhibitedMember, location, memberDisplayName, methodDisplayName, prohibitedMember), resolution.Kind, memberContainingFqn);
        if (selectedMember is IPropertySymbol { GetMethod: { } memberGetter }
            && ProhibitedCallSiteAttribute(memberGetter) is { } prohibitedMemberGetter)
            return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataProhibitedMemberGetter, location, memberDisplayName, methodDisplayName, prohibitedMemberGetter), resolution.Kind, memberContainingFqn);

        // Step 7: CMP0053 - return shape, then row element shape.
        var declaredMemberType = GetMemberType(selectedMember);
        asyncRowElementIsValueType = AnalyzeAsyncRowElementIsValueType(declaredMemberType);
        var returnAnalysis = AnalyzeReturnShape(declaredMemberType);
        if (returnAnalysis.ErrorType is not null)
        {
            return Fail(
                UnsupportedType(memberDisplayName, methodDisplayName, location, returnAnalysis.ErrorType, "return"),
                resolution.Kind, memberContainingFqn);
        }

        var rowAnalysis = AnalyzeRows(returnAnalysis.CollectionType!, returnAnalysis.Shape!.Value);
        if (rowAnalysis.ErrorType is not null)
        {
            return Fail(
                UnsupportedType(memberDisplayName, methodDisplayName, location, rowAnalysis.ErrorType, "row element"),
                resolution.Kind, memberContainingFqn, returnAnalysis.Shape, rowAnalysis.RowsAreTyped, rowAnalysis.RowElementTypeNames);
        }

        // Step 8: CMP0055 then CMP0056 (typed rows only, statically provable mismatches).
        if (rowAnalysis.RowsAreTyped)
        {
            var rowElements = rowAnalysis.RowElementTypes;

            if (arity == 2 && configType is not null && rowElements.Length > 0
                && !IsStaticallyAssignable(rowElements[0], configType, compilation))
            {
                return Fail(
                    new DiagnosticInfo(DiagnosticDescriptors.ComposeDataConfigValueTypeMismatch, location, memberDisplayName, methodDisplayName, rowElements[0].ToDisplayString(), configType.ToDisplayString()),
                    resolution.Kind, memberContainingFqn, returnAnalysis.Shape, true, rowAnalysis.RowElementTypeNames);
            }

            var valueElements = arity == 2 ? rowElements.Skip(1).ToArray() : rowElements;
            if (valueElements.Length > method.Parameters.Length)
            {
                return Fail(
                    new DiagnosticInfo(DiagnosticDescriptors.ComposeDataTooManyRowValues, location, memberDisplayName, methodDisplayName, valueElements.Length, method.Parameters.Length),
                    resolution.Kind, memberContainingFqn, returnAnalysis.Shape, true, rowAnalysis.RowElementTypeNames);
            }

            for (var i = 0; i < valueElements.Length; i++)
            {
                if (!IsStaticallyAssignable(valueElements[i], method.Parameters[i].Type, compilation))
                {
                    return Fail(
                        new DiagnosticInfo(DiagnosticDescriptors.ComposeDataRowValueTypeMismatch, location, memberDisplayName, methodDisplayName, i, valueElements[i].ToDisplayString(), method.Parameters[i].Name, method.Parameters[i].Type.ToDisplayString()),
                        resolution.Kind, memberContainingFqn, returnAnalysis.Shape, true, rowAnalysis.RowElementTypeNames);
                }
            }
        }

        // Step 9: CMP0057 then CMP0044 (skip conditions, active only when Skip is set).
        AotComposeDataSkipCondition? skipUnless = null;
        AotComposeDataSkipCondition? skipWhen = null;

        if (GetNamedArgument(namedArguments, "Skip") is { IsNull: false })
        {
            var skipUnlessProperty = GetNamedArgument(namedArguments, "SkipUnless") is { Value: string skipUnlessName } ? skipUnlessName : null;
            var skipWhenProperty = GetNamedArgument(namedArguments, "SkipWhen") is { Value: string skipWhenName } ? skipWhenName : null;
            var skipTypeSymbol = GetNamedArgument(namedArguments, "SkipType") is { Kind: TypedConstantKind.Type, Value: ITypeSymbol skipType } ? skipType : null;
            var skipHolder = (ITypeSymbol?)skipTypeSymbol ?? declaringType;

            // An explicitly supplied unbound generic SkipType (e.g. typeof(Holder<>)) exposes no members
            // to Roslyn's GetMembers, so resolve the skip property through its OriginalDefinition - the
            // property still exists, and when it lives on the open generic the CMP0054 open-generic
            // skip-holder rejection reports exactly that. Scoped to SkipType only: no broader fallback.
            var skipLookupHolder = skipTypeSymbol is INamedTypeSymbol { IsUnboundGenericType: true } unboundSkipType
                ? unboundSkipType.OriginalDefinition
                : skipHolder;

            if (skipUnlessProperty is not null && skipWhenProperty is not null)
            {
                return Fail(
                    new DiagnosticInfo(DiagnosticDescriptors.ComposeDataConflictingSkipConditions, location, methodDisplayName),
                    resolution.Kind, memberContainingFqn, returnAnalysis.Shape, rowAnalysis.RowsAreTyped, rowAnalysis.RowElementTypeNames);
            }

            if (skipUnlessProperty is not null)
            {
                var resolvedSkipUnless = ResolveSkipProperty(skipLookupHolder, skipUnlessProperty);
                if (resolvedSkipUnless is null || !IsUsableSkipProperty(resolvedSkipUnless))
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataInvalidSkipConditionProperty, location, methodDisplayName, "SkipUnless", skipUnlessProperty, skipHolder.ToDisplayString()), resolution.Kind, memberContainingFqn, returnAnalysis.Shape, rowAnalysis.RowsAreTyped, rowAnalysis.RowElementTypeNames);
                if (!compilation.IsSymbolAccessibleWithin(skipHolder, compilation.Assembly))
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataInaccessibleProfileSymbol, location, skipHolder.ToDisplayString(), methodDisplayName, "the SkipType type argument"), resolution.Kind, memberContainingFqn, returnAnalysis.Shape, rowAnalysis.RowsAreTyped, rowAnalysis.RowElementTypeNames);
                if (IsOpenGenericType(resolvedSkipUnless.ContainingType))
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataOpenGenericSkipHolder, location, methodDisplayName, "SkipUnless", skipUnlessProperty, resolvedSkipUnless.ContainingType.ToDisplayString()), resolution.Kind, memberContainingFqn, returnAnalysis.Shape, rowAnalysis.RowsAreTyped, rowAnalysis.RowElementTypeNames);
                if (IsStaticInterfaceMember(resolvedSkipUnless))
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataStaticInterfaceSkipProperty, location, methodDisplayName, "SkipUnless", skipUnlessProperty, resolvedSkipUnless.ContainingType.ToDisplayString()), resolution.Kind, memberContainingFqn, returnAnalysis.Shape, rowAnalysis.RowsAreTyped, rowAnalysis.RowElementTypeNames);
                if (ProhibitedCallSiteAttribute(resolvedSkipUnless) is { } prohibitedSkipUnless)
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataProhibitedSkipProperty, location, methodDisplayName, "SkipUnless", skipUnlessProperty, resolvedSkipUnless.ContainingType.ToDisplayString(), prohibitedSkipUnless), resolution.Kind, memberContainingFqn, returnAnalysis.Shape, rowAnalysis.RowsAreTyped, rowAnalysis.RowElementTypeNames);
                if (resolvedSkipUnless.GetMethod is { } skipUnlessGetter && ProhibitedCallSiteAttribute(skipUnlessGetter) is { } prohibitedSkipUnlessGetter)
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataProhibitedSkipPropertyGetter, location, methodDisplayName, "SkipUnless", skipUnlessProperty, resolvedSkipUnless.ContainingType.ToDisplayString(), prohibitedSkipUnlessGetter), resolution.Kind, memberContainingFqn, returnAnalysis.Shape, rowAnalysis.RowsAreTyped, rowAnalysis.RowElementTypeNames);
                skipUnless = new AotComposeDataSkipCondition(resolvedSkipUnless.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), skipUnlessProperty);
            }

            if (skipWhenProperty is not null)
            {
                var resolvedSkipWhen = ResolveSkipProperty(skipLookupHolder, skipWhenProperty);
                if (resolvedSkipWhen is null || !IsUsableSkipProperty(resolvedSkipWhen))
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataInvalidSkipConditionProperty, location, methodDisplayName, "SkipWhen", skipWhenProperty, skipHolder.ToDisplayString()), resolution.Kind, memberContainingFqn, returnAnalysis.Shape, rowAnalysis.RowsAreTyped, rowAnalysis.RowElementTypeNames);
                if (!compilation.IsSymbolAccessibleWithin(skipHolder, compilation.Assembly))
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataInaccessibleProfileSymbol, location, skipHolder.ToDisplayString(), methodDisplayName, "the SkipType type argument"), resolution.Kind, memberContainingFqn, returnAnalysis.Shape, rowAnalysis.RowsAreTyped, rowAnalysis.RowElementTypeNames);
                if (IsOpenGenericType(resolvedSkipWhen.ContainingType))
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataOpenGenericSkipHolder, location, methodDisplayName, "SkipWhen", skipWhenProperty, resolvedSkipWhen.ContainingType.ToDisplayString()), resolution.Kind, memberContainingFqn, returnAnalysis.Shape, rowAnalysis.RowsAreTyped, rowAnalysis.RowElementTypeNames);
                if (IsStaticInterfaceMember(resolvedSkipWhen))
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataStaticInterfaceSkipProperty, location, methodDisplayName, "SkipWhen", skipWhenProperty, resolvedSkipWhen.ContainingType.ToDisplayString()), resolution.Kind, memberContainingFqn, returnAnalysis.Shape, rowAnalysis.RowsAreTyped, rowAnalysis.RowElementTypeNames);
                if (ProhibitedCallSiteAttribute(resolvedSkipWhen) is { } prohibitedSkipWhen)
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataProhibitedSkipProperty, location, methodDisplayName, "SkipWhen", skipWhenProperty, resolvedSkipWhen.ContainingType.ToDisplayString(), prohibitedSkipWhen), resolution.Kind, memberContainingFqn, returnAnalysis.Shape, rowAnalysis.RowsAreTyped, rowAnalysis.RowElementTypeNames);
                if (resolvedSkipWhen.GetMethod is { } skipWhenGetter && ProhibitedCallSiteAttribute(skipWhenGetter) is { } prohibitedSkipWhenGetter)
                    return Fail(new DiagnosticInfo(DiagnosticDescriptors.ComposeDataProhibitedSkipPropertyGetter, location, methodDisplayName, "SkipWhen", skipWhenProperty, resolvedSkipWhen.ContainingType.ToDisplayString(), prohibitedSkipWhenGetter), resolution.Kind, memberContainingFqn, returnAnalysis.Shape, rowAnalysis.RowsAreTyped, rowAnalysis.RowElementTypeNames);
                skipWhen = new AotComposeDataSkipCondition(resolvedSkipWhen.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), skipWhenProperty);
            }
        }

        return new AotComposeDataMethodInfo(
            arity,
            fullyQualifiedTestClassName,
            method.Name,
            lookupFqn,
            profileFqn,
            configFqn,
            memberName,
            ToMemberKind(resolution.Kind),
            memberContainingFqn,
            returnAnalysis.Shape,
            rowAnalysis.RowsAreTyped,
            rowAnalysis.RowElementTypeNames,
            parameters,
            metadata,
            skipUnless,
            skipWhen,
            testClassIndex,
            isGenericTestClass,
            isTestClassAccessible,
            lookupIsTestClass,
            asyncRowElementIsValueType,
            EquatableArray<DiagnosticInfo>.Empty);
    }

    private static int CountAllComposeFamilyAttributes(IMethodSymbol method)
        => method.GetAttributes().Count(static a => a.AttributeClass is { } attributeClass && IsComposeFamilyAttribute(attributeClass));

    private static bool IsComposeFamilyAttribute(INamedTypeSymbol attributeClass)
    {
        var metadataName = MetadataName(attributeClass);
        return metadataName is AttributeMetadataName
            or GenericAttributeMetadataName
            or TwoTypeParameterAttributeMetadataName
            or AotComposeMethodDiscovery.AttributeMetadataName
            or AotComposeMethodDiscovery.GenericAttributeMetadataName
            or AotComposeMethodDiscovery.TwoTypeParameterAttributeMetadataName;
    }

    private static string? FindDuplicateSharedReason(IMethodSymbol method, string methodDisplayName, Compilation compilation)
    {
        // Compare types pairwise with the compiler's own classification, which matches the CLR type
        // identity the JIT reflection path sees (System.Type): `dynamic` is `object`, tuple element
        // names are erased, and nullability annotations on reference types do not change identity -
        // while a nullable value type (`int?`) stays distinct from its underlying type (`int`).
        var sharedParameters = method.Parameters.Where(static p => IsSharedMarker(p)).ToArray();
        for (var i = 0; i < sharedParameters.Length; i++)
        {
            for (var j = i + 1; j < sharedParameters.Length; j++)
            {
                if (!compilation.ClassifyConversion(sharedParameters[i].Type, sharedParameters[j].Type).IsIdentity)
                    continue;

                var parameterNames = string.Join("', '", new[] { sharedParameters[i], sharedParameters[j] }.Select(static p => p.Name));
                return $"More than one [Shared] parameter of type '{sharedParameters[i].Type.ToDisplayString()}' ('{parameterNames}') on '{methodDisplayName}' - only one [Shared] parameter per type is allowed";
            }
        }

        return null;
    }

    private static bool IsSharedMarker(IParameterSymbol parameter)
        => parameter.GetAttributes().Any(static a => a.AttributeClass is { } attributeClass && MetadataName(attributeClass) == SharedAttributeMetadataName);

    private static bool IsGenericTestClass(INamedTypeSymbol type)
    {
        for (var current = (INamedTypeSymbol?)type; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0)
                return true;
        }

        return false;
    }

    private static bool IsOpenGenericType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
            return false;

        if (named.IsUnboundGenericType)
            return true;

        if (named.IsGenericType && named.TypeArguments.Any(ComposedTypeAnalyzer.ContainsTypeParameter))
            return true;

        return named.ContainingType is not null && IsOpenGenericType(named.ContainingType);
    }

    private static bool IsStaticInterfaceMember(ISymbol member)
    {
        if (member.ContainingType.TypeKind != TypeKind.Interface)
            return false;

        return member.IsStatic && (member.IsAbstract || member.IsVirtual);
    }

    private static MemberResolution ResolveMember(ITypeSymbol lookupType, string memberName, Compilation compilation)
    {
        // Fields (most-derived public static wins; fields always hide by name).
        for (var type = lookupType; type is not null; type = type.BaseType)
        {
            foreach (var member in type.GetMembers(memberName))
            {
                if (member is IFieldSymbol { IsStatic: true, DeclaredAccessibility: Accessibility.Public })
                    return new MemberResolution(MemberResolutionKind.Field, member);
            }
        }

        // Properties. A more-derived static property of identical CLR type hides a base one; only public
        // properties are selectable. Mirror reflection's visibility: a private property participates in
        // hiding only when it is declared on the lookup type itself - an inherited private property is
        // not exposed by FlattenHierarchy and does not hide a base property. Type identity uses the
        // compilation's conversion rules (matching reflection), so dynamic/object and tuple element names
        // that erase to the same CLR type are treated as identical.
        var seenPropertyTypes = new List<ITypeSymbol>();
        var propertyCandidates = new List<IPropertySymbol>();
        for (var type = lookupType; type is not null; type = type.BaseType)
        {
            foreach (var member in type.GetMembers(memberName))
            {
                if (member is not IPropertySymbol { IsStatic: true, IsIndexer: false } property)
                    continue;

                if (property.DeclaredAccessibility == Accessibility.Private
                    && !SymbolEqualityComparer.Default.Equals(type, lookupType))
                    continue;

                if (seenPropertyTypes.Any(t => compilation.ClassifyConversion(t, property.Type).IsIdentity))
                    continue;

                seenPropertyTypes.Add(property.Type);

                if (property.DeclaredAccessibility == Accessibility.Public)
                    propertyCandidates.Add(property);
            }
        }

        if (propertyCandidates.Count > 1)
            return new MemberResolution(MemberResolutionKind.Ambiguous, null);

        if (propertyCandidates.Count == 1 && propertyCandidates[0].GetMethod is { IsStatic: true, DeclaredAccessibility: Accessibility.Public })
            return new MemberResolution(MemberResolutionKind.Property, propertyCandidates[0]);

        // Parameterless methods (most-derived type declaring any public static zero-parameter method).
        for (var type = lookupType; type is not null; type = type.BaseType)
        {
            var candidates = type.GetMembers(memberName)
                .OfType<IMethodSymbol>()
                .Where(static m => m.MethodKind == MethodKind.Ordinary && m.IsStatic && m.DeclaredAccessibility == Accessibility.Public && m.Parameters.Length == 0)
                .ToArray();

            if (candidates.Length == 0)
                continue;

            if (candidates.Length > 1)
                return new MemberResolution(MemberResolutionKind.Ambiguous, null);

            if (candidates[0].IsGenericMethod)
                return new MemberResolution(MemberResolutionKind.GenericMethod, null);

            return new MemberResolution(MemberResolutionKind.Method, candidates[0]);
        }

        // Failure classification (mirrors reflection's public+nonpublic+static+instance+FlattenHierarchy
        // fallback: base-type private members are invisible, every other accessibility is a candidate).
        var anyVisible = false;
        var anyMethodWithParameters = false;
        for (var type = lookupType; type is not null; type = type.BaseType)
        {
            foreach (var member in type.GetMembers(memberName))
            {
                if (!SymbolEqualityComparer.Default.Equals(type, lookupType) && member.DeclaredAccessibility == Accessibility.Private)
                    continue;

                anyVisible = true;
                if (member is IMethodSymbol { Parameters.Length: > 0 })
                    anyMethodWithParameters = true;
            }
        }

        if (!anyVisible)
            return new MemberResolution(MemberResolutionKind.NotFound, null);
        if (anyMethodWithParameters)
            return new MemberResolution(MemberResolutionKind.RequiresArguments, null);
        return new MemberResolution(MemberResolutionKind.NotPublicStatic, null);
    }

    private static ReturnAnalysis AnalyzeReturnShape(ITypeSymbol declaredType)
    {
        var asyncInterfaces = GetAsyncEnumerableInterfaces(declaredType);
        if (asyncInterfaces.Count > 0)
        {
            // The JIT path reads through `source is IAsyncEnumerable<object?>` when that variance cast
            // succeeds, else the first matching interface from Type.GetInterfaces(). Exactly one
            // reference-type element is therefore deterministic (any number of value-type elements is
            // shadowed by it); two-or-more reference-type elements, or two-or-more value-type elements
            // with no reference-type element, is not (ADR-0070 Amendment 4).
            var referenceCount = 0;
            var valueCount = 0;
            foreach (var asyncInterface in asyncInterfaces)
            {
                if (asyncInterface.TypeArguments[0].IsValueType)
                    valueCount++;
                else
                    referenceCount++;
            }

            if (referenceCount >= 2 || (referenceCount == 0 && valueCount >= 2))
                return new ReturnAnalysis(null, null, declaredType);

            return new ReturnAnalysis(AotComposeDataReturnShape.AsyncEnumerable, declaredType, null);
        }

        if (declaredType is INamedTypeSymbol named)
        {
            var metadataName = MetadataName(named);
            if (metadataName == TaskMetadataName || metadataName == ValueTaskMetadataName)
                return new ReturnAnalysis(null, null, declaredType);

            if (metadataName == TaskGenericMetadataName)
            {
                var result = named.TypeArguments[0];
                return ProvablyNeverEnumerable(result)
                    ? new ReturnAnalysis(null, null, declaredType)
                    : new ReturnAnalysis(AotComposeDataReturnShape.TaskOf, result, null);
            }

            if (metadataName == ValueTaskGenericMetadataName)
            {
                var result = named.TypeArguments[0];
                return ProvablyNeverEnumerable(result)
                    ? new ReturnAnalysis(null, null, declaredType)
                    : new ReturnAnalysis(AotComposeDataReturnShape.ValueTaskOf, result, null);
            }
        }

        if (declaredType.SpecialType != SpecialType.System_String && ImplementsIEnumerable(declaredType))
            return new ReturnAnalysis(AotComposeDataReturnShape.Sync, declaredType, null);

        return new ReturnAnalysis(null, null, declaredType);
    }

    private static RowAnalysis AnalyzeRows(ITypeSymbol collectionType, AotComposeDataReturnShape shape)
    {
        var row = shape == AotComposeDataReturnShape.AsyncEnumerable
            ? GetAsyncEnumerableElement(collectionType)
            : GetEnumerableElement(collectionType);

        if (TryGetTheoryDataArguments(collectionType, out var theoryDataArguments))
            return RowAnalysis.Typed(theoryDataArguments);

        if (row is not null && TryGetTheoryDataRowArguments(row, out var theoryDataRowArguments))
            return RowAnalysis.Typed(theoryDataRowArguments);

        if (row is not null && ProvablyNeverRowShape(row))
            return RowAnalysis.Error(row);

        return RowAnalysis.Untyped();
    }

    private static bool TryGetTheoryDataArguments(ITypeSymbol collectionType, out ITypeSymbol[] arguments)
    {
        for (var type = collectionType as INamedTypeSymbol; type is not null; type = type.BaseType)
        {
            if (type.Arity > 0 && type.Name == TheoryDataName && type.ContainingNamespace.ToDisplayString() == XunitNamespace)
            {
                arguments = type.TypeArguments.ToArray();
                return true;
            }
        }

        arguments = [];
        return false;
    }

    private static bool TryGetTheoryDataRowArguments(ITypeSymbol rowType, out ITypeSymbol[] arguments)
    {
        for (var type = rowType as INamedTypeSymbol; type is not null; type = type.BaseType)
        {
            if (type.Arity > 0 && type.Name == TheoryDataRowName && type.ContainingNamespace.ToDisplayString() == XunitNamespace)
            {
                arguments = type.TypeArguments.ToArray();
                return true;
            }
        }

        arguments = [];
        return false;
    }

    private static ITypeSymbol? GetAsyncEnumerableElement(ITypeSymbol type)
    {
        ITypeSymbol? referenceElement = null;
        var referenceCount = 0;
        ITypeSymbol? valueElement = null;
        var valueCount = 0;

        foreach (var asyncInterface in GetAsyncEnumerableInterfaces(type))
        {
            var element = asyncInterface.TypeArguments[0];
            if (element.IsValueType)
            {
                valueCount++;
                valueElement = element;
            }
            else
            {
                referenceCount++;
                referenceElement = element;
            }
        }

        if (referenceCount == 1)
            return referenceElement;
        if (referenceCount == 0 && valueCount == 1)
            return valueElement;
        return null;
    }

    private static List<INamedTypeSymbol> GetAsyncEnumerableInterfaces(ITypeSymbol type)
    {
        var interfaces = new List<INamedTypeSymbol>();
        if (type is INamedTypeSymbol named && MetadataName(named) == IAsyncEnumerableGenericMetadataName)
            interfaces.Add(named);

        foreach (var @interface in type.AllInterfaces)
        {
            if (MetadataName(@interface) == IAsyncEnumerableGenericMetadataName)
                interfaces.Add(@interface);
        }

        return interfaces;
    }

    private static bool AnalyzeAsyncRowElementIsValueType(ITypeSymbol type)
    {
        var asyncInterfaces = GetAsyncEnumerableInterfaces(type);
        return asyncInterfaces.Count == 1 && asyncInterfaces[0].TypeArguments[0].IsValueType;
    }

    private static ITypeSymbol? GetEnumerableElement(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && MetadataName(named) == IEnumerableGenericMetadataName)
            return named.TypeArguments[0];

        ITypeSymbol? element = null;
        foreach (var @interface in type.AllInterfaces)
        {
            if (MetadataName(@interface) != IEnumerableGenericMetadataName)
                continue;
            if (element is not null)
                return null;
            element = @interface.TypeArguments[0];
        }

        return element;
    }

    private static bool ImplementsIEnumerable(ITypeSymbol type)
        => ImplementsInterface(type, IEnumerableMetadataName) || ImplementsInterface(type, IEnumerableGenericMetadataName);

    private static bool ImplementsInterface(ITypeSymbol type, string metadataName)
    {
        if (type is INamedTypeSymbol named && MetadataName(named) == metadataName)
            return true;

        foreach (var @interface in type.AllInterfaces)
        {
            if (MetadataName(@interface) == metadataName)
                return true;
        }

        return false;
    }

    private static bool ProvablyNeverEnumerable(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
            return true;
        if (ImplementsIEnumerable(type))
            return false;
        return type.IsValueType || type.IsSealed;
    }

    private static bool ProvablyNeverRowShape(ITypeSymbol row)
    {
        if (row is IArrayTypeSymbol array)
            return array.ElementType.IsValueType;
        if (row.SpecialType == SpecialType.System_Object)
            return false;
        if (ImplementsInterface(row, ITheoryDataRowMetadataName) || ImplementsInterface(row, ITupleMetadataName))
            return false;
        return row.IsValueType || row.IsSealed;
    }

    private static ITypeSymbol GetMemberType(ISymbol member) => member switch
    {
        IFieldSymbol field => field.Type,
        IPropertySymbol property => property.Type,
        IMethodSymbol method => method.ReturnType,
        _ => throw new System.InvalidOperationException($"Unexpected ComposeData member kind '{member.Kind}'."),
    };

    private static bool IsStaticallyAssignable(ITypeSymbol from, ITypeSymbol to, Compilation compilation)
    {
        var unwrappedFrom = UnwrapNullable(from);
        var unwrappedTo = UnwrapNullable(to);
        if (SymbolEqualityComparer.Default.Equals(unwrappedFrom, unwrappedTo))
            return true;

        var conversion = compilation.ClassifyConversion(unwrappedFrom, unwrappedTo);
        return conversion.IsIdentity || conversion.IsReference || conversion.IsBoxing || conversion.IsUnboxing;
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
        => type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;

    private static IPropertySymbol? ResolveSkipProperty(ITypeSymbol holder, string propertyName)
    {
        // Hide-by-name: the most-derived declaration named propertyName wins, matching how a generated
        // `() => global::Holder.Property` reference would bind. Only a public static bool property with a
        // public static getter is usable from the generated static context.
        for (var type = holder; type is not null; type = type.BaseType)
        {
            foreach (var member in type.GetMembers(propertyName))
            {
                if (member is IPropertySymbol { IsIndexer: false } property)
                    return property;
            }
        }

        return null;
    }

    private static bool IsUsableSkipProperty(IPropertySymbol property)
        => property.IsStatic
            && property.DeclaredAccessibility == Accessibility.Public
            && property.Type.SpecialType == SpecialType.System_Boolean
            && property.GetMethod is { IsStatic: true, DeclaredAccessibility: Accessibility.Public };

    private static IMethodSymbol[] FindUsableProfileConstructors(ITypeSymbol profileType, ITypeSymbol configType, Compilation compilation)
    {
        if (profileType is not INamedTypeSymbol { IsAbstract: false } namedProfile)
            return [];

        // Match the configuration parameter against TConfig using the compiler's own CLR-identity
        // classification (System.Type semantics on the JIT path): `dynamic` is `object`, tuple element
        // names are erased, and reference-type nullability does not change identity - while a nullable
        // value type (`int?`) stays distinct from its underlying type (`int`).
        return namedProfile.Constructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public
                && c.Parameters.Length == 1
                && c.Parameters[0].RefKind == RefKind.None
                && compilation.ClassifyConversion(c.Parameters[0].Type, configType).IsIdentity
                && !HasProhibitedAotAttribute(c))
            .ToArray();
    }

    private static AotComposeDataAttributeMetadata BuildAttributeMetadata(ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments)
    {
        var seed = GetNamedArgument(namedArguments, "Seed") is { Value: int seedValue } ? seedValue : (int?)null;
        var label = GetNamedArgument(namedArguments, "Label") is { Value: string labelValue } ? labelValue : null;
        var skip = GetNamedArgument(namedArguments, "Skip") is { Value: string skipValue } ? skipValue : null;
        var testDisplayName = GetNamedArgument(namedArguments, "TestDisplayName") is { Value: string testDisplayNameValue } ? testDisplayNameValue : null;
        var explicitValue = GetNamedArgument(namedArguments, "Explicit") is { Value: bool explicitFlag } ? explicitFlag : (bool?)null;
        var timeout = GetNamedArgument(namedArguments, "Timeout") is { Value: int timeoutValue } ? timeoutValue : (int?)null;
        var disableParallelization = GetNamedArgument(namedArguments, "DisableParallelization") is { Value: bool disableParallelizationFlag } ? disableParallelizationFlag : (bool?)null;

        var traitsConstant = GetNamedArgument(namedArguments, "Traits");
        var traits = traitsConstant is { Kind: TypedConstantKind.Array } traitsArray
            ? traitsArray.Values.Select(static v => (string)v.Value!).ToEquatableArray()
            : EquatableArray<string>.Empty;

        return new AotComposeDataAttributeMetadata(seed, label, skip, testDisplayName, explicitValue, timeout, traits, disableParallelization);
    }

    private static TypedConstant? GetNamedArgument(ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments, string name)
    {
        foreach (var namedArgument in namedArguments)
        {
            if (namedArgument.Key == name)
                return namedArgument.Value;
        }

        return null;
    }

    private static DiagnosticInfo UnsupportedType(string memberDisplayName, string methodDisplayName, LocationInfo? location, ITypeSymbol type, string kind)
        => new(DiagnosticDescriptors.ComposeDataUnsupportedReturnOrRowElementType, location, memberDisplayName, methodDisplayName, type.ToDisplayString(), kind);

    private static string MetadataName(INamedTypeSymbol type)
        => $"{type.ContainingNamespace.ToDisplayString()}.{type.MetadataName}";

    private static bool IsNullable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
            return true;
        if (type.IsValueType)
            return false;
        return type.NullableAnnotation == NullableAnnotation.Annotated;
    }

    private static bool ConstructorSatisfiesRequiredMembers(ITypeSymbol type, IMethodSymbol constructor)
    {
        if (constructor.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute"))
            return true;

        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true })
                    return false;
            }
        }

        return true;
    }

    private static DiagnosticInfo RequiredMembersDiagnostic(ITypeSymbol type, IMethodSymbol constructor, string methodDisplayName, LocationInfo? location)
    {
        var unsatisfiedMember = FindFirstUnsatisfiedRequiredMember(type);
        return new DiagnosticInfo(DiagnosticDescriptors.ComposeDataProfileConstructorRequiredMembersUnsatisfied, location, type.ToDisplayString(), unsatisfiedMember, methodDisplayName);
    }

    private static string FindFirstUnsatisfiedRequiredMember(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true })
                    return member.Name;
            }
        }

        return "(unknown)";
    }

    private static string? ProhibitedCallSiteAttribute(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            switch (attribute.AttributeClass?.ToDisplayString())
            {
                case "System.ObsoleteAttribute" when attribute.ConstructorArguments is [_, { Value: true }]:
                    return "[Obsolete(error: true)]";
                case "System.Diagnostics.CodeAnalysis.ExperimentalAttribute":
                    var diagnosticId = attribute.ConstructorArguments is [{ Value: string id }] ? id : "...";
                    return $"[Experimental(\"{diagnosticId}\")]";
                case "System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute":
                    var isOptional = attribute.NamedArguments.FirstOrDefault(static na => na.Key == "IsOptional").Value is { Value: true };
                    if (isOptional)
                        continue;
                    var featureName = attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string feature ? feature : "...";
                    return $"[CompilerFeatureRequired(\"{featureName}\")]";
            }
        }

        return null;
    }

    private static bool HasProhibitedAotAttribute(IMethodSymbol constructor)
        => constructor.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() is
            "System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute" or
            "System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute" or
            "System.Diagnostics.CodeAnalysis.RequiresAssemblyFilesAttribute");

    private static DiagnosticInfo ProhibitedConstructorDiagnostic(ITypeSymbol type, string methodDisplayName, LocationInfo? location, string prohibitedAttribute)
        => new(DiagnosticDescriptors.ComposeDataProhibitedProfileConstructor, location, type.ToDisplayString(), methodDisplayName, prohibitedAttribute);

    private static IMethodSymbol? FindHigherPriorityAccessibleSibling(INamedTypeSymbol type, IMethodSymbol selected, Compilation compilation)
    {
        var selectedPriority = OverloadResolutionPriorityOf(selected);
        foreach (var candidate in type.Constructors)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, selected))
                continue;
            if (OverloadResolutionPriorityOf(candidate) <= selectedPriority)
                continue;
            if (compilation.IsSymbolAccessibleWithin(candidate, compilation.Assembly))
                return candidate;
        }

        return null;
    }

    private static int OverloadResolutionPriorityOf(IMethodSymbol constructor)
    {
        foreach (var attribute in constructor.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute"
                && attribute.ConstructorArguments is [{ Value: int priority }])
            {
                return priority;
            }
        }

        return 0;
    }

    private static DiagnosticInfo SupersededByPriorityDiagnostic(ITypeSymbol type, IMethodSymbol supersedingConstructor, string methodDisplayName, LocationInfo? location)
        => new(DiagnosticDescriptors.ComposeDataProfileConstructorSupersededByPriority, location, type.ToDisplayString(), methodDisplayName, supersedingConstructor.ToDisplayString());

    private static AotComposeDataMemberKind ToMemberKind(MemberResolutionKind kind) => kind switch
    {
        MemberResolutionKind.Field => AotComposeDataMemberKind.Field,
        MemberResolutionKind.Property => AotComposeDataMemberKind.Property,
        MemberResolutionKind.Method => AotComposeDataMemberKind.Method,
        _ => throw new System.InvalidOperationException($"Unexpected ComposeData member resolution kind '{kind}'."),
    };

    private enum MemberResolutionKind
    {
        Field,
        Property,
        Method,
        NotFound,
        Ambiguous,
        GenericMethod,
        RequiresArguments,
        NotPublicStatic,
    }

    private readonly record struct MemberResolution(MemberResolutionKind Kind, ISymbol? Member);

    private readonly record struct ReturnAnalysis(AotComposeDataReturnShape? Shape, ITypeSymbol? CollectionType, ITypeSymbol? ErrorType);

    private readonly record struct RowAnalysis(bool RowsAreTyped, ITypeSymbol[] RowElementTypes, EquatableArray<string> RowElementTypeNames, ITypeSymbol? ErrorType)
    {
        public static RowAnalysis Typed(ITypeSymbol[] elementTypes)
            => new(true, elementTypes, elementTypes.Select(static t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).ToEquatableArray(), null);

        public static RowAnalysis Untyped()
            => new(false, [], EquatableArray<string>.Empty, null);

        public static RowAnalysis Error(ITypeSymbol errorType)
            => new(false, [], EquatableArray<string>.Empty, errorType);
    }
}
