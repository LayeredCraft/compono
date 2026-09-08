# [RESEARCH-0028] .NET Configuration/Options Testing Support: Capability & Package Admission Research

**Status:** Research complete, reassessed once (2026-09-07, same day as
original). No ADR yet — this document is the pre-ADR evidence base for a
future problem-only `Proposed` ADR, per `design-decisions.md`'s rule that
a design dive's research phase precedes drafting one, and per this
investigation's own explicit scope (admission recommendation only, no
design, no ADR).

**Governing process:** [`docs/architecture/capability-admission.md`](../architecture/capability-admission.md),
applied faithfully end to end. Gate A (architectural admission) and Gate B
(evidence admission) are evaluated as separate questions, per capability
slice — not as one blanket verdict for "Configuration/Options."

**Trigger:** an explicit product-owner request (Nick Cipollina, 2026-09-07)
for first-class Compono support in .NET Configuration/Options testing,
explicitly conditioned on the request only counting as Gate B evidence for
whatever survives Gate A on its own architectural merits — not a mandate
to admit a package. **Reassessed the same day** against a sharpened
version of that request — see **Reassessment**, below the Executive
Conclusion — clarifying that the underlying product goal is composition
ergonomics ("make dependencies easier, more obvious, and more pleasant to
compose in tests") as legitimate value in its own right, not merely a
request to wrap already-simple APIs.

**Candidate framing, as instructed:** not "should we build `Compono.Options`"
or "should we build `Compono.Configuration`" — package names and
boundaries are this document's *conclusion* (§17), not its premise. The
actual question: *should Compono provide first-class testing/composition
capabilities for .NET Configuration and/or Options, and if so, for which
concrete problems?*

**2026-09-07 reassessment note:** this document was revisited once,
against clarified product-owner context about composition ergonomics as
legitimate Compono-specific value in their own right. See the
**Reassessment** section immediately below for what changed, what didn't,
and why. Sections carrying a **(Reassessed 2026-09-07)** marker were
revised; all others are unchanged from the original investigation.

---

## Reassessment (2026-09-07): composition ergonomics as legitimate Compono-specific value

**What prompted this.** The original investigation treated "the raw .NET
API is already simple" as sufficient reason to stop at Step 2 of the
admission process for both Configuration and `IOptions<T>`. The
product owner pushed back on that inference specifically — not by
disputing that the raw APIs are simple, but by asking whether "simple to
construct" and "natural to compose *inside Compono's model*" are the same
question. They aren't, and treating them as interchangeable was this
document's one real gap.

**The sharper question, stated precisely:** not *"is the .NET construction
call short?"* but *"does a Compono user stay inside Compono's composition
model to get this dependency, or do they have to drop out of it and
reconstruct framework-specific ceremony by hand, from memory, every time?"*
A short construction call can still represent real, repeated, avoidable
friction if a consumer has to (a) remember which of three related
interfaces they need, (b) remember the wrapping/registration idiom, and
(c) keep multiple related registrations consistent with each other by
hand. None of that is captured by "how many lines is the `new
ConfigurationBuilder()` call."

**Does this lower the admission bar?** No — and this reassessment is
explicit about why not. `docs/architecture/capability-admission.md`'s
Step 2 ("boilerplate alone does not justify a new abstraction") stays
exactly as written and is applied just as strictly below. What changes is
*which question counts as evidence for Step 3's "meaningful abstraction"
criterion* — not whether that criterion still has to be cleared on real
evidence. A capability that survives this reassessment still has to show
something more than line-count reduction; several slices reassessed below
(Configuration, in particular) are re-examined under the sharper question
and still land in the same place, for a sharper reason.

**Repository evidence that Compono already treats this as legitimate
product value, independent of raw technical difficulty:**

- **`CompositionBuilder.Share<T>()` (ADR-0056).** Before this shipped,
  sharing a value across a composition graph was **already fully
  possible** via `[Shared]` (ADR-0011/ADR-0022) — nothing was technically
  blocked. `Share<T>()` was admitted anyway because expressing sharing as
  **graph-wide, profile-embeddable, framework-independent composition
  configuration** — rather than a per-test-signature attribute a consumer
  has to remember to attach on every relevant parameter — was itself
  judged to be real product value. ADR-0056's own Context is explicit:
  the gap was that "the sharing intent was really a property of the
  *composition configuration*... not of any one test." This is the exact
  shape of argument the product owner is making for Configuration/Options.
- **`Compono.Bogus` (ADR-0027).** Constructing a string that looks like a
  plausible email address (`"jane@example.com"`) is not technically hard
  — a consumer could always hand-write one. `Compono.Bogus` was admitted
  anyway, explicitly framed around ergonomics and discoverability ("just
  call `UseBogus()` and get realistic values" — ADR-0027's own Context),
  not around raw difficulty.

**Conclusion from this evidence:** Compono's own accepted history does
**not** require an underlying operation to be difficult before granting it
first-class composition ergonomics. It has, at least twice, admitted a
capability whose primary value is discoverability, consistency, and
staying inside the composition model — precisely the dimension the
original research under-weighted. This is an existing, evidenced Compono
product principle, not one invented for this candidate — see
**Consideration for `capability-admission.md`** below for whether the
process document itself should say this more explicitly.

**What this does *not* change:** §7–§9's `IOptionsMonitor<T>` correctness
findings (named options, multi-subscriber `OnChange`, real disposal,
thread-safety, "don't simulate the full reload pipeline") and §13/§14's
ADR-0052 Finding B analysis are **preserved unchanged** — they were never
about raw-difficulty reasoning in the first place; they already rested on
documented behavioral gaps in existing practice, which is a stronger
evidence bar than this reassessment's question even asks for.

**Consideration for `docs/architecture/capability-admission.md`:** the
process document's Step 1–2 framing ("a concrete problem," "check whether
it's already solved") reads, on its own, as if raw construction difficulty
is the primary signal — it doesn't currently name "does this let a
consumer stay inside Compono's composition model, expressed once and
reused via profiles, instead of reconstructing framework ceremony
repeatedly" as its own recognized form of evidence, even though
`Share<T>()`/`Compono.Bogus` show this repo already accepts exactly that
argument. This looks like a real, if minor, gap in how the consolidated
document explains itself — flagged here as instructed, **not corrected in
this pass**: this task is scoped to reassessing RESEARCH-0028, and
`capability-admission.md` is only to be changed if the engineering
workflow explicitly requires it for this outcome, which it does not.

---

## 1. Executive conclusion (Reassessed 2026-09-07)

Configuration/Options still decomposes into independently-answerable
slices — that structure was correct and is preserved — but the reasoning
behind two of them changed, and one new coherent capability emerges from
combining ergonomics with the original correctness analysis:

| Slice | Outcome |
|---|---|
| Consistent `IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>` composition for a given `T`, backed by one coherent, deterministic, discoverable implementation | **Roadmap item** |
| `IOptions<T>` *alone*, with no Monitor/Snapshot need | **Already solved** — no new capability (acceptable Compono-native alternative, existing); the ergonomic case only strengthens once Monitor/Snapshot need a package anyway (§5, §16) |
| Automatic composition of `IOptions<T>`/`IOptionsMonitor<T>` for *any* composed `T` with no manual registration | **Documentation-only**, pending a prerequisite core decision (ADR-0052 Finding B, still open) — unchanged by this reassessment (§14) |
| `IConfiguration`/`ConfigurationBuilder` composition (in-memory sources, binding, layered overrides) | **Documentation-only** — reassessed under the sharper ergonomics question and still lands here, for a sharper reason (§3) |
| Options validation (`IValidateOptions<T>`, `DataAnnotations`, source-generated validators) | **Rejected** — unchanged; still a production/runtime concern, not a testing/composition gap |

**What changed:** the original document treated `IOptionsMonitor<T>` and
`IOptionsSnapshot<T>` as two separately-scored slices that happened to
share cheap implementation. The reassessment reframes them as one
**coherent capability** — consistent, correctly-wired composition across
all three related Options interfaces for a given `T` — because the real
product risk this uncovers is a consumer wiring `IOptions<T>` to one value
and `IOptionsMonitor<T>` to a different, inconsistently-constructed one
(a genuine, subtle correctness trap a hand-rolled setup invites), not
merely "three interfaces are annoying to remember separately." This is a
materially stronger justification for `IOptionsSnapshot<T>`'s inclusion
than "cheap to implement alongside Monitor" — see §6.

**Recommended package boundary — unchanged from the original conclusion:**
a single new package, **`Compono.Options`** — hand-written (non-generated)
runtime classes providing correct, consistently-wired `IOptions<T>`/
`IOptionsSnapshot<T>`/`IOptionsMonitor<T>` test doubles, architecturally a
sibling of `Compono.Http` (depends only on core `Compono`, no generator
dependency, no reflection). **`Compono.Configuration` should still not
exist** — reassessed under the sharper ergonomics question in §3, not
merely re-asserted. Nothing belongs in `Compono.DependencyInjection` —
unchanged (§12).

**Gate B:** satisfied for the `Compono.Options` roadmap item by the
explicit, now-more-precise product-owner request (§19), the same
mechanism that already cleared `Compono.TUnit`, `Compono.NUnit`, and
Compono-owned source-generated test doubles. No dogfooding evidence was
manufactured or required for this to count.

---

## 2. Candidate/problem statement

Stated per the admission process's Step 1 (a concrete problem, not "this
would be nice" or "other libraries have this"):

.NET's Options pattern (`IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>`)
is the standard, idiomatic way a modern .NET application receives
strongly-typed configuration. Any real application composed with Compono
that reads configuration through this pattern needs its test doubles
composed the same way every other dependency is — but `IOptionsMonitor<T>`
and `IOptionsSnapshot<T>` have no first-party test double at all (§4), and
the community's own standard answer (a hand-rolled fake, §7) is
demonstrably incomplete in ways that make it a *misleading* fake, not
merely an inconvenient one. This is a real, evidenced problem (§4, §7),
not a speculative one.

---

## 3. Existing .NET Configuration testing story (Reassessed 2026-09-07)

`IConfiguration`/`IConfigurationRoot`/`IConfigurationSection`/`ConfigurationBuilder`
already have a trivial, first-party, in-memory testing path:

```csharp
IConfiguration configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Smtp:Host"] = "localhost",
        ["Smtp:Port"] = "2525",
    })
    .Build();
