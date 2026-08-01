using Bogus;

namespace Compono;

/// <summary>
/// Activates <see cref="BogusMemberNameProvider"/> on a <see cref="CompositionBuilder"/>, and
/// registers a whole-object <see cref="Faker{T}"/>-backed exact registration for one type. See
/// <c>docs/adr/0027-compono-bogus-package-design.md</c>.
/// </summary>
public static class CompositionBuilderExtensions
{
    extension(CompositionBuilder builder)
    {
        /// <summary>
        /// Registers a <see cref="BogusMemberNameProvider"/> with default <see cref="BogusOptions"/>.
        /// </summary>
        public CompositionBuilder UseBogus() => builder.UseBogus(static _ => { });

        /// <summary>
        /// Registers a <see cref="BogusMemberNameProvider"/>, configured by <paramref name="configure"/>.
        /// </summary>
        /// <param name="configure">Sets the provider's <see cref="BogusOptions"/>.</param>
        public CompositionBuilder UseBogus(Action<BogusOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            var options = new BogusOptions();
            configure(options);

            return options.EnableMemberNameConventions
                ? builder.AddSemanticProvider(new BogusMemberNameProvider(options.Locale))
                : builder;
        }

        /// <summary>
        /// Registers <typeparamref name="T"/> as exclusively Bogus-generated, via a
        /// <see cref="Faker{T}"/> instance <paramref name="configureFaker"/> configures. Purely
        /// ergonomic sugar over <see cref="CompositionBuilder.Register{T}(Func{ICompositionContext, T})"/>
        /// (stage 3) - no hidden pipeline stage, no special runtime behavior of its own. Independent of
        /// <c>UseBogus()</c>/<c>UseBogus(Action{BogusOptions})</c> - defaults to the
        /// locale <c>"en"</c> on its own, never reads <see cref="BogusOptions.Locale"/>.
        /// </summary>
        /// <typeparam name="T">The type <see cref="Faker{T}"/> generates.</typeparam>
        /// <param name="configureFaker">
        /// Configures the <see cref="Faker{T}"/> instance in place (e.g. via <c>RuleFor</c>) - does not
        /// need to, and should not, return a different instance.
        /// </param>
        public CompositionBuilder UseBogus<T>(Action<Faker<T>> configureFaker)
            where T : class =>
            builder.UseBogus("en", configureFaker);

        /// <summary>
        /// Registers <typeparamref name="T"/> as exclusively Bogus-generated, via a
        /// <see cref="Faker{T}"/> instance <paramref name="configureFaker"/> configures, using
        /// <paramref name="locale"/>. Purely ergonomic sugar over
        /// <see cref="CompositionBuilder.Register{T}(Func{ICompositionContext, T})"/> (stage 3) - no
        /// hidden pipeline stage, no special runtime behavior of its own. Independent of
        /// <c>UseBogus()</c>/<c>UseBogus(Action{BogusOptions})</c> - never reads
        /// <see cref="BogusOptions.Locale"/>.
        /// </summary>
        /// <typeparam name="T">The type <see cref="Faker{T}"/> generates.</typeparam>
        /// <param name="locale">The Bogus locale this registration's own <see cref="Faker{T}"/> uses.</param>
        /// <param name="configureFaker">
        /// Configures the <see cref="Faker{T}"/> instance in place (e.g. via <c>RuleFor</c>) - does not
        /// need to, and should not, return a different instance.
        /// </param>
        public CompositionBuilder UseBogus<T>(string locale, Action<Faker<T>> configureFaker)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(locale);
            ArgumentNullException.ThrowIfNull(configureFaker);

            return builder.Register<T>(context =>
            {
                // A fresh Faker<T> constructed inside the factory, once per T resolution - the factory
                // closure itself is captured once at Build() time (like any other Register<T> factory),
                // but no Faker<T> instance is ever retained or reused across requests, so concurrent
                // Create<T>() calls for the same T never share one. See ADR-0027's Model 3 section for
                // why a cached/shared instance was considered and rejected.
                var faker = new Faker<T>(locale);
                configureFaker(faker);
                return faker.UseSeed(context.DeriveSeed()).Generate();
            });
        }
    }
}
