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

        // An interface member (of any kind) literally named "Configure" would silently shadow the
        // generated Configure() bridge - instance members always win over extension methods in
        // overload resolution. Amendment 3, Finding E.
        if (closure.SelectMany(i => i.GetMembers()).Any(m => m.Name == "Configure"))
        {
            return Failure(fullyQualifiedName, safeIdentifier, new DiagnosticInfo(
                DiagnosticDescriptors.TestDoubleConfigureMemberCollision, location, interfaceType.ToDisplayString()));
        }

        // A zero-argument configuration extension can't disambiguate an overloaded member.
        // Amendment 3, Finding D.
        var overloadedMethodNames = closure
            .SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())
            .Where(m => m.MethodKind == MethodKind.Ordinary)
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

                    case IMethodSymbol { MethodKind: not MethodKind.Ordinary }:
                        continue;

                    case IMethodSymbol method:
                    {
                        if (method.IsStatic)
                        {
                            if (method.IsAbstract)
                            {
                                return Failure(fullyQualifiedName, safeIdentifier,
                                    UnsupportedMember(interfaceType, member, "a static abstract member", location));
                            }

                            continue;
                        }

                        if (overloadedMethodNames.Contains(method.Name))
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
                            continue;

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

    private static DiagnosticInfo UnsupportedMember(INamedTypeSymbol interfaceType, ISymbol member, string shape, LocationInfo? location) =>
        new(DiagnosticDescriptors.UnsupportedTestDoubleMemberKind, location, interfaceType.ToDisplayString(), member.Name, shape);

    private static DiagnosticInfo ReturnShapeUnsupported(INamedTypeSymbol interfaceType, ISymbol member, string shape, LocationInfo? location) =>
        new(DiagnosticDescriptors.UnsupportedTestDoubleReturnShape, location, interfaceType.ToDisplayString(), member.Name, shape);
}