```

- `AddInMemoryCollection` is purpose-built for tests — no file system, no
  environment variables, fully deterministic.
- `GetSection`/`GetValue<T>`/`Bind`/`Get<T>` all work identically against
  an in-memory source as against any other provider — there is no
  Configuration-specific behavior a test needs that production code
  doesn't already exercise the same way.
- **Binding is already source-generation-first when it matters.**
  `Microsoft.Extensions.Configuration.Binder` ships a Roslyn source
  generator (`EnableConfigurationBindingGenerator`) that rewrites
  `ConfigurationBinder.Bind`/`Get<T>` call sites to generated,
  reflection-free code — enabled automatically whenever a project sets
  `PublishAot`. .NET has already solved the exact "source-generated,
  AOT-safe configuration binding" problem Compono's own architecture
  cares about, for the exact same reason Compono cares about it. (See
  [Compile-time configuration source generation](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-generator).)

**Reassessed against the sharper question (§"Reassessment," above): does a
Compono user stay inside the composition model, or drop out of it?**
`Register<IConfiguration>(() => new ConfigurationBuilder()...)` keeps a
consumer entirely inside ordinary Compono idiom — `Register<T>` already
generically handles `IConfiguration` (or any other type) with zero
Compono-specific ceremony beyond a call every consumer already has to
learn once for any registered type. There is no analog here to
`Share<T>()`'s actual gap (an implementation detail — *that* a value is
shared — leaking into every relevant test signature) or to the real
Options-consistency risk found in §5/§6 (multiple related interfaces that
must stay wired to the same value). Layering a base configuration with
test-specific overrides — one of the scenarios flagged for reassessment —
is also already a "few obvious lines" under the sharper question, not just
the original one: `AddInMemoryCollection(base).AddInMemoryCollection(overrides)`,
later source wins, no hidden precedent to memorize. **This is a case where
the sharper ergonomics question was applied in good faith and still
produces the same answer, for a sharper reason**: not because the raw API
is short, but because nothing about *staying inside Compono's model* is
harder for Configuration than for any other `Register<T>`-shaped type,
and there is no multi-interface-consistency trap analogous to Options'.

**Conclusion (unchanged):** there is no genuine Configuration-testing gap
for Compono to fill, under either framing. The only thing worth producing
here is a Cookbook recipe showing this pattern next to Compono composition
(e.g. composing a type that takes `IConfiguration` via
`Register<IConfiguration>(() => ...)`, including the layered-override
variant) — not a package, and not a dedicated `Use*` builder extension
either, since a named wrapper over one `Register<T>` call for one
already-simple type would itself fail "meaningful abstraction" the same
way a trivial extension method always does.

---

## 4. Existing .NET Options testing story

Per current (`.NET 8`/`9`, verified against
[Options pattern - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/options),
updated 2026-05-15) first-party guidance:

| Interface | Lifetime | Named options | Change notifications | First-party test double? |
|---|---|---|---|---|
| `IOptions<T>` | Singleton | No | No | No, but trivial: `Options.Create(value)` |
| `IOptionsSnapshot<T>` | Scoped | Yes (`Get(name)`) | Recomputed per scope, not pushed | **None** |
| `IOptionsMonitor<T>` | Singleton | Yes (`Get(name)`) | Yes (`OnChange`, `CurrentValue`) | **None** |

`IOptionsFactory<T>` (creates instances from registered
`IConfigureOptions<T>`/`IPostConfigureOptions<T>`),
`IOptionsMonitorCache<T>` (invalidates/replaces cached named instances),
and `IOptionsChangeTokenSource<T>` (supplies the `IChangeToken` a monitor
watches) are the machinery `OptionsMonitor<T>`'s real implementation is
built from — not something an application or test author normally
implements directly, but directly relevant to what a *correct* test double
has to reproduce (§7).

**These three interfaces are not equivalent wrappers around a value** —
this distinction is the crux of the whole investigation:

- `IOptions<T>` — one value, computed once, never changes. Testing it is
  already solved (`Options.Create(...)`).
- `IOptionsSnapshot<T>` — recomputed once per DI scope; supports named
  options; does **not** push changes mid-scope. A unit test rarely spins
  up a real DI scope, so what it actually needs from a snapshot fake is
  "a fixed value, addressable by name" — not scope-lifecycle simulation.
- `IOptionsMonitor<T>` — one long-lived instance for the app's lifetime;
  supports named options *and* live change notification via `OnChange`.
  This is the one with real behavioral surface a fake has to get right:
  multiple independent subscribers, per-subscriber disposal, and a
  `CurrentValue`/`Get(name)` that actually changes when the underlying
  value changes.

---

## 5. `IOptions<T>` — detailed analysis (Reassessed 2026-09-07)

**Production shape:** `IOptions<T>.Value` — one non-nullable property.

**Already solved, twice over, inside this repo's own ecosystem:**

1. **As a mock/stub double.** `Compono.TestDoubles` v2 already generates a
   working double for `IOptions<T>` — confirmed by real dogfooding
   (`docs/research/0005-lightsaber-skill-testdoubles-v2-third-dogfood.md`):
   `IOptions<LightsaberOptions>` **generates and resolves** via
   `Configure().Value().Returns(...)`, once ADR-0045's
   configuration-required-members mechanism closed the `CMP0025`
   whole-interface-rejection gap that originally blocked it (a correction
   recorded in ADR-0044 Amendment 17). No new Compono capability is needed
   for "give me an `IOptions<T>` returning an arbitrary configured value."
2. **As a value composed from Compono's own composition of `T`.** Real
   consumer code already does this by hand today, via an ordinary
   registration:

   ```csharp
   builder.Register<IOptions<MyOptions>>(context =>
       Options.Create(context.Resolve<MyOptions>()));
   ```

   This is genuine, working Compono idiom (seen in
   `docs/research/0001-autofixture-comparison.md`,
   `docs/research/0011-...testkit-migration-slice-1.md`) — **and** it is
   the exact pattern that surfaced ADR-0052 Finding B (§13, §14): when
   `MyOptions` isn't independently discovered as a root elsewhere,
   `context.Resolve<MyOptions>()` inside this factory has no generated
   plan to fall back to, and fails at test-run time with a
   `CompositionException`. This is not a defect specific to `IOptions<T>`
   — it's ADR-0052's already-recorded, currently-*unresolved* nested-resolve
   discovery gap, which affects any registration factory reaching for a
   type only known through its own body, not something specific to the
   Options pattern.

**Reassessed against the sharper ergonomics question:** taken in complete
isolation — a SUT that only ever needs `IOptions<T>`, never `Snapshot`/
`Monitor` — this remains a case where "does the consumer stay inside
Compono's model" and "is the raw API short" happen to agree: `Register<IOptions<T>>(() =>
Options.Create(value))` is exactly as much Compono idiom as any other
`Register<T>` call, with no separate ceremony to remember and no
multi-interface consistency risk (there's only one interface in play).
Standing entirely alone, this still does not clear "meaningful
abstraction" any more than Configuration does (§3) — a dedicated
`IOptions<T>`-only wrapper would be exactly the kind of trivial,
name-only convenience the admission process's Step 2/3 exist to catch.

**But `IOptions<T>` is not always standing alone.** The real ergonomic
risk is specific to a SUT that depends on `IOptions<T>` *and*
`IOptionsMonitor<T>` (or `IOptionsSnapshot<T>`) for the *same* underlying
settings type — a genuinely common shape (a component reads
`IOptions<T>.Value` once at construction while another part of the same
system watches `IOptionsMonitor<T>` for live changes). Wired by hand, a
consumer has to remember to construct both from the *same* value and keep
them consistent as the test evolves; nothing prevents them silently
drifting apart. This is exactly the kind of "reducing opportunities to
construct subtly incorrect substitutes" value the reassessment was asked
to weigh — and it only exists once `Compono.Options` is being built
anyway for Monitor/Snapshot (§7). It's real ergonomic value, but it's
value the future `Compono.Options` package's own design should capture
(offering a consistent way to get all three interfaces from one value),
not a separate justification for a standalone `IOptions<T>` capability.

**Conclusion (unchanged in substance, reframed in scope):** `IOptions<T>`
composition/mocking *on its own* is an **acceptable Compono-native
alternative, already shipped** — no new capability, no ADR, no standalone
package or helper. Its ergonomic story materially improves only as part
of `Compono.Options`'s broader, coherent Monitor/Snapshot/`IOptions<T>`
consistency story (§16, §18), not as an independent admission. The one
real friction point (wrapping an auto-composed `T`) is unchanged from the
original finding — it's the general, already-tracked ADR-0052 Finding B
(§14), not an Options-specific gap.

---

## 6. `IOptionsSnapshot<T>` — detailed analysis (Reassessed 2026-09-07)

No dogfooding evidence exists anywhere in this repo's research history for
`IOptionsSnapshot<T>` specifically (grep across `docs/research/*.md` and
`docs/roadmap/*.md` found none) — unlike `IOptions<T>`, which has three
independent real-project sightings.

Ordinary .NET testing practice for `IOptionsSnapshot<T>` either (a) spins
up a real, minimal `ServiceCollection`/`ServiceProvider` and registers
`.Configure<T>(...)`, which works but pulls a full DI container into a
unit test purely to get scope-shaped options — heavier than Compono's own
"compose only what's needed" philosophy — or (b) hand-writes a fake
identical in shape to an `IOptionsMonitor<T>` fake, minus change
notification, since the only behavior a unit test actually exercises is
`Value`/`Get(name)`.

**Reassessed — explicitly not on cost grounds.** The product owner
specifically flagged that "cheap to implement alongside Monitor" is not,
by itself, product justification, and that concern is correct: shared
implementation cost is a fact about engineering effort, not about whether
a consumer actually needs this. Re-examined on its own merits instead:

`IOptionsSnapshot<T>` is a real, common shape in production code
(anything written against scoped/per-request configuration — the
idiomatic choice for most ASP.NET Core request-scoped consumers, per
Microsoft's own guidance in §4). A SUT built that way, composed with
Compono, needs `IOptionsSnapshot<T>` satisfied like any other dependency.
If `Compono.Options` ships `IOptionsMonitor<T>` support only, a consumer
whose SUT happens to depend on `IOptionsSnapshot<T>` instead gets no
first-class help at all — pushed right back to hand-rolling exactly the
kind of incomplete fake (§7) the whole package exists to prevent, for a
sibling interface a real SUT is just as likely to need. That's a
coherence gap in the *capability itself* (an Options-testing package that
only covers one of the two named-options-supporting interfaces), not a
convenience shortfall.

**Conclusion:** `IOptionsSnapshot<T>` earns its place in `Compono.Options`
because it completes a coherent capability real SUTs need addressed
together (`IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>`, all
needing to stay consistent for a given `T`, per §5's reframed finding) —
not because implementation cost happens to be low. Low shared cost remains
true and relevant to §15's cost analysis, but it is not, on its own, why
Snapshot belongs in scope.

---

## 7. `IOptionsMonitor<T>` — detailed analysis (the strongest candidate)

### What a correct implementation has to preserve

From `OptionsMonitor<TOptions>`'s actual constructor and behavior
(confirmed directly against
[dotnet/runtime's `OptionsMonitor.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Options/src/OptionsMonitor.cs)):

```csharp
public OptionsMonitor(
    IOptionsFactory<TOptions> factory,
    IEnumerable<IOptionsChangeTokenSource<TOptions>> sources,
    IOptionsMonitorCache<TOptions> cache)
```

- `CurrentValue => Get(Options.DefaultName)` — reading the default-name
  value is just `Get` with a well-known name; no separate storage.
- **Change propagation**: when a watched `IChangeToken` fires,
  `InvokeChanged` removes the affected name from the cache
  (`_cache.TryRemove(name)`), recomputes it via `Get(name)`, and invokes
  `_onChange?.Invoke(options, name)` — every subscriber, not just one.
- **`OnChange`**: registers a listener, returns an `IDisposable` that
  unsubscribes *that specific listener* on `Dispose()` — multiple
  independent subscribers are a first-class, expected scenario.
- **Named options**: `Get(name)` is looked up per name, independently
  cached.
- **Disposal**: `Dispose()` unregisters every change-token subscription it
  made — a real resource-cleanup contract, not a no-op.

### What the community's actual answer gets wrong

The most widely-cited pattern for testing `IOptionsMonitor<T>`
([Testing IOptionsMonitor - Ben Foster](https://benfoster.io/blog/20200610-testing-ioptionsmonitor/),
and the same shape recurs across
[code-maze](https://code-maze.com/csharp-mock-ioptions/),
[thecodebuzz](https://thecodebuzz.com/unit-test-mock-ioption-net-core-ioption-moq-appsettings/),
and similar articles) is a hand-rolled class:

```csharp
public class TestOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
{
    private Action<TOptions, string> _listener;

    public TestOptionsMonitor(TOptions currentValue) => CurrentValue = currentValue;
    public TOptions CurrentValue { get; private set; }
    public TOptions Get(string name) => CurrentValue;          // ignores name entirely
    public void Set(TOptions value)
    {
        CurrentValue = value;
        _listener.Invoke(value, null);                          // single listener only
    }
    public IDisposable OnChange(Action<TOptions, string> listener)
    {
        _listener = listener;                                   // overwrites any prior subscriber
        return Mock.Of<IDisposable>();                           // fake disposable, no real unsubscribe
    }
}
```

This is **not a faithful fake** — three concrete, documented gaps:

1. **`Get(name)` ignores `name`** — named options are silently broken; a
   system under test that distinguishes named configurations can't be
   tested correctly at all.
2. **Single-listener only** — a second `OnChange` subscription silently
   *replaces* the first rather than adding to it, unlike real
   `OptionsMonitor<T>`, which supports and invokes every subscriber.
3. **`OnChange`'s returned `IDisposable` doesn't unsubscribe anything** —
   disposal is a no-op, so a test can't verify unsubscribe behavior, and a
   production code path that disposes its subscription and expects no
   further callbacks would pass against this fake while being broken
   against the real thing.

This is exactly the "misleading fake" risk the admission process's
investigation prompt asked to guard against — a naive implementation
*looks* like it solves the problem and is exactly what gets copy-pasted
project to project, while silently under-testing named options,
multi-subscriber behavior, and disposal.

### Gate A's "meaningful abstraction" test, applied directly

Step 3 asks: *could a consumer already write the equivalent correctly in a
few obvious lines?* The answer, demonstrated by real, widely-published
prior art above, is **no** — the "few obvious lines" version that gets
written in practice is measurably wrong in three independent ways. A
correct implementation needs a per-name value store, a real multi-listener
event with per-listener disposal, and thread-safety (the production type
is a singleton other singletons may call concurrently) — meaningfully more
machinery than the naive version, and machinery a consumer is unlikely to
get right unprompted. This clears "meaningful abstraction" on real,
sourced evidence, not assumption.

### What must NOT be modeled

Per the admission process's cost-proportionality principle and this
repo's own restraint (`design-principles.md`'s "avoid becoming... a
reflection-heavy runtime" / "a feature-complete wrapper"), a Compono
`IOptionsMonitor<T>` test double should be a **deliberately narrower
testing abstraction**, not a reimplementation of the full production
pipeline:

- No real `IConfiguration`/change-token wiring is needed — a test wants to
  *deliberately* push a new value and see subscribers fire, not simulate
  file-system polling or JSON reload.
- No `IOptionsFactory<T>`/`IConfigureOptions<T>` pipeline is needed — the
  test already has (or composes) the exact `T` values it wants; there is
  no "configure via delegate then materialize" step to replicate.
- Synchronous notification (matching real `OptionsMonitor<T>`'s own
  synchronous `_onChange?.Invoke(...)` call) is correct and sufficient —
  no async notification model should be invented (this is also explicitly
  out of scope per this investigation's exclusions).

---

## 8. Named-options analysis

Both `IOptionsSnapshot<T>.Get(name)` and `IOptionsMonitor<T>.Get(name)`
need real, independent per-name storage — this is the single most common
correctness gap in hand-rolled fakes (§7, gap 1). A correct Compono
implementation needs, at minimum, a name-keyed dictionary of current
values, with `Options.DefaultName` (`string.Empty`) as the well-known
default-name key `CurrentValue`/parameterless `Value` reads through — the
same semantic `IOptionsMonitorCache<T>` already establishes in the real
implementation. This is not extra scope invented for completeness; it's
required to avoid becoming exactly the kind of "misleading fake" this
investigation was asked to guard against.

---

## 9. Change/reload/`OnChange` analysis

Real per-file-provider reload (JSON/INI/XML/user-secrets/KeyPerFile
sources watching the file system, per the Learn page's own list) is
explicitly out of scope for this investigation (excluded: "configuration-file
mocking," "environment-variable mocking") and, more importantly, is not
where the real friction lives — a unit test doesn't want to touch the file
system at all. The friction is in the *notification mechanism itself*:
giving a test a deterministic way to say "the value changed now, fire
every subscriber synchronously" without needing any real configuration
source, change token, or DI container. That's a Compono-native testing
primitive (a `.Set(newValue)`/`.Change(newValue)`-shaped method), not a
simulation of .NET's reload pipeline.

---

## 10. Validation analysis

`IValidateOptions<T>`, `DataAnnotations`-based validation
(`ValidateDataAnnotations`), `ValidateOnStart`, and the compile-time
options-validation source generator
([Compile-time options validation source generation](https://learn.microsoft.com/en-us/dotnet/core/extensions/options-validation-generator))
are all first-party, already source-generation-first where AOT matters,
and are a **production configuration-correctness concern**, not a test
composition/double concern — validation runs against real configuration
at startup or first access, and there's no Compono-specific value in
wrapping a validation call that already works identically against a
composed value. **Rejected** — not because the API is thin (`Compono.FakeItEasy`'s
reason), but because it fails "Compono-specific value" outright: it's not
a testing-friction problem at all.

---

## 11. Configuration ↔ Options interaction

Options binds to Configuration via `services.Configure<T>(configuration.GetSection(...))`
— the only interaction point relevant to testing is that
`IOptionsMonitor<T>`'s change notification is *driven by* a
`IConfigurationRoot.Reload()`-triggered change token when Options is wired
this way in production. Since §9 established that a Compono test double
should let a test push a new value directly rather than simulate
Configuration's reload pipeline, this interaction doesn't create any
additional capability requirement — a Compono `IOptionsMonitor<T>` fake
that lets a test call `.Set(newValue)` directly is strictly simpler than,
and does not need to model, the Configuration-driven path real production
code uses.

---

## 12. Compono core / `Compono.DependencyInjection` interaction

Read directly rather than assumed from the package's name
(`src/Compono.DependencyInjection/ComponoServiceProvider.cs`,
`CompositionRowServiceProviderExtensions.cs`; ADR-0047):

- **What it actually owns:** exactly one public member,
  `CompositionRow.AsServiceProvider()`, returning an `IServiceProvider`
  backed by `CompositionRow.TryResolveConfigured` — a **pull-only**
  bridge over values a row can *already* resolve (row scope, exact
  `Register<T>` registrations, stage 4-6 value providers). It resolves
  nothing itself; it forwards to the row and caches per-type identity.
- **What it explicitly does not do:** construct, compose, or own any new
  value-production logic. It has no concept of `IOptions<T>`,
  `IOptionsMonitor<T>`, named options, or change notification — nothing
  about its actual code touches configuration or options at all.
- **Why Configuration/Options support does not belong here:** a hand-authored
  `IOptionsMonitor<T>` test double is production/composition logic — it
  *creates* a new kind of composable value, the same category of work
  `Compono.Http`'s `TestHttpHandler` or `Compono.Logging`'s
  `CapturingLogger` do. `Compono.DependencyInjection`'s entire design is
  the opposite: it never creates anything, it only forwards already-resolved
  values through a different interface shape. Bolting options-construction
  logic onto it would broaden a deliberately narrow, single-purpose bridge
  package into something its own ADR (0047) never scoped it for — the
  same "improperly broaden an existing package" failure mode Gate A's
  package-boundary criterion exists to catch.
- **Core `Compono`:** provides `Match<T>`/`CallVerifier` (already reused
  by `Compono.Http`) and `ICompositionContext.Resolve<T>()`. A new
  `Compono.Options` package can reuse `Match<T>`/`CallVerifier` the same
  way `Compono.Http` does, without core ever knowing `Compono.Options`
  exists — consistent with the core-knows-nothing-about-integrations rule.

---

## 12a. Profiles and reusable Configuration/Options composition (New, 2026-09-07)

Investigated directly against `ICompositionProfile`/`CompositionBuilder`'s
actual current code, per the reassessment request — not assumed.

**A profile is just a named, reusable sequence of ordinary builder calls.**
`ICompositionProfile.Configure(CompositionBuilder builder)` (ADR-0018) has
no special vocabulary of its own — a profile *is* whatever builder calls
its `Configure` method makes: `UseNSubstitute()`, `UseBogus(...)`,
`Register<T>(...)`, `Share<T>()`, all identically, in any combination.
`CompositionBuilder.Register<T>` (`src/Compono/CompositionBuilder.cs`) is
fully generic — `Register<T>(Func<ICompositionContext, T>)` and
`Register<T>(Func<T>)` — with nothing type-specific about `IConfiguration`
or `IOptions<T>`.

**Direct consequence: reusable Configuration/Options setup through
profiles is already fully possible today, with zero new capability.** A
profile can already do exactly this, unchanged, right now:

```csharp
public sealed class AppTestProfile : CompositionProfile
{
    protected override void Configure(CompositionBuilder builder)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(DefaultTestValues)
            .Build();

        builder
            .Register<IConfiguration>(() => configuration)
            .Register<IOptions<MyOptions>>(() => Options.Create(new MyOptions { ... }));
    }
}
```

This directly answers the reassessment's own question ("can existing
profiles already encapsulate the raw ceremony cleanly? if yes, is the
remaining friction merely documentation/discoverability?") — **yes, and
yes**, for both Configuration and standalone `IOptions<T>`. Nothing about
profiles is currently a blocker; nothing about profiles needs new
capability to make Configuration or bare `IOptions<T>` reusable. This
directly confirms and sharpens §3/§5's conclusions: the remaining friction
there really is discoverability (knowing the idiom exists), not a
composition-model gap — and discoverability alone, per Step 2/3 of the
admission process, is a documentation problem, not a package problem, for
those two.

**Where profiles change the calculus: `Compono.Options`'s coherence
story.** A profile is exactly the right place to express "this test
suite's baseline options for `MyOptions`, consistent across `IOptions<T>`/
`IOptionsSnapshot<T>`/`IOptionsMonitor<T>`, with individual tests able to
override the value inline" — the same "express once, reuse across many
tests, vary selectively" shape ADR-0056's `Share<T>()` was justified by,
applied to Options instead of arbitrary shared values. This doesn't
require any *new* profile mechanism (profiles already generically support
whatever `Compono.Options` ends up registering, the same way they support
`UseBogus`/`UseNSubstitute` today) — but it means `Compono.Options`'s
future design should treat "read naturally both inline and inside a
profile" as a real design constraint, not an afterthought, since that's
where its coherence value is most visible in practice.

**Answering the reassessment's specific profile questions:**

- *Can existing profiles already encapsulate the ceremony cleanly?* Yes,
  for Configuration and standalone `IOptions<T>` (confirmed above,
  directly against `Register<T>`'s actual signature).
- *If yes, is remaining friction merely documentation/discoverability?*
  Yes, for those two — reinforcing §3/§5's documentation-only/no-new-capability
  conclusions with direct evidence rather than assumption.
- *Would first-class APIs make profiles more expressive/less repetitive?*
  Not for Configuration/bare `IOptions<T>` — `Register<T>` is already the
  first-class API there, and a named wrapper adds a name, not expressivity.
  For the Options-coherence capability (Monitor/Snapshot/`IOptions<T>`
  wired consistently), yes — a `Compono.Options` package earns exactly
  this kind of profile-native expressiveness gain, the same way
  `Share<T>()` did for sharing.
- *Would first-class APIs establish consistent semantics every consumer
  would otherwise reinvent?* Yes, specifically for the multi-interface
  consistency risk (§5, §6) — that's the one place "everyone reinvents
  this, slightly differently, and some get it subtly wrong" genuinely
  applies here, mirroring `Compono.Http`'s own admission rationale
  (independently-duplicated, subtly-inconsistent hand-rolled fakes).

---

## 13. Gate A: criterion-by-criterion evaluation (Reassessed 2026-09-07)

Applied to the coherent candidate that survives to this stage — consistent
`IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>` composition for a
given `T`, correctly wired to one underlying value/store (§1, §5, §6):

1. **Compono-specific value — clears, on two independent grounds.** (a)
   Real, sourced friction (§7): the standard hand-rolled fake used across
   the .NET community is demonstrably incomplete (silently broken named
   options, single-listener only, no real disposal). (b) Composition
   ergonomics in their own right, per the Reassessment above: keeping a
   consumer's related Options interfaces consistently wired to one value,
   expressible once and reusable via a profile (§12a), the same category
   of value `Share<T>()`/`Compono.Bogus` were admitted on. Ground (a) alone
   already cleared this criterion in the original research; ground (b)
   independently reinforces it and is why `IOptionsSnapshot<T>` belongs in
   scope on its own merits (§6), not as a cost-sharing add-on.
2. **Native ecosystem fit — clears.** The design vocabulary is
   Microsoft's own (`CurrentValue`, `Get(name)`, `OnChange`, named
   options) — nothing about this needs Compono-invented terminology, and
   the investigation deliberately avoided hiding named options,
   snapshot/monitor distinctions, or change semantics behind
   Compono-specific naming.
3. **Meaningful abstraction — clears, on direct evidence.** §7 shows the
   "few obvious lines" version that gets written in practice is
   measurably wrong in three independent, documented ways. This is a
   materially different bar than a trivial extension method.
4. **Architectural fit — clears, with one explicit prerequisite noted.**
   A hand-written, non-generated runtime class (test constructs and
   configures it directly, mirroring `TestHttpHandler`'s shape) needs no
   reflection, no new core extension point, and no generator work — it is
   buildable entirely on existing public surface (`Match<T>`,
   `CallVerifier`) the same way `Compono.Http` already is. **The one
   explicitly-flagged exception**: *automatically* supplying
   `IOptionsMonitor<T>`/`IOptions<T>` for an arbitrary composed `T` with
   no manual registration is blocked on ADR-0052 Finding B (§14) — a
   real, already-identified, currently-unresolved core architectural
   question this candidate's own design must not invent an answer to ad
   hoc. The clean scope for a v1 design is a class the test constructs
   explicitly and hands an already-composed (or hand-built) `T` to — see
   §14 for why this sidesteps the open gap entirely rather than working
   around it.
5. **Package-boundary justification — clears, as its own package.** §12
   shows this doesn't belong in `Compono.DependencyInjection` (wrong
   shape entirely) or core (depends on `Microsoft.Extensions.Options`,
   which core must never reference). It's substantial enough — real
   thread-safety, disposal, and named-value-store logic, not a one-line
   helper — to justify its own package, the same bar `Compono.Http`
   cleared with a comparable amount of hand-written logic.

**Maintenance/CI/docs/skill cost** (weighing factor, not a sixth
pass/fail gate): comparable to `Compono.Http`'s — one new package guide,
one new skill reference file, one new CI package-validation target. Real
but bounded and linear, per the same finding ADR-0039 already made for
this repo's existing routing pattern.

**`IOptions<T>` (standing alone) and Configuration still do not reach Gate
A independently** — not because they fail a criterion, but because Step
1/Step 2 of the admission process (concrete problem; already solved today,
including *inside* Compono's composition model via profiles, per §12a)
resolve them before Gate A's five criteria are even reached. This is a
reassessed conclusion, not a carried-over one: §3/§5/§12a explicitly
re-ran both under the sharper "stays inside the composition model"
question and confirmed this holds, rather than re-asserting the original
raw-difficulty framing.

---

## 14. The ADR-0052 Finding B prerequisite, in detail

ADR-0052's own text is explicit that Finding B — "a type reachable only
via nested `context.Resolve<T>()` inside a registration factory" — is
"the separate, still-open question, untouched by Part B," and that Part A
(the mechanism that would have covered a related but distinct case,
statically-recognized `Register<T>(...)` calls) was itself deferred
("Recommendation: ship Part B alone for now. Defer Part A... as a
separate, later" decision). As of this research, Finding B has **no
accepted resolution**.

This matters directly for Configuration/Options: the real
`alexa-vox-craft` friction that surfaced Finding B in the first place
(§13.4 of `docs/research/0010-...`) was **exactly** this pattern:

```csharp
.Register<IOptions<SmapiDeveloperAccessTokenOptions>>(context =>
    Options.Create(context.Resolve<SmapiDeveloperAccessTokenOptions>()));
```

A hypothetical automatic stage-4-6 provider that composes `IOptions<T>`/
`IOptionsMonitor<T>` for *any* requested `T` by calling
`context.Resolve<T>()` internally would hit the identical wall whenever
`T` isn't independently discovered as a root elsewhere — it would not be
a new problem, but the same open one, narrowed to Options types.

**Why the recommended `Compono.Options` design avoids this entirely,
without waiting on Finding B's resolution:** if the test constructs the
fake directly and hands it an already-composed value —

```csharp
var options = composer.Create<MyOptions>();   // ordinary root-level composition
var monitor = new TestOptionsMonitor<MyOptions>(options);   // illustrative only, not a proposed API
```

— then no nested, provider-internal `context.Resolve<T>()` ever happens.
`MyOptions` is composed the ordinary way, as a normal root or parameter,
which the generator already discovers correctly today. This is not a
workaround invented to dodge Finding B — it's the same shape
`Compono.Http`'s `TestHttpHandler` already uses (the test constructs and
configures it directly; nothing auto-resolves it into existence). **An
automatic, no-registration-needed version remains blocked on Finding B**
and is correctly scored as documentation-only/pending, not designed
around here.

---

## 15. Cost analysis

Relative to `Compono.Http`'s already-accepted cost (the closest real
precedent — a new, hand-written, non-generated runtime package):

| Cost dimension | Estimate |
|---|---|
| Public API surface | Comparable to `Compono.Http` — a handful of public types (`TestOptionsMonitor<T>`, likely a shared snapshot type or shared base), no generic explosion |
| Dependencies | `Microsoft.Extensions.Options` only (a near-universal transitive dependency already present in any project using the pattern this package tests) — no heavier than `Compono.Http`'s `Microsoft.Extensions.Http`-free design |
| Generator changes | None — no source generation involved, matching `Compono.Http` |
| Runtime machinery/allocations | A name-keyed dictionary and a multi-subscriber event list per instance — small, bounded, comparable to `TestHttpHandler`'s registration list |
| Concurrency/lifecycle complexity | Real, and the crux of doing this correctly (§7) — thread-safe multi-subscriber notification and per-subscriber disposal are non-trivial but well-understood (the real `OptionsMonitor<T>` source is the reference implementation to match against) |
| Native AOT/trimming | Clean — no reflection, no dynamic code, same posture as every other Compono package |
| Documentation | One new package guide, one new Concept/Cookbook entry — comparable to `Compono.Http`/`Compono.Logging`'s launch cost |
| Skill/eval maintenance | One new package reference file in the `compono` agent skill's detection table — linear, per ADR-0039's own finding |
| Compatibility commitments | A public contract for `IOptionsMonitor<T>`/`IOptionsSnapshot<T>` test-double behavior, once shipped, is a real long-term API surface — same commitment level as any other shipped package |

**Overall: comparable to `Compono.Http`**, not a larger undertaking — the
evidence bar this candidate needed to clear (§7's documented,
citation-backed correctness gaps in existing practice) is proportionally
strong for a cost this size.

---

## 16. Capability-slice classification (Reassessed 2026-09-07)

Using ADR-0029's five-way classification, applied per slice rather than
once:

- **Consistent `IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>`
  composition for a given `T`** — clears Gate A (§13) and Gate B (§19,
  explicit product-owner request) → **Roadmap item.** (Reassessed:
  previously scoped as "Monitor/Snapshot," now scoped as the coherent
  three-interface capability per §1/§5/§6/§12a.)
- **`IOptions<T>` composition/mocking, standing entirely alone (no
  Monitor/Snapshot need in the same SUT)** — **Acceptable Compono-native
  alternative, already shipped.** No new ADR/Amendment needed; the
  existing pattern (`Compono.TestDoubles`'s generated double, plus
  `Register<IOptions<T>>(() => Options.Create(...))` for the composed-value
  case, both already reusable through profiles with zero new capability
  per §12a) is already pleasant and already documented in the migration
  guide precedent research (§5).
- **Automatic no-registration `IOptions<T>`/`IOptionsMonitor<T>`
  composition for arbitrary `T`** — **Documentation-only, pending a
  prerequisite core decision** (ADR-0052 Finding B). Recorded here, not
  designed around — the correct future path is Finding B getting its own
  resolution first, the same restraint ADR-0039 already applied to
  `Compono.DependencyInjection`'s "richer" idea pending its own
  prerequisite core concept.
- **`IConfiguration`/`ConfigurationBuilder` composition** —
  **Documentation-only.** Reassessed under the sharper ergonomics question
  (§3, §12a) and confirmed, not merely re-asserted: no capability gap,
  including inside profiles; a Cookbook recipe is the correct and
  sufficient artifact.
- **Options validation (`IValidateOptions<T>`, DataAnnotations, the
  validation source generator)** — **Rejected.** Not a testing/composition
  problem; a production concern .NET already solves, including with its
  own source generator where AOT matters.

---

## 17. Package-boundary analysis (Reassessed 2026-09-07 — conclusion unchanged, reasoning strengthened)

Working down the admission process's placement ladder (§"Step 6"):

1. **Core `Compono`** — ruled out. Core must never reference
   `Microsoft.Extensions.Options`, matching the same rule that keeps every
   other integration (`Compono.NSubstitute`, `Compono.Bogus`,
   `Compono.Http`, `Compono.Logging`) out of core.
2. **An existing extension package** — ruled out for
   `Compono.DependencyInjection` specifically (§12: wrong shape, pull-only
   forwarding vs. new construction logic). No other existing package's
   ecosystem overlaps (`Compono.Http` is HTTP-specific; `Compono.Logging`
   is `ILogger`-specific).
3. **A new extension package — `Compono.Options`.** Justified on two
   independent grounds now, not one: the correctness work (§7) remains
   substantial enough to be its own independently-consumed artifact,
   matching `Compono.Http`'s precedent almost exactly in shape and scale
   — **and** the package is the right home for the multi-interface
   consistency value identified in §5/§6/§12a (a consumer getting
   `IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>` from one
   coherently-wired source). Combining a package and its own ergonomic
   front door is exactly the shape ADR-0056's `Share<T>()` and
   `Compono.Bogus` already established. **`Compono.Configuration` should
   still not be created** — reassessed, not re-asserted (§3): nothing in
   this investigation, including under the sharper ergonomics question,
   justifies it as a package; the one real Configuration-adjacent idea is
   fully satisfied by a documentation recipe.
4. **Documentation/sample guidance only** — the correct outcome for both
   `IConfiguration`/`ConfigurationBuilder` composition (§3) and the
   automatic no-registration Options composition idea (§14), pending its
   own prerequisite.

**Recommended package identity:**

```
Compono.Options
    -> Compono   (reuses Match<T>, CallVerifier if verification is added — no generator dependency)
```

matching `Compono.Http`'s own dependency graph shape exactly.

---

## 18. Recommended admission outcome (Reassessed 2026-09-07)

- **`Compono.Options`** — **Roadmap item.** Scoped as the coherent
  `IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>` consistency
  capability (§1), not Monitor/Snapshot alone. Gate A cleared (§13), Gate B
  cleared by the clarified explicit product-owner request (§19). Needs its
  own problem-focused `Proposed` ADR next (per `tasks/design.md`), not
  designed here — the problem statement that ADR should open with is
  restated precisely in item 17 of the Final report below.
- **`IOptions<T>` composition/mocking** — **No action needed.** Already an
  acceptable Compono-native alternative; worth a Cookbook entry
  cross-referencing `Compono.TestDoubles`'s existing `IOptions<T>` support
  once `Compono.Options` ships, so a reader lands on the right answer for
  each of the two related-but-distinct problems.
- **`Compono.Configuration`** — **Rejected as a package; documentation-only
  as an idea.** Record in a future `future-packages.md` update (deferred
  to the actual design/roadmap step, per this investigation's scope
  limits) as a Cookbook recipe candidate, not a package candidate.
- **Options validation** — **Rejected outright.** Not recorded as a
  documentation-only idea even — it's out of scope on "Compono-specific
  value" grounds, not a near-miss.

---

## 19. Gate B evaluation (Reassessed 2026-09-07)

Per `docs/architecture/capability-admission.md` Step 4, Gate B accepts
"an explicit product-owner request" as legitimate evidence on its own —
the same mechanism that already cleared `Compono.TUnit`, `Compono.NUnit`,
and Compono-owned source-generated test doubles, none of which had
dogfooding evidence at admission time either.

**The request itself is now more precise** (2026-09-07): "I want Compono
to make common .NET Configuration and Options dependencies easier and more
discoverable to compose in tests, provided the resulting capability is
coherent, composition-native, architecturally sound, and meaningfully
better than simply documenting Microsoft's APIs" — explicitly conditioned
on Gate A clearing independently, exactly as the original request was.

**Applied here:** Gate B is satisfied for `Compono.Options`, scoped as the
coherent three-interface consistency capability (§1, §13). It is **not**
separately claimed for `Compono.Configuration` (reassessed and still no
Gate A clearance to apply it to, §3) or for the automatic-composition idea
(still blocked at Gate A's architectural-fit criterion, pending Finding
B, §14) — the clarified request does not change either verdict, because
neither cleared Gate A in the first place, and Gate B evidence cannot
substitute for a Gate A criterion that didn't clear (per
`capability-admission.md`'s own ordering: both gates must clear, in that
order).

**Where future dogfooding would still be valuable**, despite Gate B
already being satisfied: real dogfooding against `alexa-vox-craft` or
`cosmere-tracker` (both already have real `IOptions<T>` usage, per §5)
would validate the *design* once a `Compono.Options` ADR exists — in
particular, whether the recommended "test constructs and hands over an
already-composed value" shape (§14) actually reads well against a real
consumer's test suite, and whether `IOptionsSnapshot<T>` bundled in ends
up used at all in practice. This is design validation, not admission
evidence — Gate B is already closed.

---

## 20. Rejected/deferred ideas

- **`Compono.Configuration`** — rejected as a package (§17); the
  underlying idea survives only as a documentation-only Cookbook
  candidate.
- **Options validation support** — rejected outright (§10, §16); not a
  testing/composition concern.
- **Automatic, no-registration `IOptions<T>`/`IOptionsMonitor<T>`
  composition for arbitrary `T`** — deferred, pending ADR-0052 Finding B's
  own resolution (§14, §16). Not designed around; not silently dropped —
  recorded here with its exact blocker so it isn't rediscovered from
  scratch.
- **Simulating real Configuration-driven reload** (file-system change
  tokens, `IConfigurationRoot.Reload()`) inside a Compono test double —
  considered and rejected in favor of a direct `.Set(...)`-shaped push,
  per §9/§11 — modeling the full production reload pipeline inside a test
  double would be exactly the "reflection-heavy, feature-complete
  wrapper" `design-principles.md` warns against, for a scenario a
  deterministic direct push already serves better.

---

## 21. Open questions genuinely requiring design work (not more research) (Reassessed 2026-09-07 — two questions added)

These belong in the `Compono.Options` ADR's own design pass, per
`tasks/design.md`'s deep-dive process — this document deliberately does
not answer them:

- **(New)** What the single, coherent entry point for "this `T`'s Options
  composition" looks like — one call that consistently wires
  `IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>` to the same
  underlying value, versus three separate registrations a consumer must
  remember to keep in sync themselves. This is the design question the
  reassessment's coherence finding (§5, §6) most directly feeds.
- **(New)** How this reads both inline and inside a profile (§12a) — a
  profile can already hold whatever this design produces with zero new
  profile-mechanism work, but the design pass should still verify the
  resulting shape is genuinely pleasant in both contexts, not just
  inline, since profile reuse is where §12a found the strongest
  ergonomic payoff.
- Exact public shape: a single `TestOptionsMonitor<T>` class implementing
  both `IOptionsMonitor<T>` and `IOptionsSnapshot<T>`, or two related
  types? (§6 found their implementations nearly identical, but that's an
  implementation observation, not a public-API decision.)
- How a test supplies/changes named-option values — a dictionary-style
  indexer, a builder, or named overloads of a `.Set(...)`-shaped method?
- Whether/how this composes automatically as a constructor parameter via
  `[Shared]`/registration for the common case, versus always being
  constructed explicitly by the test (§14's recommended default) — and if
  automatic composition is offered at all pre-Finding-B, what its exact,
  narrower boundary is (e.g., only when `T` is already independently
  composed elsewhere, detectable or not).
- Whether `CallVerifier` reuse (verifying an `OnChange` subscription fired
  N times) is in v1 scope or a later addition, mirroring
  `Compono.Http`/`Compono.TestDoubles`'s own verification surface.
- Thread-safety implementation approach (lock-based like
  `Compono.DependencyInjection`'s adapter, or a lock-free
  `ConcurrentDictionary`-based approach like the real
  `OptionsMonitorCache<T>`).
- Whether disposal of the fake itself needs to be modeled (real
  `OptionsMonitor<T>.Dispose()` unregisters change-token subscriptions;
  a Compono fake with no real change tokens may not need an equivalent
  disposal contract at all — needs an explicit decision, not a default).

---

## 22. Sources and experiment results

**Primary sources fetched directly, not recalled from memory:**

- [Options pattern - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
  (updated 2025-10-22, refreshed page metadata 2026-05-15) — current
  `IOptions`/`IOptionsSnapshot`/`IOptionsMonitor`/`IOptionsFactory`/
  `IOptionsMonitorCache`/`IOptionsChangeTokenSource` semantics, named
  options, validation (`IValidateOptions`, DataAnnotations,
  `ValidateOnStart`, `[ValidateObjectMembers]`/`[ValidateEnumeratedItems]`).
- [dotnet/runtime `OptionsMonitor.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Options/src/OptionsMonitor.cs) —
  constructor signature, `CurrentValue`/`Get`/`OnChange`/`InvokeChanged`/
  `Dispose` implementation, confirming §7's behavioral claims directly
  against source rather than documentation prose.
- [Compile-time configuration source generation - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-generator) —
  `EnableConfigurationBindingGenerator`, automatic activation under
  `PublishAot`, confirming §3's "already source-generation-first" finding.
- [Testing IOptionsMonitor - Ben Foster](https://benfoster.io/blog/20200610-testing-ioptionsmonitor/) —
  the widely-cited hand-rolled `TestOptionsMonitor<T>` pattern quoted
  verbatim in §7, including the article's own caveat about incomplete
  disposal semantics.
- Corroborating hand-rolled-fake pattern sightings:
  [code-maze](https://code-maze.com/csharp-mock-ioptions/),
  [thecodebuzz](https://thecodebuzz.com/unit-test-mock-ioption-net-core-ioption-moq-appsettings/),
  [mitch.codes](https://mitch.codes/net-core-manually-instantiating-ioptions-for-unit-testing/) —
  used only to confirm the pattern recurs across independent sources, not
  quoted individually.

**Repository sources inspected directly (not assumed from names):**

- `src/Compono.DependencyInjection/ComponoServiceProvider.cs`,
  `CompositionRowServiceProviderExtensions.cs` — confirmed the
  pull-only, no-construction-logic shape described in §12.
- `src/Compono.Logging/CompositionBuilderExtensions.cs`,
  `LoggingProvider.cs` — the stage-6 `ICompositionValueProvider` shape,
  used as a precedent for what an *automatic* provider would look like
  (and why that shape hits Finding B for Options specifically, §14).
- `src/Compono.Http/TestHttpHandler.cs` — the hand-written,
  directly-constructed, non-generated runtime-class precedent §7/§14's
  recommended `Compono.Options` shape follows.
- `src/Compono/ICompositionContext.cs` — confirmed `Resolve<TValue>()`'s
  actual contract (only valid inside an active registration/provider
  invocation), relevant to why Finding B's nested-resolve gap is a real
  constraint and not a hypothetical one.
- `docs/adr/0052-compile-time-composition-discovery-boundary-for-registered-and-nested-resolved-types.md` —
  confirmed Finding B's status as still-open/untouched by Part B directly
  from the ADR's own text, not inferred from `docs/roadmap/post-mvp.md`'s
  summary alone.
- `docs/research/0001-autofixture-comparison.md`,
  `0004-lightsaber-skill-testdoubles-v2-dogfood.md`,
  `0005-lightsaber-skill-testdoubles-v2-third-dogfood.md`,
  `0010-alexa-vox-craft-compono-ecosystem-migration.md`,
  `0011-alexa-vox-craft-mediatr-tests-testkit-migration-slice-1.md` — every
  real `IOptions<T>` sighting cited in §5, including the exact Finding B
  reproduction in §14.

**Additional sources inspected for the 2026-09-07 reassessment:**

- `docs/adr/0056-composition-builder-share-graph-wide-sharing.md`'s
  Context — confirmed directly that `Share<T>()` was admitted despite
  `[Shared]` already making sharing fully possible, on ergonomics/
  composition-configuration grounds, the precedent cited in the
  Reassessment section and §13.
- `docs/adr/0027-compono-bogus-package-design.md`'s Context — confirmed
  `Compono.Bogus`'s own admission framing ("just call `UseBogus()`"),
  cited as the second precedent for ergonomics-as-value independent of
  raw difficulty.
- `docs/adr/0018-composition-profiles.md` and
  `src/Compono/CompositionBuilder.cs` (`Register<T>`'s two generic
  overloads) — confirmed directly, not assumed, that profiles already
  generically support any builder-level registration with no
  Configuration/Options-specific capability gap (§12a).

**No focused code experiments were run.** Every behavioral claim about
`OptionsMonitor<T>` (§7) was verifiable directly against dotnet/runtime's
own source, and every claim about Compono's existing behavior (§5, §12,
§14) was verifiable directly against this repo's own source and prior
dogfooding research — neither required a disposable spike to confirm. If
the future `Compono.Options` ADR's design pass needs empirical
confirmation of a specific concurrency scenario (e.g., two threads calling
`OnChange`/`.Set(...)` concurrently against a candidate implementation),
that belongs in that design pass, per §21.
