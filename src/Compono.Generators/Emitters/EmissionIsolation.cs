using Compono.Generators.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Emitters;

internal static class EmissionIsolation
{
    public static void TryEmit(
        SourceProductionContext context,
        string artifactKind,
        string itemIdentity,
        Action emit)
    {
        try
        {
            emit();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratedSourceEmissionFailed,
                Location.None,
                artifactKind,
                itemIdentity,
                ex.GetType().Name,
                ex.Message));
        }
    }
}
