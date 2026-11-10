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
    public void WithSeedInMessage_RewritesMessage_ButPreservesDiagnosticAndSetsInnerException()
    {
        // Internal seam for Compono.XunitV3 (PR #26 review; ADR-0022 Amendment 5) - a pipeline-thrown
        // CompositionException's own Message never carried the seed on its own (only Diagnostic did,
        // via its own ToString()), so Compono.XunitV3's GetData rewrites it before propagating.
        var diagnostic = new CompositionDiagnostic
        {
            RootType = typeof(CompositionExceptionTests),
            FailedType = typeof(CompositionExceptionTests),
            Path = "unrelated-path",
            Trace = [],
            Seed = 0,
            Message = "original pipeline failure message",
        };
        var original = CompositionException.CreatePipelineDiagnosed(diagnostic, diagnosingContextIdentity: new object());

        var wrapped = CompositionException.WithSeedInMessage(original, seed: 492173);

        wrapped.Message.Should().Be("original pipeline failure message\n\nSeed: 492173");
        wrapped.Diagnostic.Should().BeSameAs(diagnostic);
        wrapped.Diagnostic!.Message.Should().Be("original pipeline failure message", "Diagnostic's own Message is left untouched - only the outer exception's Message is rewritten");
        wrapped.InnerException.Should().BeSameAs(original);
    }

    [Fact]
    public void WithSeedInMessage_RewritesMessage_AndLeavesDiagnosticNull_ForAPlainMessageOriginal()
    {
        // PR #26 review, third round - a generated HashSet<T>/Dictionary collection plan's
        // unique-value-exhaustion path (CollectionPlan.scriban) throws exactly this shape: a plain
        // CompositionException(string), no Diagnostic at all. That failure deserves the same
        // pasteable seed as a diagnosed one, so WithSeedInMessage must handle it too, not throw.
        var original = new CompositionException("a plain, non-pipeline-diagnosed message");

        var wrapped = CompositionException.WithSeedInMessage(original, seed: 1);

        wrapped.Message.Should().Be("a plain, non-pipeline-diagnosed message\n\nSeed: 1");
        wrapped.Diagnostic.Should().BeNull();
        wrapped.InnerException.Should().BeSameAs(original);
    }

    [Fact]
    public void Constructor_PreservesMessageDiagnosticReferenceAndInnerException()
    {
        // ADR-0070 Amendment 1: the [ComposeData] row-failure wrapper prepends the failing row's
        // identity to the cause's message, so it needs a message it controls exactly while still
        // keeping the cause as InnerException and carrying the original CompositionDiagnostic by
        // reference. None of the pre-existing public constructors could do all three at once.
        var diagnostic = new CompositionDiagnostic
        {
            RootType = typeof(CompositionExceptionTests),
            FailedType = typeof(CompositionExceptionTests),
            Path = "unrelated-path",
            Trace = [],
            Seed = 0,
            Message = "original pipeline failure message",
        };
        var cause = new Exception("the cause");

        var exception = new CompositionException("ComposeData row 0 failed: original pipeline failure message", diagnostic, cause);

        exception.Message.Should().Be("ComposeData row 0 failed: original pipeline failure message");
        exception.Diagnostic.Should().BeSameAs(diagnostic);
        exception.Diagnostic!.Message.Should().Be("original pipeline failure message", "the supplied message is used verbatim and the diagnostic is not rewritten");
        exception.InnerException.Should().BeSameAs(cause);
    }

    [Fact]
    public void Constructor_AllowsNullDiagnostic()
    {
        // A non-pipeline cause (a profile constructor or Configure throwing a plain exception) has
        // no CompositionDiagnostic; Diagnostic stays null and the cause is still preserved.
        var cause = new InvalidOperationException("profile configuration blew up");

        var exception = new CompositionException("ComposeData row 1 failed: profile configuration blew up", diagnostic: null, cause);

        exception.Message.Should().Be("ComposeData row 1 failed: profile configuration blew up");
        exception.Diagnostic.Should().BeNull();
        exception.InnerException.Should().BeSameAs(cause);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenInnerExceptionIsNull()
    {
        var act = () => new CompositionException("message", diagnostic: null, innerException: null!);

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
