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
}
