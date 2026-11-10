using Compono.Generators.Discovery;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Compono.Generators.Tests;

// PLAN-0071 B5.3: the xUnit AOT declared-type index renderer.
public sealed class AotComposeDataTestClassIndexTests
{
    private const string SampleSource = """
        public class GlobalType { }

        namespace IndexSample
        {
            public class Plain { }

            public class GenericBase<T> { }

            public class Two<T1, T2> { }

            public class OuterPlain
            {
                public class InnerGen<T> { }
            }

            public class Outer<T>
            {
                public class InnerBase { }
            }

            public class @class { }

            public class async { }

            public class record { }

            public class where { }

            public class @event<T> { }
        }
        """;

    private static string Render(string metadataName)
    {
        var (_, compilation) = ComposeDataAotTestHelper.CreateTrackingDriver(SampleSource);
        var type = compilation.GetTypeByMetadataName(metadataName);
        type.Should().NotBeNull();
        return AotComposeDataTestClassIndex.Render(type!);
    }

    [Theory]
    [InlineData("GlobalType", "global::GlobalType")]
    [InlineData("IndexSample.Plain", "global::IndexSample.Plain")]
    [InlineData("IndexSample.GenericBase`1", "global::IndexSample.GenericBase<>")]
    [InlineData("IndexSample.Two`2", "global::IndexSample.Two<,>")]
    [InlineData("IndexSample.OuterPlain+InnerGen`1", "global::IndexSample.OuterPlain.InnerGen<>")]
    [InlineData("IndexSample.Outer`1+InnerBase", "global::IndexSample.Outer<>.InnerBase")]
    [InlineData("IndexSample.class", "global::IndexSample.@class")]
    // `record` is deliberately absent here: whether FullyQualifiedFormat escapes it depends on the
    // Roslyn version (4.11 renders `record`, 5.x renders `@record`). The renderer takes its escaping
    // from the host compiler, exactly as xUnit's own generator does, so `record` is covered only by
    // the version-independent differential test below.
    [InlineData("IndexSample.async", "global::IndexSample.async")]
    [InlineData("IndexSample.where", "global::IndexSample.where")]
    [InlineData("IndexSample.event`1", "global::IndexSample.@event<>")]
    public void Render_ProducesExpectedIndex(string metadataName, string expected) =>
        Render(metadataName).Should().Be(expected);

    [Fact]
    public void Render_NonGenericChain_IsByteIdenticalToFullyQualifiedFormat()
    {
        var (_, compilation) = ComposeDataAotTestHelper.CreateTrackingDriver(SampleSource);
        var type = compilation.GetTypeByMetadataName("IndexSample.Plain")!;

        AotComposeDataTestClassIndex.Render(type)
            .Should().Be(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }

    [Theory]
    [InlineData("IndexSample.async")]
    [InlineData("IndexSample.record")]
    [InlineData("IndexSample.where")]
    [InlineData("IndexSample.class")]
    public void Render_NonGenericKeywordNames_AreByteIdenticalToFullyQualifiedFormat(string metadataName)
    {
        var (_, compilation) = ComposeDataAotTestHelper.CreateTrackingDriver(SampleSource);
        var type = compilation.GetTypeByMetadataName(metadataName)!;

        AotComposeDataTestClassIndex.Render(type)
            .Should().Be(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }
}
