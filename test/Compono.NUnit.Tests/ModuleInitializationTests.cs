using System.Collections.Concurrent;
using Compono.NUnit.SignatureFixtures;
using Compono.NUnit.Tests.Fixtures;
using NUnit.Framework;

namespace Compono.NUnit.Tests;

/// <summary>
/// Pins the module selection of the classic-VSTest discovery fix (ADR-0059 Amendment 1): before
/// binding, <c>ComposeAttribute.BuildFrom</c> forces the <em>declaring test method's</em> module to
/// initialize via <c>RuntimeHelpers.RunModuleConstructor</c>, because the classic VSTest test host
/// builds test cases before the test assembly's own generated <c>[ModuleInitializer]</c>s have run
/// and so the consumer assembly's <c>RowInvokerRegistry</c>/<c>PlanCache</c> registrations (emitted
/// into that declaring assembly) are still missing. Each shape below is a case where the declaring
/// type/module could plausibly differ from the calling test's own: an inherited method, a nested
/// type, a closed generic fixture, and a method declared in a different assembly. Every test asserts
/// the selected module is exactly the declaring type's own module - the one the generator populates -
/// and that <c>BuildFrom</c> still produces its single row.
/// </summary>
[TestFixture]
public sealed class ModuleInitializationTests
{
    [Test]
    public void BuildFrom_TargetsTheDeclaringModule_ForAnInheritedComposeMethod()
    {
        // Reflection resolves the inherited method through the derived type, but the method itself is
        // declared by the base type - BuildFrom must initialize the base type's module, which is where
        // the generator emitted this method's registrations.
        var method = typeof(InheritedComposeDerived).GetMethod(nameof(InheritedComposeBase.InheritedComposeMethod))!;

        Assert.That(method.DeclaringType, Is.EqualTo(typeof(InheritedComposeBase)));
        Assert.That(method.DeclaringType!.Module, Is.SameAs(typeof(InheritedComposeBase).Module));

        var rows = new ComposeAttribute().BuildFrom(MethodInfoWrapper.Wrap(method), null).ToArray();

        Assert.That(rows, Has.Length.EqualTo(1));
    }

    [Test]
    public void BuildFrom_TargetsTheNestedTypeModule_ForANestedComposeMethod()
    {
        var nested = typeof(NestedComposeContainer.NestedComposeTests);
        var method = nested.GetMethod(nameof(NestedComposeContainer.NestedComposeTests.NestedComposeMethod))!;

        Assert.That(method.DeclaringType, Is.EqualTo(nested));
        Assert.That(method.DeclaringType!.Module, Is.SameAs(nested.Module));

        var rows = new ComposeAttribute().BuildFrom(MethodInfoWrapper.Wrap(method), null).ToArray();

        Assert.That(rows, Has.Length.EqualTo(1));
    }

    [Test]
    public void BuildFrom_TargetsTheGenericDefinitionModule_ForAClosedGenericFixtureMethod()
    {
        // A closed generic fixture's method reports the constructed type as its DeclaringType, but that
        // constructed type's Module is still the generic type definition's assembly module - the one
        // holding the generated registrations.
        var closed = typeof(GenericComposeProbe<string>);
        var method = closed.GetMethod(nameof(GenericComposeProbe<string>.ComposedProbe))!;

        Assert.That(method.DeclaringType, Is.EqualTo(closed));
        Assert.That(method.DeclaringType!.Module, Is.SameAs(typeof(GenericComposeProbe<>).Module));

        var rows = new ComposeAttribute().BuildFrom(MethodInfoWrapper.Wrap(method), null).ToArray();

        Assert.That(rows, Has.Length.EqualTo(1));
    }

    [Test]
    public void BuildFrom_TargetsTheDeclaringAssemblyModule_ForAMethodDeclaredInAnotherAssembly()
    {
        var method = typeof(CrossAssemblyComposeFixtures).GetMethod(nameof(CrossAssemblyComposeFixtures.CrossAssemblyComposed))!;

        Assert.That(method.DeclaringType!.Module, Is.Not.SameAs(typeof(ModuleInitializationTests).Module));
        Assert.That(method.DeclaringType!.Module, Is.SameAs(typeof(CrossAssemblyComposeFixtures).Module));

        var rows = new ComposeAttribute().BuildFrom(MethodInfoWrapper.Wrap(method), null).ToArray();

        Assert.That(rows, Has.Length.EqualTo(1));
    }

    [Test]
    public void BuildFrom_InitializesModulesIdempotently_UnderConcurrentDiscovery()
    {
        // NUnit may build test cases concurrently; RunModuleConstructor is a no-op once a module is
        // initialized, so hammering BuildFrom across the shapes from many threads must never throw and
        // must always produce exactly one row per invocation.
        var methods = new[]
        {
            MethodInfoWrapper.Wrap(typeof(InheritedComposeBase).GetMethod(nameof(InheritedComposeBase.InheritedComposeMethod))!),
            MethodInfoWrapper.Wrap(typeof(NestedComposeContainer.NestedComposeTests).GetMethod(nameof(NestedComposeContainer.NestedComposeTests.NestedComposeMethod))!),
            MethodInfoWrapper.Wrap(typeof(GenericComposeProbe<string>).GetMethod(nameof(GenericComposeProbe<string>.ComposedProbe))!),
            MethodInfoWrapper.Wrap(typeof(CrossAssemblyComposeFixtures).GetMethod(nameof(CrossAssemblyComposeFixtures.CrossAssemblyComposed))!),
        };

        const int iterations = 400;
        var rowCounts = new ConcurrentBag<int>();
        var failures = new ConcurrentBag<Exception>();

        Parallel.For(0, iterations, i =>
        {
            try
            {
                var attribute = new ComposeAttribute();
                rowCounts.Add(attribute.BuildFrom(methods[i % methods.Length], null).Count());
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        });

        Assert.That(failures, Is.Empty);
        Assert.That(rowCounts, Has.Count.EqualTo(iterations));
        Assert.That(rowCounts, Has.All.EqualTo(1));
    }
}

/// <summary>Base type declaring an inherited <c>[Compose]</c> method (ADR-0059 §7 - no <c>[TestFixture]</c> needed).</summary>
public class InheritedComposeBase
{
    [Compose]
    public void InheritedComposeMethod(int number, string text) => Assert.That(text, Is.Not.Null);
}

/// <summary>Derived type with no own test members - discovers the base's inherited <c>[Compose]</c> method.</summary>
public sealed class InheritedComposeDerived : InheritedComposeBase;

/// <summary>Container for a nested <c>[Compose]</c>-bearing test type.</summary>
public sealed class NestedComposeContainer
{
    public sealed class NestedComposeTests
    {
        [Compose]
        public void NestedComposeMethod(int number, string text) => Assert.That(text, Is.Not.Null);
    }
}

/// <summary>
/// Generic fixture shape for module selection - intentionally carries no <c>[Compose]</c> attribute so
/// NUnit never tries to discover/run the open generic type; <see cref="ModuleInitializationTests"/>
/// reflects over its closed form directly.
/// </summary>
public sealed class GenericComposeProbe<T>
{
    public void ComposedProbe(T value, int number)
    {
    }
}
