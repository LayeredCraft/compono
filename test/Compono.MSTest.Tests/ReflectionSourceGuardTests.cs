using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Compono.MSTest.Tests;

// A simple text/syntax scan over src/Compono.MSTest/**/*.cs that fails the build if a
// reflection-based dynamic-generic-dispatch pattern this design specifically rejected (ADR-0057
// §14) ever reappears - so a future change can't silently reintroduce it. `ConstructorInfo.Invoke`
// on an already-known, non-generic Type (ConfigProfileBinder's own bounded, once-per-attribute-
// instance construction) is the one accepted exception - explicitly not scanned for here, matching
// ADR-0057's own "framework-required metadata access, not a new reflection category" distinction.
[TestClass]
public sealed class ReflectionSourceGuardTests
{
    private static readonly string[] ForbiddenPatterns =
    [
        "MakeGenericType",
        "MakeGenericMethod",
        "Activator.CreateInstance",
        "DynamicMethod",
        "Delegate.CreateDelegate",
        "System.Linq.Expressions",
    ];

    [TestMethod]
    public void SourceFiles_ContainNoDynamicGenericDispatchPatterns()
    {
        var sourceDirectory = FindSourceDirectory();
        var sourceFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);

        Assert.IsTrue(sourceFiles.Length > 0, $"Expected to find .cs files under '{sourceDirectory}'.");

        var violations = new List<string>();

        foreach (var file in sourceFiles)
        {
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                // A doc-comment (///) mentioning a forbidden pattern by name to explain why it's
                // *not* used is not a violation - only real code is scanned.
                var trimmed = lines[i].TrimStart();

                if (trimmed.StartsWith("///", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
                    continue;

                foreach (var pattern in ForbiddenPatterns)
                {
                    if (lines[i].Contains(pattern, StringComparison.Ordinal))
                        violations.Add($"{Path.GetFileName(file)}:{i + 1}: contains '{pattern}'");
                }
            }
        }

        Assert.IsTrue(violations.Count == 0,
            "Found dynamic-generic-dispatch pattern(s) ADR-0057 §14 rejects:\n" + string.Join('\n', violations));
    }

    private static string FindSourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Compono.MSTest")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Could not locate the repository root (a 'src/Compono.MSTest' directory) above " + AppContext.BaseDirectory);

        return Path.Combine(directory.FullName, "src", "Compono.MSTest");
    }
}
