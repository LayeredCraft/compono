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
/// This slice emits the exact feature envelope <see cref="IsWithinEmissionEnvelope"/> describes:
/// every member shape (<see cref="AotComposeDataReturnShape.Sync"/>,
/// <see cref="AotComposeDataReturnShape.TaskOf"/>, <see cref="AotComposeDataReturnShape.ValueTaskOf"/>,
/// and <see cref="AotComposeDataReturnShape.AsyncEnumerable"/> - the last with both the reference- and
/// value-element paths), every non-generic attribute arity (plain, <c>TProfile</c>, and
/// <c>TProfile, TConfig</c>), attribute-level seeds (including a negative seed, whose whole factory
/// body throws the same <c>NegativeAttributeSeed</c> failure the reflection path produces),
/// <c>[Shared]</c> parameters, active attribute-level <c>SkipUnless</c>/<c>SkipWhen</c> conditions
/// (rendered as statically bound <c>static () =&gt; global::Holder.Prop</c> predicates, matching
/// ADR-0070's compile-time AOT rendering), and a <c>[ComposeData]</c> method declared on a generic
/// test-class base (ADR-0070 Amendment 4 / PLAN-0071 B5.3): the registration is keyed by the
/// declaring type's open-generic index and the closed constructed base is resolved once per factory
/// execution via <c>ComposeDataRows.ResolveGenericTestClassType</c>.
/// </remarks>
internal static class AotComposeDataRegistrationEmitter
{
    /// <summary>
    /// The exact feature envelope this emitter can produce correctly: every supported member source
    /// shape - the synchronous <see cref="AotComposeDataReturnShape.Sync"/> collection, the awaited
    /// <see cref="AotComposeDataReturnShape.TaskOf"/> and
    /// <see cref="AotComposeDataReturnShape.ValueTaskOf"/> results, and the
    /// <see cref="AotComposeDataReturnShape.AsyncEnumerable"/> source (both the reference-element and
    /// value-element paths) - for every form of <c>[ComposeData]</c> across every approved attribute
    /// arity, including profiles, profile configuration values, attribute-level seeds, <c>[Shared]</c>
    /// parameters, active attribute-level <c>SkipUnless</c>/<c>SkipWhen</c> conditions, and methods
    /// declared on a generic test-class base. Every path that could emit a registration gates on
    /// this single predicate, so a model outside the envelope produces no registration at all rather
    /// than a registration missing the feature's semantics.
    /// </summary>
    /// <param name="method">The discovered <c>[ComposeData]</c> model.</param>
    /// <returns><see langword="true"/> when the model is within the emission envelope.</returns>
    public static bool IsWithinEmissionEnvelope(AotComposeDataMethodInfo method)
    {
        // Every supported source shape is emittable: the synchronous collection, the awaited
        // Task<T>/ValueTask<T> results, and async-enumerable sources (both the reference-element and
        // value-element paths). Active attribute-level SkipUnless/SkipWhen conditions are rendered
        // too (S5), and a [ComposeData] method declared on a generic test-class base is rendered
        // against the closed constructed base resolved at execution time (S6). Unsupported generic
        // shapes are already rejected during discovery (CMP0054/CMP0040/CMP0047), so by the time an
        // eligible method reaches here it is always emittable.
        if (method.ReturnShape is null || method.MemberKind is null)
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
            // The closed test class used by every CreateRow/descriptor/{T}/{M} call. For a method
            // declared on a generic test-class base (ADR-0070 Amendment 4 / PLAN-0071 B5.3) the
            // registration is keyed by the open-generic index and the closed constructed base is
            // resolved once, at execution time, from the executing test class - never the open
            // generic (which generated code cannot name as a type). For every non-generic test class
            // the closed type is simply the attribute's own declaring type.
            TestClassTypeExpression = method.IsGenericTestClass
                ? $"global::Compono.XunitV3.Aot.ComposeDataRows.ResolveGenericTestClassType(typeof({method.TestClassIndex}))"
                : $"typeof({method.FullyQualifiedTestClassName})",
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
            IsSync = method.ReturnShape == AotComposeDataReturnShape.Sync,
            IsTaskOf = method.ReturnShape == AotComposeDataReturnShape.TaskOf,
            IsValueTaskOf = method.ReturnShape == AotComposeDataReturnShape.ValueTaskOf,
            IsAsyncEnumerable = method.ReturnShape == AotComposeDataReturnShape.AsyncEnumerable,
            // The value-element async path is only reachable when the declared member type implements a
            // single IAsyncEnumerable<T> whose element is a value type; the mixed one-reference-plus-
            // value case reads through the reference path (AsyncRowElementIsValueType is false there).
            IsAsyncEnumerableValueElement = method.ReturnShape == AotComposeDataReturnShape.AsyncEnumerable
                && method.AsyncRowElementIsValueType,
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
                // A recorded active condition only (attribute Skip set, ADR-0070 / PLAN-0071 B4.9):
                // a statically bound predicate reading the property off its own declaring holder, so
                // the Native AOT runtime never needs reflection or a string name lookup.
                SkipUnlessValue = RenderSkipCondition(method.SkipUnless),
                SkipWhenValue = RenderSkipCondition(method.SkipWhen),
                // Emitted only for a theory-level condition (ADR-0070 Amendment 5). None renders as
                // null so a theory without a condition stays byte-identical to the pre-B6 output and
                // every existing snapshot is preserved.
                TheorySkipConditionValue = RenderTheorySkipCondition(method.AttributeMetadata.TheorySkipCondition),
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

    /// <summary>
    /// Renders a recorded active attribute-level <c>SkipUnless</c>/<c>SkipWhen</c> condition (ADR-0070 /
    /// PLAN-0071 B5.8) as the Native AOT <see cref="System.Func{TResult}"/> predicate the metadata
    /// carries, or <c>null</c> when no condition is active. The predicate is a <c>static</c> lambda (it
    /// captures nothing) that reads the <see langword="bool"/> property directly off the type that
    /// declares it - which may be a base class of the attribute's <c>SkipType</c> or of the test class -
    /// matching ADR-0070's compile-time rendering exactly and avoiding runtime reflection.
    /// </summary>
    private static string RenderSkipCondition(AotComposeDataSkipCondition? condition)
    {
        if (condition is null)
        {
            return "null";
        }

        var escaped = RequiredMemberCollector.EscapeIdentifier(condition.PropertyName);
        return $"static () => {condition.FullyQualifiedHolderTypeName}.{escaped}";
    }

    private static string? RenderTheorySkipCondition(AotComposeDataTheorySkipCondition condition) =>
        condition switch
        {
            AotComposeDataTheorySkipCondition.None => null,
            AotComposeDataTheorySkipCondition.Unless => "1",
            AotComposeDataTheorySkipCondition.When => "2",
            _ => null,
        };

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
