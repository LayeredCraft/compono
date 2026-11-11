using System.Reflection;
using NUnit.Framework;

namespace Compono.NUnit.CompatibilityMatrix;

public sealed class Widget
{
    public Widget(string name) => Name = name;

    public string Name { get; }
}

// Deliberately no [TestFixture] - this project's own minimal proof that [Compose] alone (ADR-0059
// §7) discovers and dispatches correctly against every matrix leg's actual resolved NUnit version.
public class MatrixTests
{
    [Compose]
    public void ComposeDispatchesCorrectly(Widget widget, string leaf)
    {
        Assert.That(widget, Is.Not.Null);
        Assert.That(widget.Name, Is.Not.Empty);
        Assert.That(leaf, Is.Not.Empty);
    }
}

// Inherited [Compose] method - the attribute is declared by the base type, so ComposeAttribute's
// discovery-time module initialization must target the *base* type's module (ADR-0059 Amendment 1).
// Runs cold under the classic VSTest host on the classic leg, so a regression that initialized the
// wrong module fails here.
public class InheritedMatrixTestsBase
{
    [Compose]
    public void InheritedComposeDispatchesCorrectly(Widget widget, string leaf)
    {
        Assert.That(widget, Is.Not.Null);
        Assert.That(leaf, Is.Not.Empty);
    }
}

public sealed class InheritedMatrixTests : InheritedMatrixTestsBase;

// Nested [Compose]-bearing test type - the declaring type is nested, but its module is still this
// test assembly's, which is where the generator emitted the registration.
public sealed class NestedMatrixContainer
{
    public sealed class NestedMatrixTests
    {
        [Compose]
        public void NestedComposeDispatchesCorrectly(Widget widget, string leaf)
        {
            Assert.That(widget, Is.Not.Null);
            Assert.That(leaf, Is.Not.Empty);
        }
    }
}

/// <summary>
/// Positively verifies which test host actually ran this assembly. The matrix script builds two
/// *different* artifacts (a non-MTP Library for the classic leg, an MTP Exe for the MTP leg) and
/// sets <c>COMPONO_NUNIT_MATRIX_EXPECTED_HOST</c> to <c>vstest</c> or <c>mtp</c> for each run; this
/// test fails if the leg's intended host and the host that actually executed disagree.
/// </summary>
/// <remarks>
/// Detection uses a stable runtime signal, not parsed console output: under the Microsoft Testing
/// Platform the test assembly *is* the process entry assembly, whereas the classic VSTest test host
/// runs the test assembly inside the platform's own <c>testhost</c> entry point. If a future
/// <c>vstest.console</c> ever silently auto-routed the classic leg back to MTP (the historical false
/// green), the entry assembly would become this test assembly and this assertion would fail instead
/// of passing unnoticed.
/// </remarks>
public sealed class TestHostVerificationTests
{
    private const string ExpectedHostVariable = "COMPONO_NUNIT_MATRIX_EXPECTED_HOST";

    [Test]
    public void RunsUnderTheHostTheMatrixLegIntends()
    {
        var expected = Environment.GetEnvironmentVariable(ExpectedHostVariable);
        Assert.That(expected, Is.Not.Null.And.Not.Empty,
            $"{ExpectedHostVariable} must be set by .github/scripts/nunit-compatibility-matrix.sh so this " +
            "test can confirm the leg ran under the host it intends.");

        var testAssemblyName = typeof(MatrixTests).Assembly.GetName().Name;
        var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        var actual = string.Equals(entryAssemblyName, testAssemblyName, StringComparison.Ordinal) ? "mtp" : "vstest";

        Assert.That(actual, Is.EqualTo(expected),
            $"Expected the '{expected}' test host but this assembly actually ran under '{actual}' " +
            $"(entry assembly '{entryAssemblyName ?? "<null>"}'; test assembly '{testAssemblyName}'). " +
            "The classic leg must genuinely exercise the classic DotnetTestHostManager - not be silently " +
            "auto-routed to MtpTestRuntimeProvider.");
    }
}
