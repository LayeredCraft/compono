namespace Compono;

/// <summary>
/// What a <see cref="CompositionRequestDescriptor"/> is requesting a value for.
/// </summary>
public enum CompositionRequestKind
{
    /// <summary>The request is for a selected constructor's parameter.</summary>
    ConstructorParameter,

    /// <summary>The request is for a required init-only member.</summary>
    RequiredMember,
}
