using BenchmarkDotNet.Attributes;

namespace Compono.Benchmarks.FeatureOverhead;

public interface ISingleOverloadMember
{
    void Send(string message);
}

public interface IManySiblingOverloads
{
    void Send(string message);

    void Send(int retryCount, string message);

    void Send(string message, bool urgent);

    void Send(int retryCount, string message, bool urgent);

    void Send(Guid correlationId, string message);
}

/// <summary>
/// Isolates whether a member's own dispatch cost changes when it has several sibling overloads on
/// the same interface (ADR-0044's "AOT and performance" section, PLAN-0044 Phase 3) - each generated
/// overload is its own explicit interface implementation with its own backing field, so both arms
/// call the identical <c>Send(string)</c> shape; the only difference is how many other overloads
/// exist alongside it on the interface the generator walked.
/// </summary>
[MemoryDiagnoser]
public class OverloadDispatchOverheadBenchmarks
{
    private readonly Composer _composer = Composer.Create(builder => builder.UseGeneratedTestDoubles());
    private ISingleOverloadMember _single = null!;
    private IManySiblingOverloads _many = null!;

    [GlobalSetup]
    public void Setup()
    {
        _single = _composer.Create<ISingleOverloadMember>();
        _single.Configure().Send();

        _many = _composer.Create<IManySiblingOverloads>();
        _many.Configure().Send("hello");
    }

    /// <summary>Calls the one and only <c>Send(string)</c> overload on a single-member interface.</summary>
    [Benchmark(Baseline = true)]
    public void SingleOverloadMember() => _single.Send("hello");

    /// <summary>Calls the same-shaped <c>Send(string)</c> overload, alongside four siblings.</summary>
    [Benchmark]
    public void MemberWithFourSiblingOverloads() => _many.Send("hello");
}
