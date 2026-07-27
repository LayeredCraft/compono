using System.Collections.Concurrent;
using System.Reflection;
using Scriban;

namespace Compono.Generators.Emitters;

/// <summary>
/// Loads and renders the embedded Scriban templates, per
/// <c>docs/adr/0005-generator-implementation-conventions.md</c>. Parsed templates are cached for the
/// lifetime of the generator process - IDE scenarios re-run the generator often, and re-parsing the
/// same template text every time would be wasted work.
/// </summary>
internal static class TemplateHelper
{
    private static readonly ConcurrentDictionary<string, Template> TemplateCache = new();

    public static string Render<TModel>(string resourceName, TModel model)
    {
        var template = TemplateCache.GetOrAdd(resourceName, LoadTemplate);

        return template.Render(model);
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
