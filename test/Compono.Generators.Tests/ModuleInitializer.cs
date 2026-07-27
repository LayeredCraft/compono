using System.Runtime.CompilerServices;
using VerifyTests;

namespace Compono.Generators.Tests;

internal static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize() => VerifySourceGenerators.Initialize();
}
