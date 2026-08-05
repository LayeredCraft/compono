# Reusing Configuration

## Reach for a profile before repeating a registration

Any registration or rule used by more than one test class belongs in a
[profile](../concepts/profiles.md), not copy/pasted into each
`Composer.Create(...)` call:

```csharp
public sealed class ApplicationTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register<IClock>(_ => new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
}
```

A registration repeated across several `Composer.Create(...)` calls is
duplication that will drift the first time only one of the copies gets
updated — a profile is the one place that configuration lives.

## Compose profiles instead of writing a bigger one

Don't grow one profile to cover every concern a large test file needs;
combine smaller ones instead — `AddProfile<TProfile>()` and
`[Compose<TProfile>]` both apply as many as you list, in order:

```csharp
var composer = Composer.Create(builder => builder
    .AddProfile<DomainProfile>()
    .AddProfile<InfrastructureProfile>());
```

See [Organizing Profiles](organizing-profiles.md) for the fuller case for
why several small profiles beat one large one.

## Fall back to `UseServiceProvider` only when a container already exists

If a test host already has a configured `IServiceProvider` (an ASP.NET
Core `WebApplicationFactory`, for example), `builder.UseServiceProvider(app.Services)`
avoids duplicating every one of that container's registrations by hand. An
exact `Register<T>` always wins over the container for the same type, so
this is safe to combine with a profile's own explicit registrations —
reach for it only when a real container already exists, not as a
substitute for `Register<T>`/a profile when there isn't one.

## Don't reuse a member rule across unrelated types

A [member rule](../how-to/customize-a-member.md)
(`For<T>().Member(x => x.Y).Use(...)`) is scoped to one member of one
parent type by design — if the same override genuinely applies everywhere
a type is composed, that's a type rule (`For<T>().Use(...)`) or a
`Register<T>`, not a member rule repeated once per parent type that
happens to have a same-named member.

## Next

- The full registration/rule model these all build on →
  [Registrations and Rules](../concepts/registrations-and-rules.md).
- Applying reused configuration to a test → [Use Profiles](../how-to/use-profiles.md).
- Falling back to an existing `IServiceProvider` in depth →
  [Register a Type](../how-to/register-a-type.md#falling-back-to-an-iserviceprovider).
