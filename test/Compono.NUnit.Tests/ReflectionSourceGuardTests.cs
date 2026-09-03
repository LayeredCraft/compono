using NUnit.Framework;

namespace Compono.NUnit.Tests;

// A simple text/syntax scan over src/Compono.NUnit/**/*.cs that fails the build if a
// reflection-based dynamic-generic-dispatch pattern this design specifically rejected (ADR-0059
// §17) ever reappears - so a future change can't silently reintroduce it. `ConstructorInfo.Invoke`
// on an already-known, non-generic Type (ConfigProfileBinder's own bounded, once-per-attribute-
// instance construction) is the one accepted exception - explicitly not scanned for here, matching
// ADR-0059's own "framework-required metadata access, not a new reflection category" distinction.
// Ported from test/Compono.MSTest.Tests/ReflectionSourceGuardTests.cs (ADR-0057 §14) - real,
// demonstrated repository precedent for this exact check, not ritual validation invented for this
// package.
[TestFixture]
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

    [Test]
    public void SourceFiles_ContainNoDynamicGenericDispatchPatterns()
    {
        var sourceDirectory = FindSourceDirectory();
        var sourceFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);

        Assert.That(sourceFiles.Length, Is.GreaterThan(0), $"Expected to find .cs files under '{sourceDirectory}'.");

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

        Assert.That(violations, Is.Empty,
            "Found dynamic-generic-dispatch pattern(s) ADR-0059 §17 rejects:\n" + string.Join('\n', violations));
    }

    private static string FindSourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Compono.NUnit")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Could not locate the repository root (a 'src/Compono.NUnit' directory) above " + AppContext.BaseDirectory);

        return Path.Combine(directory.FullName, "src", "Compono.NUnit");
    }
}
