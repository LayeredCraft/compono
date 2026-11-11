using System.Diagnostics.Metrics;

namespace Compono.Metrics.Tests;

public sealed class CapturingMeterFactoryTests
{
    [Fact]
    public void Create_Throws_WhenOptionsIsNull()
    {
        using var factory = new CapturingMeterFactory();

        var act = () => factory.Create(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_Throws_WhenScopeBelongsToAnotherFactory()
    {
        using var factory = new CapturingMeterFactory();
        var options = new MeterOptions("Shop") { Scope = new CapturingMeterFactory() };

        var act = () => factory.Create(options);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_ThrowsObjectDisposed_AfterDispose()
    {
        var factory = new CapturingMeterFactory();
        factory.Dispose();

        var act = () => factory.Create(new MeterOptions("Shop"));

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Create_DoesNotMutateCallerOptions()
    {
        using var factory = new CapturingMeterFactory();
        var options = new MeterOptions("Shop");

        var meter = factory.Create(options);

        options.Scope.Should().BeNull();
        meter.Scope.Should().BeSameAs(factory);
    }

    [Fact]
    public void Create_ReturnsSameInstance_ForSameNameVersionAndTags()
    {
        using var factory = new CapturingMeterFactory();
        var tags = new[] { new KeyValuePair<string, object?>("env", "test") };

        var first = factory.Create(new MeterOptions("Shop") { Version = "1.0", Tags = tags });
        var second = factory.Create(new MeterOptions("Shop") { Version = "1.0", Tags = tags });

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Create_TreatsTagOrderAsInsensitive()
    {
        using var factory = new CapturingMeterFactory();

        var first = factory.Create(new MeterOptions("Shop")
        {
            Tags = [
                new KeyValuePair<string, object?>("a", "1"),
                new KeyValuePair<string, object?>("b", "2"),
            ],
        });
        var second = factory.Create(new MeterOptions("Shop")
        {
            Tags = [
                new KeyValuePair<string, object?>("b", "2"),
                new KeyValuePair<string, object?>("a", "1"),
            ],
        });

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Create_ReturnsDifferentInstance_ForDifferentVersion()
    {
        using var factory = new CapturingMeterFactory();

        var first = factory.Create(new MeterOptions("Shop") { Version = "1.0" });
        var second = factory.Create(new MeterOptions("Shop") { Version = "2.0" });

        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public void Create_ReturnsDifferentInstance_ForDifferentTags()
    {
        using var factory = new CapturingMeterFactory();

        var first = factory.Create(new MeterOptions("Shop")
        {
            Tags = [new KeyValuePair<string, object?>("env", "test")],
        });
        var second = factory.Create(new MeterOptions("Shop")
        {
            Tags = [new KeyValuePair<string, object?>("env", "prod")],
        });

        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public void Create_ReturnsSameInstance_ForDuplicateKeysWithIdenticalValues()
    {
        using var factory = new CapturingMeterFactory();

        var first = factory.Create(new MeterOptions("Shop")
        {
            Tags = [
                new KeyValuePair<string, object?>("k", "1"),
                new KeyValuePair<string, object?>("k", "1"),
            ],
        });
        var second = factory.Create(new MeterOptions("Shop")
        {
            Tags = [
                new KeyValuePair<string, object?>("k", "1"),
                new KeyValuePair<string, object?>("k", "1"),
            ],
        });

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Create_ReturnsDifferentInstance_ForDuplicateKeysWithDifferentSecondEntry()
    {
        using var factory = new CapturingMeterFactory();

        var first = factory.Create(new MeterOptions("Shop")
        {
            Tags = [
                new KeyValuePair<string, object?>("k", "1"),
                new KeyValuePair<string, object?>("k", "1"),
            ],
        });
        var second = factory.Create(new MeterOptions("Shop")
        {
            Tags = [
                new KeyValuePair<string, object?>("k", "1"),
                new KeyValuePair<string, object?>("j", "2"),
            ],
        });

        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public void Create_ReturnsDifferentInstance_ForDuplicateKeysWithDifferentCount()
    {
        using var factory = new CapturingMeterFactory();

        var first = factory.Create(new MeterOptions("Shop")
        {
            Tags = [
                new KeyValuePair<string, object?>("k", "1"),
                new KeyValuePair<string, object?>("k", "1"),
            ],
        });
        var second = factory.Create(new MeterOptions("Shop")
        {
            Tags = [new KeyValuePair<string, object?>("k", "1")],
        });

        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var factory = new CapturingMeterFactory();
        factory.Create(new MeterOptions("Shop"));

        var act = () =>
        {
            factory.Dispose();
            factory.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Create_IsThreadSafe_ReturnsSingleInstanceForConcurrentCallers()
    {
        using var factory = new CapturingMeterFactory();
        var results = new Meter[32];

        Parallel.For(0, results.Length, i => results[i] = factory.Create(new MeterOptions("Shop")));

        results.Distinct(ReferenceEqualityComparer.Instance).Should().ContainSingle();
    }

    [Fact]
    public void CreateAndDisposeConcurrently_DisposesSafely_AndRejectsLaterCreate()
    {
        var factory = new CapturingMeterFactory();
        using var barrier = new Barrier(2);
        Exception? unexpected = null;

        Parallel.Invoke(
            () =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < 200; i++)
                {
                    try
                    {
                        factory.Create(new MeterOptions("Shop"));
                    }
                    catch (ObjectDisposedException)
                    {
                        // Disposal may win the race at any point; that is the documented outcome.
                        break;
                    }
                    catch (Exception exception)
                    {
                        unexpected = exception;
                        break;
                    }
                }
            },
            () =>
            {
                barrier.SignalAndWait();
                factory.Dispose();
            });

        unexpected.Should().BeNull();

        var act = () => factory.Create(new MeterOptions("Shop"));
        act.Should().Throw<ObjectDisposedException>();
    }
}
