using BenchmarkDotNet.Attributes;
using Compono.Benchmarks.Models;

namespace Compono.Benchmarks.FeatureOverhead;

/// <summary>
/// Isolates the incremental cost of one Compono configuration mechanism at a time, per ADR-0034 -
/// each step composes the same <see cref="MediumAggregate"/> model, adding exactly one mechanism
/// on top of <see cref="GeneratedOnly"/>'s plain composer. Every mechanism here can decline
/// without breaking composition (a member/type rule that doesn't match, a semantic provider that
/// declines) - each step's marginal cost is real, not required for the graph to compose at all.
/// </summary>
[MemoryDiagnoser]
public class ConfigurationOverheadBenchmarks
{
    private readonly Composer _generatedOnly = Composer.Create();

    private readonly Composer _plusMemberRule = Composer.Create(builder => builder
        .For<MediumAggregate>().Member(x => x.FirstName).Use("Fixed"));

    private readonly Composer _plusTypeRule = Composer.Create(builder => builder
        .For<Address>().Use(_ => new Address("Fixed St", "Fixed City")));

    private readonly Composer _plusCustomProvider = Composer.Create(builder => builder
        .AddSemanticProvider(new FirstNameProvider()));

    /// <summary>Composes <see cref="MediumAggregate"/> with no configuration - the floor this class' other benchmarks are compared against.</summary>
    [Benchmark(Baseline = true)]
    public MediumAggregate GeneratedOnly() => _generatedOnly.Create<MediumAggregate>();

    /// <summary>Composes <see cref="MediumAggregate"/> with one stage-4 member rule active.</summary>
    [Benchmark]
    public MediumAggregate PlusMemberRule() => _plusMemberRule.Create<MediumAggregate>();

    /// <summary>Composes <see cref="MediumAggregate"/> with one stage-4 type rule (on the nested <see cref="Address"/>) active.</summary>
    [Benchmark]
    public MediumAggregate PlusTypeRule() => _plusTypeRule.Create<MediumAggregate>();

    /// <summary>Composes <see cref="MediumAggregate"/> with one stage-5 semantic provider active.</summary>
    [Benchmark]
    public MediumAggregate PlusCustomProvider() => _plusCustomProvider.Create<MediumAggregate>();

    /// <summary>A minimal stage-5 provider claiming only members literally named <c>FirstName</c> - exercises the public provider extension point's own dispatch cost.</summary>
    private sealed class FirstNameProvider : ICompositionValueProvider
    {
        public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context) =>
            request.Name == "FirstName"
                ? CompositionProviderResult.Handled("Provided")
                : CompositionProviderResult.NotHandled;
    }
}
