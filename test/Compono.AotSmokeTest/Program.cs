using Compono;

namespace Compono.AotSmokeTest;

// A real, custom composed type - needs both a generated PlanCache<Widget> entry (Phase 1's ordinary
// plan-generation discovery) and a generated RowInvokerRegistry registration (ADR-0041).
internal sealed class Widget
{
    public Widget(string name) => Name = name;

    public string Name { get; }
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

            Console.WriteLine($"PASS: RowInvokerRegistry dispatch survived Native AOT - Widget.Name='{widget.Name}', leaf='{leaf}'.");
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
