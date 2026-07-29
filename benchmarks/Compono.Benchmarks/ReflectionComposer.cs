using System.Collections;

namespace Compono.Benchmarks;

/// <summary>
/// A minimal reflection-based construction baseline - what <see cref="Composer.Create{T}"/>
/// replaces with source generation.
/// </summary>
public static class ReflectionComposer
{
    private const int CollectionSize = 3;

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

    /// <summary>
    /// Constructs an instance of <typeparamref name="T"/> by walking its constructor's parameters
    /// recursively through reflection - the reflection-based alternative to a generated
    /// <see cref="ICompositionPlan{T}"/> graph (<see cref="ResolutionBenchmarks"/>), not just a
    /// single flat type (<see cref="Compose{T}"/>). Deliberately narrow: only the shapes
    /// <see cref="Customer"/>/<see cref="Address"/> actually use (<c>string</c>,
    /// <c>List&lt;T&gt;</c>, and a type with a single public constructor) - this is a benchmark
    /// baseline, not a general reflection-based composer.
    /// </summary>
    /// <typeparam name="T">The type to construct.</typeparam>
    public static T ComposeRecursive<T>() => (T)ComposeValue(typeof(T))!;

    private static object? ComposeValue(Type type)
    {
        if (type == typeof(string))
            return "value";

        if (type.IsEnum)
            return Enum.GetValues(type).GetValue(0);

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = type.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(type)!;
            for (var i = 0; i < CollectionSize; i++)
                list.Add(ComposeValue(elementType));

            return list;
        }

        var constructor = type.GetConstructors().Single();
        var arguments = constructor.GetParameters()
            .Select(parameter => ComposeValue(parameter.ParameterType))
            .ToArray();

        return constructor.Invoke(arguments);
    }
}
