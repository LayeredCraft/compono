using System.Collections.Concurrent;
using System.Reflection;
using Scriban;
using Scriban.Runtime;

namespace Compono.Generators.Emitters;

/// <summary>
/// Loads and renders the embedded Scriban templates, per
/// <c>docs/adr/0005-generator-implementation-conventions.md</c>. Parsed templates are cached for the
/// lifetime of the generator process - IDE scenarios re-run the generator often, and re-parsing the
/// same template text every time would be wasted work.
/// </summary>
internal static class TemplateHelper
{
    /// <summary>
    /// Scriban's own default (<see cref="Scriban.TemplateContext"/>'s constructor) is 1000 - a
    /// generic ceiling sized for arbitrary/untrusted user-facing templating, not this generator's
    /// workload. Compono's templates render a bounded, already-compiled Roslyn symbol model at
    /// build time (no untrusted input, no DoS surface - see
    /// <c>docs/research/0034-testdoubles-scriban-loop-limit-issue-142-investigation.md</c> §8), so
    /// the ceiling can and should be set to a value Compono has actually measured against, rather
    /// than one inherited by accident. 20,000 was chosen there from direct measurement: the real
    /// <c>Amazon.S3.IAmazonS3</c> (182 members) needs exactly 1,448, and a deliberately extreme
    /// 1,800-member, heavily-overloaded synthetic interface needs at most ~12,000 - 20,000 clears
    /// every measured case with wide margin while still catching a genuine runaway (e.g. a future
    /// template bug introducing an actual infinite loop).
    /// </summary>
    private const int ScribanLoopIterationLimit = 20_000;

    /// <summary>
    /// Scriban's own default (<see cref="Scriban.TemplateContext"/>'s constructor) is 1,048,576
    /// characters (1 MiB) - a second, independent Scriban safety ceiling from
    /// <see cref="ScribanLoopIterationLimit"/> that caps total rendered output length and, once
    /// hit, silently truncates the remaining output and appends "..." with no exception and no
    /// diagnostic (found while raising the loop-iteration limit above - see
    /// <c>docs/research/0034-testdoubles-scriban-loop-limit-issue-142-investigation.md</c> §17).
    /// Real <c>Amazon.S3.IAmazonS3</c> requires 2,206,258 characters to render completely, and the
    /// same deliberately extreme 1,800-member synthetic stress shape used to size
    /// <see cref="ScribanLoopIterationLimit"/> requires 17,345,696 characters - 20,000,000 gives
    /// that same demonstrated shape headroom under this limit too, so one Scriban safety ceiling
    /// doesn't accept a shape the other would still reject.
    /// </summary>
    private const int ScribanOutputCharacterLimit = 20_000_000;

    private static readonly ConcurrentDictionary<string, Template> TemplateCache = new();

    public static string Render<TModel>(string resourceName, TModel model)
    {
        var template = TemplateCache.GetOrAdd(resourceName, LoadTemplate);

        // Scriban's convenience Template.Render(model) overload has no way to supply a custom
        // TemplateContext - it always constructs one with the library defaults. This replicates
        // exactly what that overload does internally (ScriptObject + Import + PushGlobal, per
        // Scriban's own Template.cs), the only difference being the two explicit limits above, so
        // current rendering semantics are unchanged apart from those two values.
        var scriptObject = new ScriptObject();
        scriptObject.Import(model);
        var context = new Scriban.TemplateContext
        {
            LoopLimit = ScribanLoopIterationLimit,
            LimitToString = ScribanOutputCharacterLimit,
        };
        context.PushGlobal(scriptObject);

        return template.Render(context);
    }

    private static Template LoadTemplate(string resourceName)
    {
        var assembly = typeof(TemplateHelper).Assembly;
        var fullResourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.Ordinal));

        if (fullResourceName is null)
            throw new InvalidOperationException(
                $"No embedded template resource ending in '{resourceName}' was found. Available resources: " +
                string.Join(", ", assembly.GetManifestResourceNames()));

        using var stream = assembly.GetManifestResourceStream(fullResourceName)!;
        using var reader = new StreamReader(stream);
        var templateText = reader.ReadToEnd();

        var template = Template.Parse(templateText, resourceName);

        if (template.HasErrors)
            throw new InvalidOperationException(
                $"Template '{resourceName}' failed to parse: " +
                string.Join("; ", template.Messages.Select(m => $"{m.Span}: {m.Message}")));

        return template;
    }
}
