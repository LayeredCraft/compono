namespace Compono.NUnit.SampleTests;

// Reached only through CompositionTests' own [Compose]-attributed test method parameter - no
// [Composable], no Create<T>()/CreateMany<T>(), no direct CompositionRow call site anywhere else in
// this project. Proves Compono.Generators.ComposeMethodDiscovery's Compono.NUnit registrations
// (ComponoIncrementalGenerator.cs) generate a real plan through the packaged Compono.NUnit ->
// Compono dependency, not just Compono.Generators.Tests' isolated snapshot test. Mirrors
// Compono.MSTest.SampleTests/Domain.cs's own Repository/OrderService pair.
public sealed class Repository;

public sealed class OrderService
{
    public OrderService(Repository repository)
    {
        Repository = repository;
    }

    public Repository Repository { get; }
}
