using System.Globalization;
using System.Linq;
using System.Text;
using Compono.Generators.Discovery;
using Compono.Generators.Models;
using Compono.Generators.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Compono.Generators.Emitters;

/// <summary>
/// Renders an <see cref="AotComposeDataMethodInfo"/> into a generated
/// <c>RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)</c> module-initializer registration -
/// the <c>Compono.XunitV3.Aot</c> <c>[ComposeData]</c> Native AOT counterpart to
/// <c>Compono.XunitV3.ComposeDataAttribute</c>'s reflection-mode theory-row enumeration
/// (ADR-0070 / PLAN-0071 B5).
/// </summary>
/// <remarks>
/// This slice (S3) emits the exact feature envelope <see cref="IsWithinEmissionEnvelope"/> describes:
/// the synchronous member shape, every non-generic attribute arity (plain, <c>TProfile</c>, and
/// <c>TProfile, TConfig</c>), attribute-level seeds (including a negative seed, whose whole factory
/// body throws the same <c>NegativeAttributeSeed</c> failure the reflection path produces), and
/// <c>[Shared]</c> parameters. Every other shape - active skip predicates, generic test-class bases,
/// and the asynchronous <c>TaskOf</c>/<c>ValueTaskOf</c>/<c>AsyncEnumerable</c> shapes - is discovered
/// (and its own diagnostics reported) but emitted by a later slice against this same template, so
/// emitting it here would silently discard its semantics.
/// </remarks>
internal static class AotComposeDataRegistrationEmitter
{
    /// <summary>
    /// The exact feature envelope this slice (S3) can emit correctly: the synchronous, non-generic,
    /// no-active-skip form of <c>[ComposeData]</c> for every approved attribute arity, including
    /// profiles, profile configuration values, attribute-level seeds, and <c>[Shared]</c> parameters.
    /// Every path that could emit a registration gates on this single predicate, so a model outside
    /// the envelope produces no registration at all rather than a registration missing the feature's
    /// semantics.
    /// </summary>
    /// <param name="method">The discovered <c>[ComposeData]</c> model.</param>
    /// <returns><see langword="true"/> when the model is within the emission envelope.</returns>
    public static bool IsWithinEmissionEnvelope(AotComposeDataMethodInfo method)
    {
        if (method.ReturnShape != AotComposeDataReturnShape.Sync
            || method.MemberKind is null
            || method.IsGenericTestClass
            || method.SkipUnless is not null
            || method.SkipWhen is not null)
        {
            return false;
        }

        if (method.AttributeArity is < 0 or > 2)
        {
            return false;
        }

        if (method.AttributeArity >= 1 && method.FullyQualifiedProfileTypeName is null)
        {
            return false;
        }

        if (method.AttributeArity == 2 && method.FullyQualifiedConfigTypeName is null)
        {
            return false;
        }

        return true;
    }

    public static void Generate(SourceProductionContext context, AotComposeDataMethodInfo method)
    {
        // Only the feature envelope IsWithinEmissionEnvelope governs. Every other shape is discovered
        // but its emission belongs to a later slice - emit nothing here so no registration (and no
        // broken generated source) is produced for it yet.
        if (!IsWithinEmissionEnvelope(method))
        {
            return;
        }

        var memberName = method.MemberName ?? string.Empty;

        var model = new
        {
            TestClassIndexLiteral = SymbolDisplay.FormatLiteral(method.TestClassIndex, quote: true),
            MethodNameLiteral = SymbolDisplay.FormatLiteral(method.MethodName, quote: true),
            // IsWithinEmissionEnvelope excludes generic test classes, so the closed test class at execution
            // time is always the attribute's own declaring type.
            TestClassTypeExpression = $"typeof({method.FullyQualifiedTestClassName})",
            // {M} is the member display name the failure messages use: the lookup type's full name
            // (the attribute's MemberType when supplied, else the test class) plus the member name.
            MemberDisplayNameBase = method.LookupIsTestClass
                ? "testClass.FullName"
                : $"typeof({method.FullyQualifiedLookupTypeName}).FullName",
            MemberSuffixLiteral = SymbolDisplay.FormatLiteral("." + memberName, quote: true),
            MethodSuffixLiteral = SymbolDisplay.FormatLiteral("." + method.MethodName, quote: true),
            // The read binds to the member's own declaring type, not the lookup type, so an inherited
            // member is read through the base that actually declares it.
            MemberReadExpression = BuildMemberReadExpression(method, memberName),
            AttributeSeedExpression = RenderNullableInt(method.AttributeMetadata.Seed),
            HasNegativeAttributeSeed = method.AttributeMetadata.Seed is < 0,
            HasConfig = method.AttributeArity == 2,
            ConfigTypeName = method.FullyQualifiedConfigTypeName ?? "object",
            ComposerCreateExpression = BuildComposerCreateExpression(method),
            ParameterCount = method.Parameters.Count,
            Parameters = method.Parameters.Select(p => new
            {
                p.FullyQualifiedTypeName,
                p.IsNullable,
                p.IsShared,
                p.Ordinal,
                NameLiteral = SymbolDisplay.FormatLiteral(p.Name, quote: true),
                LocalName = $"value{p.Ordinal}",
            }).ToArray(),
            AttributeMetadata = new
            {
                ExplicitValue = RenderNullableBool(method.AttributeMetadata.Explicit),
                LabelValue = RenderNullableString(method.AttributeMetadata.Label),
                SkipValue = RenderNullableString(method.AttributeMetadata.Skip),
                TestDisplayNameValue = RenderNullableString(method.AttributeMetadata.TestDisplayName),
                TimeoutValue = RenderNullableInt(method.AttributeMetadata.Timeout),
                TraitsValue = RenderTraits(method.AttributeMetadata.Traits),
                DisableParallelizationValue = RenderBool(method.AttributeMetadata.DisableParallelization ?? false),
                // Active skip predicates are a later slice; both are always null here.
                SkipUnlessValue = "null",
                SkipWhenValue = "null",
            },
            GeneratorVersion = GeneratorVersion.Current,
        };

        var source = TemplateHelper.Render("AotComposeDataRegistration.scriban", model);
        var hintName = GeneratedFileNaming.HintNameFor($"{method.FullyQualifiedTestClassName}.{method.MethodName}");

        context.AddSource($"{hintName}.AotComposeDataRegistration.g.cs", source);
    }

