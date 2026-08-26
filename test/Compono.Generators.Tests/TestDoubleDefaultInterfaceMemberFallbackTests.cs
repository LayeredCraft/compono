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

    [Fact]
    public Task NoSurfacePropertyDim_RoutesThroughDimHelper_ReportsCmp0029()
    {
        // A concrete default property whose name collides with a zero-argument method inherited by
        // the same leaf interface (ADR-0044's zero-argument-extension-collision disposition,
        // CMP0029) - the collision withholds Value's Configure()/Verify() surface
        // (hasPropertyConfigurationSurface = false), but Value is still, independently, a standalone
        // DIM fallback target (its own identity group has exactly one member - no base/derived
        // redeclaration at all). Round-6 code-review finding: the template's no-surface property
        // branch never checked is_dim_fallback_target, so this shape always emitted
        // `get => {{ default_expression }}` (0 for int) instead of routing through Value's own real
        // body (7) via the generated dispatch helper - verified here by snapshotting the generated
        // getter, the same way RefReadOnlySiblingParameter_PreservesModifier_ConsumerCompiles above
        // verifies its own template-routing fix.
        const string noSurfacePropertySource = """
            namespace TestNamespace;

            public interface IHasValueProp
            {
                int Value => 7;
            }

            public interface IHasValueMethod
            {
                int Value();
            }

            public interface ILeaf : IHasValueProp, IHasValueMethod
            {
            }

            public sealed class Consumer
            {
                public Consumer(ILeaf leaf) { }
            }

            public static class EntryPoint
            {
                public static void Run()
                {
                    Compono.Composer.Create().Create<Consumer>();
                }
            }
            """;

        return GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = noSurfacePropertySource,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0029",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task DimHelperFieldNameCollidesWithRealMember_ReportsCmp0035_ConsumerCompiles()
    {
        // Foo is a standalone concrete DIM (own identity group of one) - its generated dispatch-
        // helper field derives "__Foo_dimHelper" from Foo's own FieldName ("__Foo"). A real sibling
        // member literally named "Foo_dimHelper" independently derives that exact same field name
        // via the ordinary FieldName formula ("__Foo_dimHelper"), which would otherwise emit two
        // fields with the same name (CS0102). Round-6 code-review finding.
        const string collisionSource = """
            namespace TestNamespace;

            public interface ICollision
            {
                bool Foo() => true;

                void Foo_dimHelper();
            }

            public sealed class Consumer
            {
                public Consumer(ICollision handler) { }
            }

            public static class EntryPoint
            {
                public static void Run()
                {
                    Compono.Composer.Create().Create<Consumer>();
                }
            }
            """;

        return GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = collisionSource,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0035",
            TestContext.Current.CancellationToken);
    }

    private const string VoidStandaloneDimSource = """
        namespace TestNamespace;

        public static class FooCallCounter
        {
            public static int Count;
        }

        // A standalone concrete void DIM, argument-independent - Amendment 20's dispatch-helper
        // fallback path for this exact shape. Round-7 code-review finding (P1): this branch only
        // checked HasConfiguredException before invoking the real body, never HasConfiguredValue -
        // `.Returns(default)` sets HasConfiguredValue (a `Unit`) on a void member's slot even though
        // there's nothing to return, meaning a consumer configuring an explicit no-op still had the
        // real DIM body (with its own side effects) invoked underneath. Mirrors the matching-eligible
        // void branch's own long-established HasConfiguredValue early return.
        public interface IVoidDim
        {
            void Foo() => FooCallCounter.Count++;
        }

        public sealed class Consumer
        {
            public Consumer(IVoidDim handler) { }
        }

        public static class EntryPoint
        {
            private static void Discover() => Compono.Composer.Create().Create<Consumer>();

            public static object CreateDouble()
            {
                Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IVoidDim), out var value);
                return value!;
            }
        }
        """;

    [Fact]
    public void ConfiguredVoidDimNoOp_HonorsConfiguredValue_DoesNotInvokeRealBody()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = VoidStandaloneDimSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var handler = (IVoidDim)CreateDouble();
                        handler.Configure().Foo().Returns(default);
                        handler.Foo();
                        return FooCallCounter.Count;
                    }

                    public static object CreateDouble()
                    """),
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        result.Should().Be(0);
    }

    [Fact]
    public void UnconfiguredVoidDim_StillInvokesRealBody()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = VoidStandaloneDimSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var handler = (IVoidDim)CreateDouble();
                        handler.Foo();
                        return FooCallCounter.Count;
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

    [Fact]
    public void ParameterNameShadowsDimHelperCacheField_ConsumerCompiles()
    {
        // A real, non-ref parameter literally named after the derived dispatch-helper cache field
        // ("__Foo_dimHelper") shadows it inside Foo's own generated body - the unqualified
        // `{{ member.field_name }}_dimHelper ??= ...` bound to the parameter instead of the field,
        // which is either a compile error (wrong type) or, worse, a silent miscompile. Round-7
        // code-review finding: fixed by qualifying every dimHelper cache access with `this.`.
        const string paramShadowSource = """
            namespace TestNamespace;

            public interface IParamShadowsDimHelper
            {
                bool Foo(int __Foo_dimHelper) => true;
            }

            public sealed class Consumer
            {
                public Consumer(IParamShadowsDimHelper handler) { }
            }

            public static class EntryPoint
            {
                private static void Discover() => Compono.Composer.Create().Create<Consumer>();

                public static object CreateDouble()
                {
                    Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IParamShadowsDimHelper), out var value);
                    return value!;
                }
            }
            """;

        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = paramShadowSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var handler = (IParamShadowsDimHelper)CreateDouble();
                        return handler.Foo(1);
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
    public void SiblingParameterNameShadowsOwnerField_ConsumerCompiles()
    {
        // A sibling member's parameter literally named "_owner" shadows the dispatch helper's own
        // `_owner` field inside the sibling-forwarding expression the helper class must generate to
        // satisfy the interface's full contract. Round-7 code-review finding: fixed by qualifying
        // every `_owner` reference in the sibling-forwarding template with `this.`.
        const string siblingParamShadowSource = """
            namespace TestNamespace;

            public interface IParamShadowsOwnerField
            {
                bool Flag() => true;

                void Visit(int _owner);
            }

            public sealed class Consumer
            {
                public Consumer(IParamShadowsOwnerField handler) { }
            }

            public static class EntryPoint
            {
                private static void Discover() => Compono.Composer.Create().Create<Consumer>();

                public static object CreateDouble()
                {
                    Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IParamShadowsOwnerField), out var value);
                    return value!;
                }
            }
            """;

        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = siblingParamShadowSource.Replace(
                    "public static object CreateDouble()",
                    """
                    public static object Run()
                    {
                        var handler = (IParamShadowsOwnerField)CreateDouble();
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

    [Fact]
    public Task RefSiblingParameter_RestatesCallSiteModifier_ConsumerCompiles()
    {
        // Unlike `ref readonly` (never restated at a call site), `ref`/`out` MUST be restated at
        // the call site or the call doesn't bind at all (CS1620). Round-8 code-review finding: the
        // sibling-forwarding render model (TestDoubleEmitter's DimFallbackSiblings projection)
        // never included CallSiteRefKindPrefix at all, so a `ref`/`out` sibling's forwarding call
        // always omitted the modifier entirely, regardless of the earlier declaration-vs-call-site
        // fix - that fix only ever reached the top-level member's OWN forwarding, never a sibling's.
        //
        // Needs a same-named overload (Visit(string)) for the same reason
        // RefReadOnlySiblingParameter_PreservesModifier_ConsumerCompiles above does - a solo
        // ref/out/in member rejects the whole interface (CMP0026) before ever reaching the
        // sibling-forwarding code this test targets.
        return GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public interface IBase6
                    {
                        bool Flag() => true;

                        void Visit(ref int value);
                        void Visit(string label);
                    }

                    public sealed class Consumer
                    {
                        public Consumer(IBase6 handler) { }
                    }

                    public static class EntryPoint
                    {
                        public static void Run()
                        {
                            Compono.Composer.Create().Create<Consumer>();
                            Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IBase6), out var value);
                            ((IBase6)value!).Configure().Visit(Compono.Match.Any<string>());
                        }
                    }
                    """,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0030",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task RefReadOnlyDimTarget_CallSiteRestatesIn_NoCs9192Warning()
    {
        // Unlike `in`/`scoped`/`[UnscopedRef]`, a `ref readonly` parameter's call site DOES need a
        // modifier restated - omitting it compiles but emits CS9192, a real warning under a
        // consumer's default settings and a real build failure under warnings-as-errors. Round-9
        // code-review finding, fresh evidence surfacing after the earlier call-site-prefix fix: the
        // approved RefReadOnlySiblingParameter snapshot itself now emitted the bare, warning-
        // producing form. GeneratorTestHelpers.VerifyWithNoWarnings (unlike CompileAndExecute/
        // VerifyWithInfoDiagnostic, which only ever check Error severity) is the only existing
        // helper that would have caught this - it recompiles the generated output and asserts zero
        // diagnostics of the given ids at ANY severity.
        // Reuses RefReadOnlySiblingParameter_PreservesModifier_ConsumerCompiles's own shape (a
        // standalone concrete DIM, Flag(), with an abstract ref-readonly sibling the dispatch
        // helper must forward to) - that test only asserted CMP0030 plus zero-Error compilation;
        // this one specifically asserts zero CS9192 at ANY severity via VerifyWithNoWarnings, the
        // only existing helper that checks warnings, not just errors.
        const string refReadOnlyDimSource = """
            namespace TestNamespace;

            public interface IBase8
            {
                bool Flag() => true;

                void Visit(ref readonly int value);
                void Visit(string label);
            }

            public sealed class Consumer
            {
                public Consumer(IBase8 handler) { }
            }

            public static class EntryPoint
            {
                public static void Run()
                {
                    Compono.Composer.Create().Create<Consumer>();
                    Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IBase8), out var value);
                    ((IBase8)value!).Configure().Visit(Compono.Match.Any<string>());
                }
            }
            """;

        return GeneratorTestHelpers.VerifyWithNoWarnings(
            new CodeGenerationOptions
            {
                SourceCode = refReadOnlyDimSource,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            ["CS9192"],
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task DimDeclaringInterfaceHasUnresolvedStaticAbstractMember_ReportsCmp0036_ConsumerCompiles()
    {
        // A DIM's declaring interface (IBase7) inherits a static abstract member (IHasStatic.Flag)
        // that only the more-derived leaf interface (ILeaf7) resolves - the outer double compiles
        // fine (it implements ILeaf7, which DOES resolve Flag per ADR-0046), but the DIM fallback
        // dispatch helper implements only IBase7, which - viewed in isolation - never resolves
        // Flag, and the generator never emits a static implementation for it. Round-9 code-review
        // finding. Fixed by excluding this DIM from being a fallback target when its declaring
        // interface itself leaves a static abstract member unresolved, reporting CMP0036 and
        // gracefully degrading to the ordinary computed-default fallback instead of emitting code
        // that fails to compile.
        const string unresolvedStaticAbstractSource = """
            namespace TestNamespace;

            public interface IHasStatic7
            {
                static abstract bool Flag();
            }

            public interface IBase7 : IHasStatic7
            {
                bool CanHandle(string input) => true;
            }

            public interface ILeaf7 : IBase7
            {
                static bool IHasStatic7.Flag() => true;
            }

            public sealed class Consumer
            {
                public Consumer(ILeaf7 handler) { }
            }

            public static class EntryPoint
            {
                public static void Run()
                {
                    Compono.Composer.Create().Create<Consumer>();
                }
            }
            """;

        return GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = unresolvedStaticAbstractSource,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0036",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task DimOwnInterfaceDeclaresUnresolvedStaticAbstractMember_ReportsCmp0036_ConsumerCompiles()
    {
        // Narrower than DimDeclaringInterfaceHasUnresolvedStaticAbstractMember above: there, the
        // unresolved static abstract member is INHERITED by the DIM's declaring interface from a
        // separate base interface. Here, IBase9 declares BOTH the static abstract member AND the
        // DIM directly, itself, with no separate base interface at all - only the more-derived leaf
        // (ILeaf9) resolves it. Round-10 code-review finding: the original fix's
        // `declaringInterface.AllInterfaces` check excludes the declaring interface itself, so this
        // narrower, one-interface-closer case still generated a dispatch helper implementing IBase9
        // directly, missing Flag() and failing the consumer's compilation.
        const string ownInterfaceUnresolvedStaticAbstractSource = """
            namespace TestNamespace;

            public interface IBase9
            {
                static abstract bool Flag();

                bool CanHandle(string input) => true;
            }

            public interface ILeaf9 : IBase9
            {
                static bool IBase9.Flag() => true;
            }

            public sealed class Consumer
            {
                public Consumer(ILeaf9 handler) { }
            }

            public static class EntryPoint
            {
                public static void Run()
                {
                    Compono.Composer.Create().Create<Consumer>();
                }
            }
            """;

        return GeneratorTestHelpers.VerifyWithInfoDiagnostic(
            new CodeGenerationOptions
            {
                SourceCode = ownInterfaceUnresolvedStaticAbstractSource,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "CMP0036",
            TestContext.Current.CancellationToken);
    }
}
