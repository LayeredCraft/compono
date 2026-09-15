using System.Text;

namespace Compono.Generators.Tests;

/// <summary>
/// Regression coverage for issue #142 / RESEARCH-0034: a sufficiently large interface (the real
/// trigger was <c>Amazon.S3.IAmazonS3</c>, measured at 182 analyzed members) used to crash
/// <c>ComponoIncrementalGenerator</c> with a Scriban <c>ScriptRuntimeException</c> ("Exceeding
/// number of iteration limit `1000` for loop statement") because <c>TemplateHelper.Render</c>
/// never explicitly configured Scriban's <c>TemplateContext.LoopLimit</c> and so inherited
/// Scriban's own generic default (1000). Raising that limit alone then exposed a second,
/// independent inherited Scriban default - <c>TemplateContext.LimitToString</c> (1,048,576
/// characters) - which silently truncates total rendered output with no exception and no
/// diagnostic once hit (RESEARCH-0034 §17). This fixture is a synthetic ~250-member interface
/// built from the same dimensions RESEARCH-0034 found actually drive both Scriban limits (member
/// count, parameter count, and overload groups) - not a hand-picked shape, and deliberately not
/// dependent on the AWS SDK (RESEARCH-0034 §10).
///
/// This is a behavior-level regression test, not an implementation-detail one: it asserts
/// observable generation outcomes (no diagnostics, generated code compiles, the double is
/// actually composable/invocable, a plain <c>Configure()</c> path works, and an overloaded
/// member's discriminator-only + <c>...Matching(...)</c> surfaces coexist correctly) rather than
/// asserting anything about either Scriban limit's configured value, Scriban's internal step
/// count, or the per-member iteration formula RESEARCH-0034 §5 used only for diagnosis - so it
/// stays valid regardless of future internal template/analyzer changes.
/// </summary>
public sealed class TestDoubleLargeInterfaceGenerationTests
{
    /// <summary>
    /// Builds a synthetic interface of ~250 members with a parameter/overload mix representative
    /// of RESEARCH-0034 §7's measured real-<c>IAmazonS3</c> shape (majority 2-3 parameter methods,
    /// some 0/1-parameter methods, some 5+-parameter methods, a meaningful number of overload
    /// groups) - plus three fixed, known-named members (<c>Compute</c>, and the overloaded
    /// <c>Lookup</c> pair) that the test's <c>Run()</c> method exercises directly.
    /// </summary>
    private static string BuildLargeInterfaceSource()
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace TestNamespace;");
        sb.AppendLine();
        sb.AppendLine("public interface IBigInterface");
        sb.AppendLine("{");

        // Fixed, known-named members the test's Run() method configures/invokes directly.
        sb.AppendLine("    int Compute(int a, int b);");
        sb.AppendLine("    int Lookup(int a, int b);");
        sb.AppendLine("    int Lookup(int a, int b, int c);");

        // Plain (non-overloaded) filler members - parameter-count distribution modeled on
        // RESEARCH-0034 §7's real IAmazonS3 histogram (majority 2-3 params, some 0/1, some 5+).
        AppendPlainMethods(sb, "Filler_P0", count: 10, paramCount: 0);
        AppendPlainMethods(sb, "Filler_P1", count: 15, paramCount: 1);
        AppendPlainMethods(sb, "Filler_P2", count: 150, paramCount: 2);
        AppendPlainMethods(sb, "Filler_P3", count: 40, paramCount: 3);
        AppendPlainMethods(sb, "Filler_P5", count: 15, paramCount: 5);

        // Overload-group filler - each group is one method name with a 2-parameter and a
        // 3-parameter overload, exercising the same overload-safe-matching Configure()/Verify()
        // extension surfaces (PR #115 / ADR-0044 Amendment 21) whose added per-member loops were
        // the structural change that caused the regression (RESEARCH-0034 §4/§6).
        for (var i = 0; i < 10; i++)
        {
            sb.AppendLine($"    int Filler_OG_{i}(int p0, int p1);");
            sb.AppendLine($"    int Filler_OG_{i}(int p0, int p1, int p2);");
        }

        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("public sealed class OrderService");
        sb.AppendLine("{");
        sb.AppendLine("    public OrderService(IBigInterface big) { }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("public static class EntryPoint");
        sb.AppendLine("{");
        sb.AppendLine("    private static void Discover() => Compono.Composer.Create().Create<OrderService>();");
        sb.AppendLine();
        sb.AppendLine("    public static object CreateDouble()");
        sb.AppendLine("    {");
        sb.AppendLine("        Compono.GeneratedTestDoubleRegistry.TryCreate(typeof(IBigInterface), out var value);");
        sb.AppendLine("        return value!;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendPlainMethods(StringBuilder sb, string namePrefix, int count, int paramCount)
    {
        for (var i = 0; i < count; i++)
        {
            var parameters = string.Join(", ", Enumerable.Range(0, paramCount).Select(p => $"int p{p}"));
            sb.AppendLine($"    int {namePrefix}_{i}({parameters});");
        }
    }

    private const string RunMethod = """

        public static object Run()
        {
            var client = (IBigInterface)CreateDouble();

            // A plain member's Configure() path.
            client.Configure().Compute(2, 3).Returns(42);
            var computeResult = client.Compute(2, 3);

            // An overloaded member: the 2-parameter overload's discriminator-only Configure(),
            // and the 3-parameter overload's discriminator-only Configure() plus a narrower
            // .LookupMatching(...) override - proving both overloads' generated surfaces coexist
            // and dispatch correctly at this interface scale.
            client.Configure().Lookup(1, 1).Returns(-1);
            client.Configure().Lookup(0, 0, 0).Returns(0);
            client.Configure().LookupMatching(
                global::Compono.Match.Is<int>(a => a == 5),
                global::Compono.Match.Any<int>(),
                global::Compono.Match.Any<int>()).Returns(99);

            var lookupTwoParamResult = client.Lookup(1, 1);
            var lookupThreeParamMatchedResult = client.Lookup(5, 0, 0);
            var lookupThreeParamFallbackResult = client.Lookup(7, 0, 0);

            return new[]
            {
                computeResult,
                lookupTwoParamResult,
                lookupThreeParamMatchedResult,
                lookupThreeParamFallbackResult,
            };
        }
        """;

    [Fact]
    public void LargeRepresentativeInterface_GeneratesCleanlyAndProducesAUsableDouble()
    {
        var source = BuildLargeInterfaceSource().Replace(
            "public static object CreateDouble()",
            RunMethod + "\n\n    public static object CreateDouble()");

        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = source,
                MSBuildProperties = new Dictionary<string, string> { ["ComponoGeneratedTestDoubles"] = "true" },
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        // computeResult, lookupTwoParamResult, lookupThreeParamMatchedResult, lookupThreeParamFallbackResult
        result.Should().BeEquivalentTo(new[] { 42, -1, 99, 0 }, options => options.WithStrictOrdering());
    }
}
