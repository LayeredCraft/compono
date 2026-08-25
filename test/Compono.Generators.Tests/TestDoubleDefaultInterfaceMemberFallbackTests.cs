using System.Reflection;

namespace Compono.Generators.Tests;

/// <summary>
/// Real end-to-end execution proving ADR-0044 Amendment 20's fix: a base interface's abstract
/// declaration resolved by a more-derived interface's own concrete (default-interface-member)
/// redeclaration via <see langword="new"/> no longer gets misclassified as a diamond collision
/// (<c>TestDoubleMemberIdentityResolver</c>) - the generated double honors the derived
/// interface's real, configurable member instead of silently discarding it for a wrong computed
/// default. Not a snapshot/compile check (<see cref="TestDoubleVerifyTests"/>); uses
/// <see cref="GeneratorTestHelpers.CompileAndExecute"/> the same way
/// <see cref="TestDoubleVerificationExecutionTests"/> does.
/// </summary>
public sealed class TestDoubleDefaultInterfaceMemberFallbackTests
{
    private const string BaseAbstractDerivedConcreteSource = """
        namespace TestNamespace;

        public interface IBase
        {
            bool CanHandle(string input);
        }

        public interface IDerived : IBase
        {
            new bool CanHandle(string input) => true;
        }

        public sealed class Consumer
        {
            public Consumer(IDerived derived) { }
        }

        public static class EntryPoint
        {
            private static void Discover() => Compono.Composer.Create().Create<Consumer>();

            public static object CreateDouble()
            {
                Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IDerived), out var value);
                return value!;
            }
        }
        """;

