using Compono.Generators.Diagnostics;
using Compono.Generators.Emitters;
using Compono.Generators.Models;
using Compono.Generators.Types;
using Microsoft.CodeAnalysis;

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
        if (closure.SelectMany(i => i.GetMembers())
            .Any(m => m.Name == "Configure" && (m is not IMethodSymbol method || IsApplicableToZeroArguments(method))))
        {
            return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                DiagnosticDescriptors.TestDoubleConfigureMemberCollision, location, interfaceType.ToDisplayString()));
        }

        // A zero-argument configuration extension can't disambiguate an overloaded member - and
        // that's not just a method-vs-method concern: two properties of the same name inherited
        // from different base interfaces (a diamond shape) would emit the same backing field and
        // the same zero-argument configuration extension just as surely as an overloaded method
        // would, so both member kinds feed the same name-collision check together. Filtered to the
        // same instance-contract eligibility the emission loop below applies (abstract, or a public
        // non-static default implementation) - a private or non-abstract-static default-interface
        // member never gets its own field/extension at all, so counting it here would falsely flag a
        // real, public, emitted member as "overloaded" against a same-named member that generates
        // nothing. PR #83 review round 3.
        var duplicateConfigurationMemberNames = closure
            .SelectMany(i => i.GetMembers())
            .Where(m => m is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol { IsIndexer: false })
            .Where(m => m.IsAbstract || (!m.IsStatic && m.DeclaredAccessibility == Accessibility.Public))
            .GroupBy(m => m.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        var members = new List<TestDoubleMemberInfo>();

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

                        if (duplicateConfigurationMemberNames.Contains(method.Name))
                        {
                            return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                DiagnosticDescriptors.OverloadedTestDoubleMember, location,
                                interfaceType.ToDisplayString(), method.Name));
                        }

                        if (method.IsGenericMethod)
                        {
                            return Failure(fullyQualifiedName, safeIdentifier,
                                UnsupportedMember(interfaceType, member, "a generic method", location));
                        }

                        foreach (var parameter in method.Parameters)
                        {
                            if (parameter.RefKind != RefKind.None)
                            {
                                return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                    DiagnosticDescriptors.UnsupportedTestDoubleParameterShape, location,
                                    interfaceType.ToDisplayString(), method.Name, parameter.Name,
                                    "as a ref/out/in parameter, which Compono cannot compose a value for"));
                            }

                            if (parameter.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
                            {
                                return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                    DiagnosticDescriptors.UnsupportedTestDoubleParameterShape, location,
                                    interfaceType.ToDisplayString(), method.Name, parameter.Name,
                                    "as a pointer or function-pointer type, which cannot be used as a generic type argument"));
                            }
                        }

                        if (method.ReturnsByRef || method.ReturnsByRefReadonly)
                        {
                            return Failure(fullyQualifiedName, safeIdentifier,
                                ReturnShapeUnsupported(interfaceType, method, "a by-ref return", location));
                        }

                        if (method.ReturnType.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
                        {
                            return Failure(fullyQualifiedName, safeIdentifier,
                                ReturnShapeUnsupported(interfaceType, method, "a pointer or function-pointer return type", location));
                        }

                        if (!method.ReturnsVoid && method.ReturnType.IsRefLikeType)
                        {
                            return Failure(fullyQualifiedName, safeIdentifier,
                                ReturnShapeUnsupported(interfaceType, method, "a ref struct (ref-like type) return type", location));
                        }

                        // Object-collision check compares the generated, always-zero-argument
                        // configuration extension's name against object's own zero-argument members -
                        // not the interface member's own declared signature. GetHashCode()/ToString()/
                        // GetType() are all zero-argument on object; Equals(object) is one-argument,
                        // so a zero-argument generated "Equals" extension does NOT collide with it.
                        // Amendment 6, Finding N.
                        if (method.Name is "ToString" or "GetHashCode" or "GetType")
                        {
                            return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                DiagnosticDescriptors.TestDoubleObjectMemberCollision, location,
                                interfaceType.ToDisplayString(), method.Name));
                        }

                        var isVoid = method.ReturnsVoid;
                        var returnTypeFullyQualifiedName = "";
                        var defaultExpression = "";

                        if (!isVoid)
                        {
                            returnTypeFullyQualifiedName = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                            if (!TestDoubleDefaults.TryGetDefaultExpression(method.ReturnType, out defaultExpression))
                            {
                                return Failure(fullyQualifiedName, safeIdentifier, ReturnShapeUnsupported(
                                    interfaceType, method, "a non-nullable reference type with no deterministic default", location));
                            }
                        }

                        var parameters = method.Parameters
                            .Select(p => new TestDoubleParameterInfo(
                                RequiredMemberCollector.EscapeIdentifier(p.Name),
                                p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                            .ToEquatableArray();

                        members.Add(new TestDoubleMemberInfo(
                            method.Name,
                            RequiredMemberCollector.EscapeIdentifier(method.Name),
                            declaringInterfaceFullyQualifiedName,
                            TestDoubleMemberKind.Method,
                            TestDoublePropertyAccessorKind.None,
                            returnTypeFullyQualifiedName,
                            isVoid,
                            defaultExpression,
                            parameters));

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

                        if (duplicateConfigurationMemberNames.Contains(property.Name))
                        {
                            return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                                DiagnosticDescriptors.OverloadedTestDoubleMember, location,
                                interfaceType.ToDisplayString(), property.Name));
                        }

                        // Same object-collision rule as the method branch above: the generated,
                        // always-zero-argument configuration extension's name is what can collide
                        // with an inherited object member, not the property's own declared shape.
                        // Previously only checked for methods - a property named ToString/GetHashCode/
                        // GetType silently lost its Configure() surface to the inherited object member
                        // instead of getting this diagnostic. Amendment 6, Finding N.
                        if (property.Name is "ToString" or "GetHashCode" or "GetType")
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

                        if (property.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
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
                        // exactly what the interface declares. Amendment 9, Finding U.
                        var accessorKind = property.SetMethod is null
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
                            property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            false,
                            propertyDefault,
                            EquatableArray<TestDoubleParameterInfo>.Empty));

                        break;
                    }
                }
            }
        }

        return new DiscoveredTestDoubleInfo(fullyQualifiedName, safeIdentifier, members.ToEquatableArray(), EquatableArray<DiagnosticInfo>.Empty);
    }

    private static DiscoveredTestDoubleInfo Failure(string fullyQualifiedName, string safeIdentifier, DiagnosticInfo diagnostic) =>
        new(fullyQualifiedName, safeIdentifier, EquatableArray<TestDoubleMemberInfo>.Empty, new[] { diagnostic }.ToEquatableArray());

    // Every parameter must either have a default value, or be the trailing `params` parameter (an
    // empty array is a valid argument for it) - the same rule the C# compiler itself applies when
    // deciding whether a zero-argument call is applicable to a method. PR #83 review round 4.
    private static bool IsApplicableToZeroArguments(IMethodSymbol method)
    {
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
