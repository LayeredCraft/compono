namespace Compono;

/// <summary>
/// Void-marker type for a generated test double's <c>void</c>/<see langword="Task"/>-returning
/// members, so <see cref="ReturnConfig{T}"/> has a closeable type argument even when the member
/// itself returns nothing. <see langword="public"/> from the start (not <see langword="internal"/>)
/// - the same cross-assembly-accessibility lesson ADR-0043 Amendment 3 already applied to
/// <see cref="ReturnConfig{T}"/>/<see cref="ReturnConfigBuilder{T}"/> applies here too: a generated
/// double lives in the consumer's own assembly, not core <c>Compono</c>. See ADR-0043 Amendment 4.
/// </summary>
public readonly struct Unit;
