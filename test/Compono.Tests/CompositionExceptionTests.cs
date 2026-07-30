using System.Reflection;

namespace Compono.Tests;

public sealed class CompositionExceptionTests
{
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenDiagnosticIsNull()
    {
        // Regression for a real gap (PR #13 review): the base(diagnostic.Message) initializer
        // dereferences diagnostic before this constructor's own body ever runs, so an unguarded
        // null argument surfaced as NullReferenceException instead of the expected
        // ArgumentNullException every other guarded constructor in this codebase throws.
        var act = () => new CompositionException(diagnostic: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DoesNotDeclareAnyMemberOfTypeCompositionContext()
    {
        // Regression for a real gap (PR #17 review): an earlier fix identified which
        // CompositionContext diagnosed an exception by storing the context instance itself,
        // which meant retaining a thrown CompositionException (e.g. a test runner keeping
        // failures for reporting) kept that context's whole object graph alive - its configured
        // IServiceProvider, registration factory closures, scope-held shared values, and trace
        // buffer. Fixed by comparing an opaque per-context identity token instead. Asserted here
        // structurally (not via GC/WeakReference, which is unreliable to assert deterministically
        // in a Debug-configuration test run) - no field on this type, including compiler-generated
        // auto-property backing fields, may be typed as or derived from CompositionContext.
        var fieldTypes = typeof(CompositionException)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(field => field.FieldType);

        fieldTypes.Should().NotContain(fieldType => typeof(CompositionContext).IsAssignableFrom(fieldType));
    }
}
