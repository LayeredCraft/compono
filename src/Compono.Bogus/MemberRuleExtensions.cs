using Bogus;

namespace Compono;

/// <summary>
/// Adds Bogus-backed sugar to <see cref="CompositionMemberRuleBuilder{TParent, TMember}"/> - an
/// explicit member rule whose value comes from a deterministically-seeded <see cref="Faker"/>, for a
/// member the conservative <see cref="BogusMemberNameProvider"/> convention allowlist doesn't (or
/// shouldn't) guess. See <c>docs/adr/0027-compono-bogus-package-design.md</c>.
/// </summary>
public static class MemberRuleExtensions
{
    extension<TParent, TMember>(CompositionMemberRuleBuilder<TParent, TMember> builder)
        where TMember : notnull
    {
        /// <summary>
        /// Registers a member rule whose value comes from a deterministically-seeded <see cref="Faker"/>
        /// - purely ergonomic sugar over
        /// <see cref="CompositionMemberRuleBuilder{TParent, TMember}.Use(Func{ICompositionContext, TMember})"/>.
        /// Always wins over <see cref="BogusMemberNameProvider"/>'s convention guess for the same
        /// member, since stage 4 (configuration rules) runs before stage 5 (semantic providers)
        /// unconditionally.
        /// </summary>
        /// <param name="configure">Produces this member's value from a seeded <see cref="Faker"/>.</param>
        /// <param name="locale">The Bogus locale this rule's own <see cref="Faker"/> uses.</param>
        public CompositionBuilder UseBogus(Func<Faker, TMember> configure, string locale = "en")
        {
            ArgumentNullException.ThrowIfNull(configure);
            ArgumentNullException.ThrowIfNull(locale);

            return builder.Use(context =>
            {
                var faker = new Faker(locale) { Random = new Randomizer(context.DeriveSeed()) };
                return configure(faker);
            });
        }
    }
}
