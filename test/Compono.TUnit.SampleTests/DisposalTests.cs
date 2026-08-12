namespace Compono.TUnit.SampleTests;

// Real, running-TUnit proof of ADR-0040's disposal section, scoped exactly as that section
// documents: TUnit disposes a root [Compose]-composed argument itself after the test completes (no
// ITestEndEventReceiver in Compono.TUnit - see ComposeAttribute.cs), but its own ObjectGraphDiscoverer
// nested-object walk is scoped to IAsyncInitializer-registered properties only, not a general graph
// walk, so a non-[Shared] dependency nested inside a composed argument is disposed by no one. Uses a
// purpose-built plain IDisposable domain type (Domain.cs's TrackedResource), not a mocking-library
// substitute, per the user's explicit instruction for this verification's goal: proving TUnit's own
// disposal behavior, not exercising a mocking library's.
//
// [After(HookType.Class)] runs once after every [Test] in this class has completed (including its
// own argument disposal), so it's the correct place to observe the post-test disposal state - a
// [Test]'s own body only ever sees the pre-disposal state.
public sealed class DisposalTests
{
    private static TrackedResource? _rootArgument;
    private static TrackedResource? _nestedDependency;

    [Test]
    [Compose]
    public async Task RootComposedArgument_IsNotYetDisposed_WhileTheTestIsRunning(TrackedResource resource)
    {
        _rootArgument = resource;
        await Assert.That(resource.Disposed).IsFalse();
    }

    [Test]
    [Compose]
    public async Task NestedComposedDependency_IsNotYetDisposed_WhileTheTestIsRunning(ResourceHolder holder)
    {
        _nestedDependency = holder.Resource;
        await Assert.That(holder.Resource.Disposed).IsFalse();
    }

    [After(HookType.Class)]
    public static async Task RootArgumentIsDisposed_ButTheNestedDependencyIsNot()
    {
        await Assert.That(_rootArgument!.Disposed).IsTrue();
        await Assert.That(_nestedDependency!.Disposed).IsFalse();
    }
}