    [Fact]
    public void UnconfiguredDerivedDimView_ReturnsRealDimBody_NotWrongComputedDefault()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = BaseAbstractDerivedConcreteSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var derived = (IDerived)CreateDouble();
                        return derived.CanHandle("anything");
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(true);
    }

    [Fact]
    public void ConfiguredDerivedView_ReturnsConfiguredValue()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = BaseAbstractDerivedConcreteSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var derived = (IDerived)CreateDouble();
                        derived.Configure().CanHandle(global::Compono.Match.Any<string>()).Returns(false);
                        return derived.CanHandle("anything");
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(false);
    }

    [Fact]
    public void BaseInterfaceView_ForwardsToSameSharedState_NoDoubleRecording()
    {
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = BaseAbstractDerivedConcreteSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var derived = (IDerived)CreateDouble();
                        derived.Configure().CanHandle(global::Compono.Match.Any<string>()).Returns(true);

                        IBase baseView = derived;
                        baseView.CanHandle("x");

                        derived.Verify().CanHandle(global::Compono.Match.Any<string>()).Once();
                        return derived;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().NotThrow();
    }

    private const string ConvergentDiamondSource = """
        namespace TestNamespace;

        public interface IAncestor
        {
            bool Flag();
        }

        public interface IBranchA : IAncestor { }
        public interface IBranchB : IAncestor { }

        public interface ILeaf : IBranchA, IBranchB
        {
            new bool Flag() => true;
        }

        public sealed class Consumer
        {
            public Consumer(ILeaf leaf) { }
        }

        public static class EntryPoint
        {
            private static void Discover() => Compono.Composer.Create().Create<Consumer>();

            public static object CreateDouble()
            {
                Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(ILeaf), out var value);
                return value!;
            }
        }
        """;

    [Fact]
    public void ConvergentDiamondResolvedByLeafRedeclaration_ReturnsRealDimBody()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = ConvergentDiamondSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var leaf = (ILeaf)CreateDouble();
                        return leaf.Flag();
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(true);
    }

    [Fact]
    public void CallRecordingInvariant_UnconfiguredDimFallbackDispatch_RecordsExactlyOneCall()
    {
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = BaseAbstractDerivedConcreteSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var derived = (IDerived)CreateDouble();
                        derived.CanHandle("x");
                        derived.Verify().CanHandle(global::Compono.Match.Any<string>()).Once();
                        return derived;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().NotThrow();
    }

    private const string PropertyDimSource = """
        namespace TestNamespace;

        public interface IBaseProp
        {
            bool Flag { get; }
        }

        public interface IDerivedProp : IBaseProp
        {
            new bool Flag => true;
        }

        public sealed class Consumer
        {
            public Consumer(IDerivedProp derived) { }
        }

        public static class EntryPoint
        {
            private static void Discover() => Compono.Composer.Create().Create<Consumer>();

            public static object CreateDouble()
            {
                Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IDerivedProp), out var value);
                return value!;
            }
        }
        """;

    [Fact]
    public void PropertyDim_UnconfiguredView_ReturnsRealDimBody()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = PropertyDimSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var derived = (IDerivedProp)CreateDouble();
                        return derived.Flag;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(true);
    }

    private const string CrossMemberDimSource = """
        namespace TestNamespace;

        public interface IBase3
        {
            bool Flag();
            int Other();
        }

        public interface IDerived3 : IBase3
        {
            new bool Flag() => Other() > 0;
        }

        public sealed class Consumer
        {
            public Consumer(IDerived3 derived) { }
        }

        public static class EntryPoint
        {
            private static void Discover() => Compono.Composer.Create().Create<Consumer>();

            public static object CreateDouble()
            {
                Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IDerived3), out var value);
                return value!;
            }
        }
        """;

    [Fact]
    public void CrossMemberDimCallsAbstractSiblingThroughThis_ForwardsToOwnerAndRecordsOnce()
    {
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = CrossMemberDimSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var derived = (IDerived3)CreateDouble();
                        derived.Configure().Other().Returns(5);

                        var flag = derived.Flag();

                        if (flag != true)
                            throw new global::System.Exception("expected DIM body's Other() > 0 to be true");

                        derived.Verify().Other().Once();
                        return derived;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().NotThrow();
    }

    private const string NonPublicSiblingSetterDimSource = """
        namespace TestNamespace;

        public interface IBase4
        {
            bool Flag();

            // A default-implemented property whose setter isn't public - not part of the
            // implementable contract (same reasoning as a private default method), so the DIM
            // fallback dispatch helper built for the sibling Flag() below must treat this as
            // get-only, never emit a call through an inaccessible private setter. Code-review
            // finding: the sibling-collection path originally classified this GetSet purely from
            // `SetMethod is not null`, without checking `DeclaredAccessibility`.
            int Value { get => 0; private set { } }
        }

        public interface IDerived4 : IBase4
        {
            new bool Flag() => true;
        }

        public sealed class Consumer
        {
            public Consumer(IDerived4 derived) { }
        }

        public static class EntryPoint
        {
            private static void Discover() => Compono.Composer.Create().Create<Consumer>();

            public static object CreateDouble()
            {
                Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IDerived4), out var value);
                return value!;
            }
        }
        """;

    [Fact]
    public void NonPublicSiblingSetter_TreatedAsGetOnly_ConsumerCompiles()
    {
        // The regression itself is a consumer COMPILE failure (the generated dispatch helper
        // emitting an assignment through an inaccessible private setter) - CompileAndExecute
        // throwing at all (rather than the assertion below) is exactly what a reintroduction of
        // this bug looks like.
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = NonPublicSiblingSetterDimSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var derived = (IDerived4)CreateDouble();
                        return derived.Flag();
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().NotThrow();
    }

    [Fact]
    public Task RefReadOnlySiblingParameter_PreservesModifier_ConsumerCompiles()
    {
        // A `ref readonly` sibling abstract member the fallback dispatch helper must also
        // implement to satisfy IBase5's full contract - omitting the "ref readonly " modifier here
        // would emit Visit(int) instead of Visit(ref readonly int), which doesn't implement the
        // interface member at all (CS0535/signature mismatch). Code-review finding.
        //
        // Needs a same-named overload (Visit(string)) so this doesn't instead trip the pre-existing
        // "solo ref/out/in member rejects the whole interface" CMP0026 disposition (ADR-0044
        // Amendment 5) before ever reaching the sibling-forwarding code this test targets -
        // confirmed empirically: without the overload, the whole double generation is rejected
        // gracefully (CMP0026, no double at all), which isn't the failure mode under test here.
        // With the overload, generation proceeds and reports the narrower, scoped CMP0030 instead
        // (this specific overload has no Configure() surface, everything else is unaffected) - the
        // same established pattern TestDoubleVerifyTests.cs's own CMP0030 tests use
        // (VerifyWithInfoDiagnostic, which - unlike CompileAndExecute/Verify - tolerates that one
        // expected Info diagnostic while still proving the generated code actually compiles).
        return GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IBase5
                    {
                        bool Flag() => true;

                        void Visit(ref readonly int value);
                        void Visit(string label);
                    }

                    public sealed class Consumer
                    {
                        public Consumer(IBase5 handler) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            Compono.Composer.Create().Create<Consumer>();
                            Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IBase5), out var value);
                            ((IBase5)value!).Configure().Visit(Compono.Match.Any<string>());
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0030",
            TestContext.Current.CancellationToken);
    }

    private const string NonPublicSetterOnLosingPropertyDimSource = """
        namespace TestNamespace;

        public interface IBaseProp5
        {
            // Losing (non-dominant) declaration once IDerivedProp5 redeclares Value below - the
            // forwarding member the double emits for this losing declaration must not try to
            // implement the non-public setter (code-review finding: this forwarding branch
            // originally checked SetMethod is not null, without checking DeclaredAccessibility,
            // mirroring the sibling-collection bug fixed earlier in this same file).
            int Value { get => 0; private set { } }
        }

        public interface IDerivedProp5 : IBaseProp5
        {
            new int Value => 42;
        }

        public sealed class Consumer
        {
            public Consumer(IDerivedProp5 derived) { }
        }

        public static class EntryPoint
        {
            private static void Discover() => Compono.Composer.Create().Create<Consumer>();

            public static object CreateDouble()
            {
                Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IDerivedProp5), out var value);
                return value!;
            }
        }
        """;

    [Fact]
    public void NonPublicSetterOnLosingPropertyDeclaration_TreatedAsGetOnly_ConsumerCompiles()
    {
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = NonPublicSetterOnLosingPropertyDimSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var derived = (IDerivedProp5)CreateDouble();
                        return derived.Value;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().NotThrow();
    }

    private const string StandaloneConcreteDimSource = """
        namespace TestNamespace;

        // A plain, non-colliding default interface member - no base/derived redeclaration
        // anywhere, no identity group with more than one member at all. This is the common case
        // Amendment 20 was meant to cover in the first place (code-review finding, P1): the
        // original resolution loop only ever considered identity groups with MORE than one
        // member, so this ordinary shape never became a DIM fallback target and silently kept
        // ADR-0045's computed default (false) instead of its own real body (true).
        public interface IStandalone
        {
            bool Flag() => true;
        }

        public sealed class Consumer
        {
            public Consumer(IStandalone handler) { }
        }

        public static class EntryPoint
        {
            private static void Discover() => Compono.Composer.Create().Create<Consumer>();

            public static object CreateDouble()
            {
                Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IStandalone), out var value);
                return value!;
            }
        }
        """;

    [Fact]
    public void StandaloneConcreteDim_UnconfiguredView_ReturnsRealDimBody_NotComputedDefault()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = StandaloneConcreteDimSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var handler = (IStandalone)CreateDouble();
                        return handler.Flag();
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(true);
    }

    private const string StandaloneClosedInstantiationDimSource = """
        namespace TestNamespace;

        // A standalone (non-colliding) DIM whose return type depends on its own type parameter
        // (T?, constrained `where T : class`) - ADR-0049's closed-instantiation-eligible shape,
        // a THIRD template branch separate from the ordinary is_eligible_for_matching path the
        // other StandaloneConcreteDim test above covers. Round-5 code-review finding: this
        // branch's own fallback spots never checked is_dim_fallback_target at all, so an
        // unconfigured closed instantiation always fell through to ADR-0045's computed default
        // instead of running Get<T>()'s own real body. The real body and the computed default
        // both evaluate to null for a reference T, so the assertion below can't just check the
        // return value - it counts real-body executions instead, since the computed-default path
        // never executes the DIM body at all.
        public static class StandaloneGenericCallCounter
        {
            public static int Count;
        }

        public interface IStandaloneGeneric
        {
            T? Get<T>() where T : class
            {
                StandaloneGenericCallCounter.Count++;
                return default;
            }
        }

        public sealed class StandaloneGenericToken;

        public sealed class Consumer
        {
            public Consumer(IStandaloneGeneric handler) { }
        }

        public static class EntryPoint
        {
            private static void Discover() => Compono.Composer.Create().Create<Consumer>();

            public static object CreateDouble()
            {
                Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IStandaloneGeneric), out var value);
                return value!;
            }
        }
        """;

    [Fact]
    public void StandaloneClosedInstantiationDim_UnconfiguredView_ExecutesRealDimBody_NotComputedDefault()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = StandaloneClosedInstantiationDimSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var handler = (IStandaloneGeneric)CreateDouble();
                        handler.Get<StandaloneGenericToken>();
                        return StandaloneGenericCallCounter.Count;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(1);
    }
}
