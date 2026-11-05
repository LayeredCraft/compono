using Xunit.Sdk;

namespace Compono.XunitV3.Tests;

// Unit coverage for the shared RequireFixture<TFixture>() accessor (ADR-0069 / PLAN-0070). Every
// failure branch is exercised here once, against an NSubstitute fake ITestContext - the logic lives
// in one shared file, so real-runner tests cover only that the wiring works on each xUnit engine.
public sealed class FixtureContextExtensionsTests
{
    [Fact]
    public void RequireFixture_ThrowsArgumentNullException_WhenContextIsNull()
    {
        var context = (ITestContext)null!;

        var act = () => context.RequireFixture<SampleFixture>();

        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public void RequireFixture_ThrowsInvalidOperationExceptionNamingFixtureAndStage_WhenNoTestClassIsActive()
    {
        var context = Substitute.For<ITestContext>();
        context.TestClass.Returns((ITestClass?)null);
        context.PipelineStage.Returns(TestPipelineStage.TestExecution);

        var act = () => context.RequireFixture<SampleFixture>();

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain(typeof(SampleFixture).FullName!).And.Contain("TestExecution");
        context.DidNotReceive().GetFixture(Arg.Any<Type>());
    }

    [Fact]
    public void RequireFixture_ThrowsInvalidOperationException_WithoutWaiting_WhenFixtureInitializationIsIncomplete()
    {
        var pending = new TaskCompletionSource<object?>();
        var (context, _) = NewContextWithActiveTestClass();
        context.GetFixture(typeof(SampleFixture)).Returns(new ValueTask<object?>(pending.Task));

        var act = () => context.RequireFixture<SampleFixture>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*has not finished initializing*");
        pending.Task.IsCompleted.Should().BeFalse("an incomplete fixture ValueTask must never be waited on");
    }

    [Fact]
    public void RequireFixture_RethrowsTheOriginalExceptionInstance_WhenFixtureInitializationFaulted()
    {
        var original = new InvalidOperationException("fixture initialization failed");
        var (context, _) = NewContextWithActiveTestClass();
        context.GetFixture(typeof(SampleFixture)).Returns(ValueTask.FromException<object?>(original));

        var act = () => context.RequireFixture<SampleFixture>();

        act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(original);
    }

    [Fact]
    public void RequireFixture_ThrowsOperationCanceledException_WhenFixtureInitializationWasCanceled()
    {
        var (context, _) = NewContextWithActiveTestClass();
        context.GetFixture(typeof(SampleFixture))
            .Returns(ValueTask.FromCanceled<object?>(new CancellationToken(canceled: true)));

        var act = () => context.RequireFixture<SampleFixture>();

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void RequireFixture_ThrowsInvalidOperationExceptionWithDeclarationGuidance_WhenNoFixtureOfTheExactTypeExists()
    {
        var (context, testClass) = NewContextWithActiveTestClass();
        context.GetFixture(typeof(SampleFixture)).Returns(ValueTask.FromResult<object?>(null));

        var act = () => context.RequireFixture<SampleFixture>();

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should()
            .Contain(testClass.TestClassName)
            .And.Contain(typeof(SampleFixture).FullName!)
            .And.Contain("IClassFixture<")
            .And.Contain("ICollectionFixture<")
            .And.Contain("AssemblyFixture")
            .And.Contain("exact type");
    }

    [Fact]
    public void RequireFixture_ThrowsInvalidOperationExceptionNamingBothTypes_WhenTheLookedUpValueIsNotTheRequestedType()
    {
        var (context, _) = NewContextWithActiveTestClass();
        context.GetFixture(typeof(SampleFixture)).Returns(ValueTask.FromResult<object?>(new OtherFixture()));

        var act = () => context.RequireFixture<SampleFixture>();

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should()
            .Contain(typeof(SampleFixture).FullName!)
            .And.Contain(typeof(OtherFixture).FullName!);
    }

    [Fact]
    public void RequireFixture_ReturnsTheFixture_UsingOnlyTheNonGenericLookup()
    {
        var fixture = new SampleFixture();
        var (context, _) = NewContextWithActiveTestClass();
        context.GetFixture(typeof(SampleFixture)).Returns(ValueTask.FromResult<object?>(fixture));

        var result = context.RequireFixture<SampleFixture>();

        result.Should().BeSameAs(fixture);

        // The accessor deliberately uses the non-generic GetFixture(Type) overload so it can inspect
        // IsCompleted and honor its never-wait contract, and so its semantics are identical across the
        // whole supported xUnit range. The generic overload does exist (an instance member on xUnit
        // 4.x, and xUnit's own async GetFixture<TFixture>() extension on the 3.2.2 floor), so guard
        // that this implementation never routes through it.
        context.Received(1).GetFixture(typeof(SampleFixture));
        context.DidNotReceive().GetFixture<SampleFixture>();
    }

    private static (ITestContext Context, ITestClass TestClass) NewContextWithActiveTestClass()
    {
        var testClass = Substitute.For<ITestClass>();
        testClass.TestClassName.Returns("Compono.XunitV3.Tests.SampleTestClass");

        var context = Substitute.For<ITestContext>();
        context.TestClass.Returns(testClass);

        return (context, testClass);
    }

    private sealed class SampleFixture;

    private sealed class OtherFixture;
}
