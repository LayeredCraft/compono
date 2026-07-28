namespace Compono.Benchmarks;

/// <summary>
/// A minimal reflection-based construction baseline - what <see cref="Composer.Create{T}"/>
/// replaces with source generation.
/// </summary>
public static class ReflectionComposer
{
    /// <summary>
    /// Constructs an instance of <typeparamref name="T"/> via its parameterless constructor,
    /// found and invoked through reflection rather than generated code.
    /// </summary>
    /// <typeparam name="T">The type to construct.</typeparam>
    public static T Compose<T>()
    {
        var constructor = typeof(T).GetConstructors().Single();

        return (T)constructor.Invoke([]);
    }
}
