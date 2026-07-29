using System.Buffers.Binary;
using Compono;

namespace Compono.Providers;

/// <summary>
/// Stage 7 built-in provider for <c>docs/mvp.md</c>'s primitive/simple built-in type list -
/// <see langword="string"/>, <see langword="bool"/>, the integral and floating-point types,
/// <see langword="decimal"/>, <see cref="Guid"/>, <see cref="DateTime"/>,
/// <see cref="DateTimeOffset"/>, <see cref="DateOnly"/>, <see cref="TimeOnly"/>, and
/// <see cref="TimeSpan"/>.
/// </summary>
internal sealed class PrimitiveValueProvider : ICompositionProvider
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int StringLength = 8;

    private static readonly IReadOnlyDictionary<Type, Func<IRandomSource, object>> Factories =
        new Dictionary<Type, Func<IRandomSource, object>>
        {
            [typeof(string)] = NextString,
            [typeof(bool)] = random => (random.NextUInt64() & 1) == 0,
            [typeof(sbyte)] = random => unchecked((sbyte)random.NextUInt64()),
            [typeof(byte)] = random => unchecked((byte)random.NextUInt64()),
            [typeof(short)] = random => unchecked((short)random.NextUInt64()),
            [typeof(ushort)] = random => unchecked((ushort)random.NextUInt64()),
            [typeof(int)] = random => unchecked((int)random.NextUInt64()),
            [typeof(uint)] = random => unchecked((uint)random.NextUInt64()),
            [typeof(long)] = random => unchecked((long)random.NextUInt64()),
            [typeof(ulong)] = random => random.NextUInt64(),
            [typeof(float)] = NextSingle,
            [typeof(double)] = NextDouble,
            [typeof(decimal)] = NextDecimal,
            [typeof(Guid)] = NextGuid,
            [typeof(DateTime)] = NextDateTime,
            [typeof(DateTimeOffset)] = NextDateTimeOffset,
            [typeof(DateOnly)] = NextDateOnly,
            [typeof(TimeOnly)] = NextTimeOnly,
            [typeof(TimeSpan)] = NextTimeSpan,
        };

    public CompositionResult TryCompose(CompositionRequest request, ICompositionContext context)
    {
        // Every provider in the pipeline is invoked with the real CompositionContext - the only
        // ICompositionContext implementation that exists - so this node's own forked random source is
        // reachable via its internal test-observability property.
        var random = ((CompositionContext)context).Random;

        return TryComposeValue(request.RequestedType, random, out var value)
            ? new CompositionResult.Success(value)
            : CompositionResult.NotHandled.Instance;
    }

    // Shared with NullableValueProvider, which needs the same dispatch for a Nullable<T>'s underlying
    // type but isn't itself claiming request.RequestedType directly (Nullable<T> boxes its non-null
    // value as a plain boxed T, so a boxed primitive value here is already the correct boxed
    // representation for the caller to hand back as a Nullable<T> Success).
    internal static bool TryComposeValue(Type type, IRandomSource random, out object value)
    {
        if (!Factories.TryGetValue(type, out var factory))
        {
            value = null!;
            return false;
        }

        value = factory(random);
        return true;
    }

    private static object NextString(IRandomSource random)
    {
        Span<char> chars = stackalloc char[StringLength];

        for (var i = 0; i < StringLength; i++)
            chars[i] = Alphabet[(int)(random.NextUInt64() % (ulong)Alphabet.Length)];

        return new string(chars);
    }

    private static object NextSingle(IRandomSource random) => (float)(random.NextUInt64() >> 40) / (1 << 24);

    private static object NextDouble(IRandomSource random) => (random.NextUInt64() >> 11) / (double)(1UL << 53);

    private static object NextDecimal(IRandomSource random) =>
        new decimal((int)random.NextUInt64(), (int)(random.NextUInt64() >> 32), (int)random.NextUInt64(), isNegative: false, scale: 0);

    private static object NextGuid(IRandomSource random)
    {
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, random.NextUInt64());
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..], random.NextUInt64());
        return new Guid(bytes);
    }

    // DateTime.MinValue.Ticks..DateTime.MaxValue.Ticks, Unspecified kind - a specific Kind isn't part
    // of docs/mvp.md's built-in type list, and picking one arbitrarily would be a design decision this
    // phase doesn't need to make.
    private static object NextDateTime(IRandomSource random) => new DateTime(NextTicksInRange(random, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks));

    private static object NextDateTimeOffset(IRandomSource random) =>
        new DateTimeOffset(NextTicksInRange(random, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks), TimeSpan.Zero);

    private static object NextDateOnly(IRandomSource random) =>
        DateOnly.FromDayNumber((int)(random.NextUInt64() % (ulong)(DateOnly.MaxValue.DayNumber - DateOnly.MinValue.DayNumber + 1)));

    private static object NextTimeOnly(IRandomSource random) => new TimeOnly((long)(random.NextUInt64() % (ulong)TimeSpan.TicksPerDay));

    private static object NextTimeSpan(IRandomSource random) => new TimeSpan(unchecked((long)random.NextUInt64()));

    private static long NextTicksInRange(IRandomSource random, long minTicks, long maxTicks) =>
        minTicks + (long)(random.NextUInt64() % (ulong)(maxTicks - minTicks));
}
