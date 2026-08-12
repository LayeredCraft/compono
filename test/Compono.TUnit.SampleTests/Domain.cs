namespace Compono.TUnit.SampleTests;

// Reached only through ComposedTypeIsGeneratedThroughThePackagedDependency's own [Compose]-attributed
// theory parameter - no [Composable], no Create<T>()/CreateMany<T>(), no direct CompositionRow call
// site anywhere else in this project. Proves Compono.Generators.ComposeMethodDiscovery's TUnit
// registrations (ComponoIncrementalGenerator.cs) generate a real plan through the packaged
// Compono.TUnit -> Compono dependency, not just Compono.Generators.Tests' isolated snapshot test.
// Mirrors Compono.XunitV3.SampleTests/Domain.cs's own Repository/OrderService pair.
public sealed class Repository;

public sealed class OrderService
{
    public OrderService(Repository repository)
    {
        Repository = repository;
    }

    public Repository Repository { get; }
}

// Purpose-built plain disposable domain type (not a mocking-library substitute) - per the user's
// explicit instruction for PLAN-0040 Phase 2's disposal verification: prove TUnit disposes composed
// method arguments, not exercise a mocking library's own disposal behavior. Reused here for Phase 0's
// own root-vs-nested disposal proof (ADR-0040's disposal section).
public sealed class TrackedResource : IDisposable
{
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}

public sealed class ResourceHolder
{
    public ResourceHolder(TrackedResource resource)
    {
        Resource = resource;
    }

    public TrackedResource Resource { get; }
}