    private static string BuildMemberReadExpression(AotComposeDataMethodInfo method, string memberName)
    {
        var containing = method.FullyQualifiedMemberContainingTypeName ?? method.FullyQualifiedTestClassName;
        var escaped = RequiredMemberCollector.EscapeIdentifier(memberName);

        return method.MemberKind == AotComposeDataMemberKind.Method
            ? $"{containing}.{escaped}()"
            : $"{containing}.{escaped}";
    }

    /// <summary>
    /// Builds the per-row <c>Composer</c> construction the generated factory runs: the profile (for
    /// the generic arities) is always applied before the attribute-level seed, matching
    /// <c>Compono.XunitV3.ComposeDataAttribute</c>'s reflection path exactly - including the
    /// duplicate-seed configuration error when a profile also calls <c>WithSeed</c>.
    /// </summary>
    private static string BuildComposerCreateExpression(AotComposeDataMethodInfo method)
    {
        var seed = method.AttributeMetadata.Seed;
        var seedLiteral = seed?.ToString(CultureInfo.InvariantCulture);
        var profile = method.FullyQualifiedProfileTypeName;

        if (method.AttributeArity == 0)
        {
            return seed is { }
                ? $"var composer = global::Compono.Composer.Create(static builder => builder.WithSeed({seedLiteral}));"
                : "var composer = global::Compono.Composer.Create();";
        }

        if (seed is null)
        {
            return method.AttributeArity == 1
                ? $"var composer = global::Compono.Composer.Create(static builder => builder.AddProfile<{profile}>());"
                : $"var composer = global::Compono.Composer.Create(builder => builder.AddProfile(new {profile}(config)));";
        }

        return method.AttributeArity == 1
            ? $"var composer = global::Compono.Composer.Create(static builder => {{ builder.AddProfile<{profile}>(); builder.WithSeed({seedLiteral}); }});"
            : $"var composer = global::Compono.Composer.Create(builder => {{ builder.AddProfile(new {profile}(config)); builder.WithSeed({seedLiteral}); }});";
    }

    private static string RenderNullableBool(bool? value) =>
        value switch
        {
            true => "true",
            false => "false",
            null => "null",
        };

    private static string RenderBool(bool value) => value ? "true" : "false";

    private static string RenderNullableInt(int? value) =>
        value is { } number ? number.ToString(CultureInfo.InvariantCulture) : "null";

    private static string RenderNullableString(string? value) =>
        value is null ? "null" : SymbolDisplay.FormatLiteral(value, quote: true);

    private static string RenderTraits(EquatableArray<string> traits)
    {
        if (traits.Count == 0)
        {
            return "null";
        }

        var builder = new StringBuilder("new string[] { ");
        for (var i = 0; i < traits.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(SymbolDisplay.FormatLiteral(traits[i], quote: true));
        }

        builder.Append(" }");
        return builder.ToString();
    }
}
