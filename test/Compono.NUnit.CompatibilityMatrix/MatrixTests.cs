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
