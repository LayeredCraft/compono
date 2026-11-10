using Xunit.v3;

namespace Compono.XunitV3.Aot;

/// <summary>
/// Marks a theory method's rows for Compono composition under xUnit v3's Native AOT pipeline with
/// <typeparamref name="TProfile"/> applied - the AOT counterpart to
/// <c>Compono.XunitV3.ComposeDataAttribute&lt;TProfile&gt;</c>. Usage is identical:
/// <code>
/// [Theory]
/// [ComposeData&lt;MyProfile&gt;(nameof(EmailCases))]
/// public void My_test(string address, DateTimeOffset sentAt) { ... }
/// </code>
/// </summary>
/// <typeparam name="TProfile">
/// The profile to apply. The <c>new()</c> constraint mirrors
/// <c>Compono.XunitV3.ComposeDataAttribute&lt;TProfile&gt;</c>'s own designed shape.
/// </typeparam>
/// <remarks>
/// Marker-only, and an independent sealed sibling of <see cref="ComposeDataAttribute"/> - it derives
/// directly from <see cref="DataAttribute"/>, <b>not</b> from the non-generic marker, and repeats the
/// <see cref="MemberName"/>/<see cref="MemberType"/>/<see cref="Seed"/> shape so the two engines expose
/// the same consumer-facing declarations (ADR-0067 Amendment 1, extended by ADR-0070).
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeDataAttribute<TProfile> : DataAttribute
    where TProfile : ICompositionProfile, new()
{
    /// <summary>
    /// Creates a <see cref="ComposeDataAttribute{TProfile}"/>.
    /// </summary>
    /// <param name="memberName">
    /// The name of the public static field, property, or parameterless method that supplies this
    /// theory's rows - see <see cref="MemberName"/>.
    /// </param>
    public ComposeDataAttribute(string memberName)
    {
        MemberName = memberName;
    }

    /// <summary>
    /// The name of the public static member that supplies this theory's rows.
    /// </summary>
    public string MemberName { get; }

    /// <summary>
    /// The type that declares <see cref="MemberName"/>. <see langword="null"/> (the default) means the
    /// test class itself, matching xUnit's own <c>MemberData</c> convention.
    /// </summary>
    public Type? MemberType { get; set; }

    /// <summary>
    /// An explicit root seed pinned to every row this attribute produces - the same contract as
    /// <c>Compono.XunitV3.ComposeDataAttribute.Seed</c>.
    /// </summary>
    public int Seed { get; set; }
}
