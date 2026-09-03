namespace Compono.Generators.Emitters;

/// <summary>
/// The generator assembly's own version, read once from its metadata so every emitter's
/// <c>GeneratedCodeAttribute</c> output stays accurate as the generator's version changes, without
/// each emitter separately reflecting over its own <c>typeof(...).Assembly</c> - every emitter type
/// lives in this same assembly, so there was never more than one distinct value to compute
/// (PLAN-0061 Phase 1). Falls back to the assembly version if no informational version is set (e.g.
/// no real release versioning wired up yet), then to a fixed placeholder.
/// </summary>
internal static class GeneratorVersion
{
    public static readonly string Current =
        typeof(GeneratorVersion).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Select(a => a.InformationalVersion)
            .FirstOrDefault()
        ?? typeof(GeneratorVersion).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";
}
