using Compono;

namespace Compono.AotSmokeTest;

// A real, custom composed type - needs both a generated PlanCache<Widget> entry (Phase 1's ordinary
// plan-generation discovery) and a generated RowInvokerRegistry registration (ADR-0041).
internal sealed class Widget
{
    public Widget(string name) => Name = name;

    public string Name { get; }
}

// ADR-0002 Amendment 3 / ADR-0052 (Part B): a type with more than one accessible constructor,
// composed through an explicit For<T>().UseConstructor<...>() selection - exercised under Native
// AOT specifically, not just the ordinary JIT this package's own dotnet test already runs under.
internal interface IBar { }

internal interface IBaz { }

internal sealed class BarImpl : IBar { }

internal sealed class BazImpl : IBaz { }

internal sealed class AmbiguousFoo
{
    public AmbiguousFoo() { }

    public AmbiguousFoo(IBar bar, IBaz baz)
    {
        Bar = bar;
        Baz = baz;
    }

    public IBar? Bar { get; }

    public IBaz? Baz { get; }
}

// docs/adr/0056-composition-builder-share-graph-wide-sharing.md: a builder-configured Share<T>()
// type reached by two sibling composed dependents, neither annotated, must resolve to the exact same
// instance - exercised here as a plain Composer.Create<T>() graph (not row/[Compose]-based) under
// Native AOT specifically, since Share<T>()'s own write-gate touches CompositionContext.ResolveCore
// directly, independent of the RowInvokerRegistry/[Compose] dispatch path already covered above.
internal sealed class SharedLeaf
{
    public string Origin { get; } = "generated";
}

internal sealed class ShareSiblingA(SharedLeaf leaf)
{
    public SharedLeaf Leaf { get; } = leaf;
}

internal sealed class ShareSiblingB(SharedLeaf leaf)
{
    public SharedLeaf Leaf { get; } = leaf;
}

internal sealed class ShareSiblingsRoot(ShareSiblingA a, ShareSiblingB b)
{
    public ShareSiblingA A { get; } = a;
    public ShareSiblingB B { get; } = b;
}

internal static class SmokeTestMethods
{
    // The real target of this whole harness: a [Compose]-attributed method parameter list containing
    // both a custom composed type (Widget, needs a real PlanCache<Widget> entry) and a
    // provider-resolved leaf type (string, needs no PlanCache entry at all per ADR-0041 Amendment 2 -
    // the exact gap the amendment closed) - both need a real RowInvokerRegistry registration to
    // dispatch through CompositionRow.Resolve<T>() under Native AOT with no MakeGenericMethod
    // anywhere.
    [Compono.XunitV3.Compose]
    public static void Handle(Widget widget, string leaf)
    {
    }
}

internal static class Program
{
    private static int Main()
    {
        try
        {
            var row = Composer.Create().CreateRow(typeof(Program));

            var widgetValue = Dispatch<Widget>(row, "widget", 0);
            var leafValue = Dispatch<string>(row, "leaf", 1);

            if (widgetValue is not Widget { Name.Length: > 0 } widget)
                throw new InvalidOperationException($"Widget dispatch produced an unexpected value: {widgetValue}");

            if (leafValue is not string { Length: > 0 } leaf)
                throw new InvalidOperationException($"string dispatch produced an unexpected value: {leafValue}");

            // ADR-0002 Amendment 3 / ADR-0052 (Part B): explicit constructor selection for an
            // ambiguous type, through the real generated composition plan, under Native AOT.
            var composer = Composer.Create(builder =>
            {
                builder.For<AmbiguousFoo>().UseConstructor<IBar, IBaz>();
                builder.Register<IBar>(() => new BarImpl());
                builder.Register<IBaz>(() => new BazImpl());
            });

            var foo = composer.Create<AmbiguousFoo>();

            if (foo.Bar is not BarImpl || foo.Baz is not BazImpl)
                throw new InvalidOperationException("UseConstructor<IBar, IBaz>() did not compose AmbiguousFoo through the selected constructor.");

            var shareComposer = Composer.Create(builder => builder.Share<SharedLeaf>());
            var shareRoot = shareComposer.Create<ShareSiblingsRoot>();

            if (!ReferenceEquals(shareRoot.A.Leaf, shareRoot.B.Leaf))
                throw new InvalidOperationException("Share<T>() did not establish an identical shared instance for two sibling composed dependents under Native AOT.");

            Console.WriteLine(
                $"PASS: RowInvokerRegistry dispatch survived Native AOT - Widget.Name='{widget.Name}', " +
                $"leaf='{leaf}', explicit constructor selection composed AmbiguousFoo correctly, " +
                "Share<T>() established identical shared instances for two sibling composed dependents.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {ex}");
            return 1;
        }
    }

    // Looks up and invokes the real generator-emitted RowInvokerRegistry.Register(...) entry for
    // <typeparamref name="T"/> by its runtime Type - exactly the same non-generic, Type-keyed lookup
    // Compono.XunitV3.Binding.RowInvokers.Build performs, just inlined here since this harness has no
    // dependency on the Compono.XunitV3 package at all.
    private static object? Dispatch<T>(CompositionRow row, string name, int ordinal)
    {
        if (!RowInvokerRegistry.TryGet(typeof(T), out var resolve, out _, out _))
            throw new InvalidOperationException($"No RowInvokerRegistry entry for '{typeof(T)}' - the generator didn't emit one, or its module initializer never ran.");

        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.TestParameter, ordinal, name, typeof(Program), Nullability.NotNullable);

        return resolve(row, descriptor);
    }
}
