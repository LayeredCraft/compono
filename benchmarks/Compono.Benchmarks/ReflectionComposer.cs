using System.Collections;

namespace Compono.Benchmarks;

/// <summary>
/// A minimal reflection-based construction baseline - what <see cref="Composer.Create{T}"/>
/// replaces with source generation.
/// </summary>
public static class ReflectionComposer
{
    // Matches Compono's own defaults exactly, so this baseline does comparable real work rather
    // than a cheaper strawman: PrimitiveValueProvider.StringLength (src/Compono/Providers) and
    // ADR-0013's default collection size.
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int StringLength = 8;
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
    /// recursively through reflection, filling every leaf field with a genuinely random value
    /// (an 8-character alphanumeric string, a 3-element collection - Compono's own defaults) rather
    /// than a fixed placeholder. This is the actual reflection-based alternative someone would write
    /// by hand for <see cref="ResolutionBenchmarks"/>' representative graph, not a dispatch-cost-only
    /// strawman: an earlier version of this method used fixed placeholder values, which made it
    /// faster than <see cref="Composer.Create{T}"/> for doing categorically less work, not because
    /// reflective dispatch beats source-generated dispatch (PR #13 review). Deliberately narrow:
    /// only the shapes <see cref="Customer"/>/<see cref="Address"/> actually use (<c>string</c>,
    /// <c>List&lt;T&gt;</c>, and a type with a single public constructor) - this is a benchmark
    /// baseline, not a general reflection-based composer, and its randomness is ordinary
    /// <see cref="Random.Shared"/>, not Compono's deterministic, seed-forked
    /// <see cref="IRandomSource"/> - reproducibility isn't a property this baseline needs.
    /// </summary>
    /// <typeparam name="T">The type to construct.</typeparam>
    public static T ComposeRecursive<T>() => (T)ComposeValue(typeof(T))!;

    private static object? ComposeValue(Type type)
    {
        if (type == typeof(string))
            return NextString();

        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            return values.GetValue(Random.Shared.Next(values.Length));
        }

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

    private static string NextString()
    {
        Span<char> chars = stackalloc char[StringLength];
        for (var i = 0; i < StringLength; i++)
            chars[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];

        return new string(chars);
    }
}
