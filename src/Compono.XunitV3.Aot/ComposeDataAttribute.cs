using Xunit.v3;

namespace Compono.XunitV3.Aot;

/// <summary>
/// Marks a theory method's rows for Compono composition under xUnit v3's Native AOT pipeline, reading
/// them from a public static field, property, or parameterless method - the AOT counterpart to
/// <c>Compono.XunitV3.ComposeDataAttribute</c>. Usage is identical:
/// <code>
/// [Theory]
/// [ComposeData(nameof(EmailCases))]
/// public void My_test(string address, DateTimeOffset sentAt) { ... }
/// </code>
/// </summary>
/// <remarks>
/// Marker-only, like <see cref="ComposeAttribute"/> - discovered by <c>Compono.Generators</c> at
/// compile time (matched on this type's own fully qualified metadata name, per ADR-0070) and never
/// invoked at runtime. xUnit's Native AOT pipeline supplies theory data through a generator-emitted
/// <c>RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)</c> registration instead of calling
/// <see cref="DataAttribute.GetData"/>.
/// <para>
/// Deliberately <see langword="sealed"/>, and <see cref="ComposeDataAttribute{TProfile}"/> /
/// <see cref="ComposeDataAttribute{TProfile, TConfig}"/> are independent marker siblings deriving
/// directly from <see cref="DataAttribute"/> rather than from this type - the same ADR-0067 Amendment 1
/// pattern the <see cref="ComposeAttribute"/> family uses. AOT discovery matches purely on each closed
/// attribute type's own metadata name (never on inheritance), so deriving from this type would buy a
/// consumer nothing while opening an undiscovered-subclass hazard.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeDataAttribute : DataAttribute
{
    /// <summary>
    /// Creates a <see cref="ComposeDataAttribute"/>.
    /// </summary>
    /// <param name="memberName">
    /// The name of the public static field, property, or parameterless method that supplies this
    /// theory's rows. Resolved against <see cref="MemberType"/> when it is set, otherwise against the
    /// test class itself.
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
    /// <c>Compono.XunitV3.ComposeDataAttribute.Seed</c>. Never read at runtime; the generator resolves
    /// it from this attribute's use site at compile time.
    /// </summary>
    public int Seed { get; set; }
}
