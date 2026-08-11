# [RESEARCH-0003] `Exception`'s Ambiguous Constructor: `structured-logging` Migration Evidence

**Status:** Done (single finding, classified, no further action)

**Feeds:** [ADR-0002](../adr/0002-constructor-selection-algorithm.md)

This document is the evidence record for one finding surfaced by
[`structured-logging` PR #57](https://github.com/LayeredCraft/structured-logging/pull/57)
(merged 2026-08-10, `36af03b`), an AutoFixture→Compono test migration in
that repository. Unlike [RESEARCH-0001](0001-autofixture-comparison.md)
(a full Milestone-7 dogfooding pass) or
[RESEARCH-0002](0002-trivia-platform-comparison.md) (a pre-migration
survey), this is a single-finding record from a real migration performed
outside a formal Milestone dogfooding pass — reusing
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
rubric/five-way classification framework per `design-decisions.md`'s
`docs/research/` convention, scoped to the one finding that migration
produced rather than a full capability survey.

## Finding: `CMP0001` on `System.Exception`

`structured-logging`'s test migration hit `CMP0001` composing
`System.Exception` directly (3 accessible constructors, correctly
reported `AmbiguousConstructor` per
[ADR-0002](../adr/0002-constructor-selection-algorithm.md)'s rule) across
61 theories.

Applying ADR-0029's gap decision rubric:

1. **Observed frequency.** 61 real theory call sites across the migrated
   test kit (exact count from the PR diff, not an estimate) — materially
   higher than [ADR-0002 Amendment 1](../adr/0002-constructor-selection-algorithm.md#amendment-1-2026-08-04-cmp0001-observed-against-a-real-ambiguous-bcl-type-no-change-made)'s
   `HttpClient` finding, which had zero real pre-migration call sites.
2. **Intended to work?** No — `Exception`, like `HttpClient`, is a BCL
   type ADR-0002's originally-anticipated `[CompositionConstructor]`
   attribute was never going to close (the type's author can't be asked
   to annotate a BCL constructor). Not a bug.
3. **Workaround cost.** Low, confirmed by the real diff. Every one of the
   61 call sites made the same one-line change — the parameter changes
   from `Exception exception` to `string exceptionMessage`, and Arrange
   gains one line:

   ```csharp
   // Before
   [Theory]
   [AutoNSubstituteData]
   public void Critical_WithMessageAndException_LogsAtCriticalLevelWithException(
       string message,
       Exception exception)
   {
       // Arrange
       var testLogger = new TestLogger();
       ...
   }

   // After
   [Theory]
   [Compose]
   public void Critical_WithMessageAndException_LogsAtCriticalLevelWithException(
       string message,
       string exceptionMessage)
   {
       // Arrange
       var exception = new Exception(exceptionMessage);
       var testLogger = new TestLogger();
       ...
   }
   ```

   Preserves randomized-message behavior, costs one line per call site,
   no readability loss — verified against all 61 occurrences in the PR
   diff, not sampled.
4. **Principle alignment.** Neutral, not blocking. An explicit,
   consumer-configured disambiguation mechanism for a registered/external
   type — the kind Amendment 1 flagged as a plausible future roadmap
   item, distinct from the guessing-based "greedy" option ADR-0002's
   Decision Outcome already rejected — would stay deterministic and
   wouldn't conflict with ADR-0002's principle. Nothing here rules a
   general mechanism out on principle; the classification below rests on
   question 3's low observed cost, not on a claim that no deterministic
   mechanism could exist.

## Classification

**Acceptable Compono-native alternative.** Per ADR-0029's classification
3 ("a different API than AutoFixture's, but the replacement remains
pleasant — low workaround cost, no material readability loss"): low cost
plus no principle conflict (question 4) is that classification's own
definition, not classification 4 ("Intentional design difference," which
needs a genuine principle conflict or disproportionate complexity).
Per ADR-0029, this classification needs no ADR or Amendment — it's
recorded here and in the migration guide, not as a governing-ADR
Amendment:

- [Migration guide: "Known differences and limitations"](../migrating-from-autofixture.md#known-differences-and-limitations)
  now documents `Exception`'s workaround alongside `HttpClient`'s.
- `skills/compono/SKILL.md`'s "When not to use Compono" section records
  the practitioner-facing pattern for an agent working in a Compono
  project.

This finding doesn't reopen or change
[ADR-0002](../adr/0002-constructor-selection-algorithm.md)'s decision —
it's the second real occurrence of the same evidence pattern Amendment 1
recorded, and it reinforces rather than revisits that Amendment's "no
change" verdict: generic registered/external disambiguation remains a
plausible future roadmap item per Amendment 1, not a rejected one, but
neither real occurrence yet crosses the cost bar that would justify
designing it now. The two workarounds aren't the same shape, though —
Amendment 1's `HttpClient` case used a real interface wrapper
(`IHttpClientProvider`, a provider-resolved leaf), while this `Exception`
case hand-constructs directly from a composed `string` — both are
legitimate answers to the same guardrail rule ("wrap in an app-owned
interface/factory, or construct it directly by hand"), not evidence the
workaround has converged on one specific mechanism.

## Decisions

- **This finding** → no ADR or Amendment. Classified "Acceptable
  Compono-native alternative" per ADR-0029, which explicitly needs no ADR
  action — recorded here and in the migration guide instead (see
  "Classification" above for the required artifacts).
  [ADR-0002](../adr/0002-constructor-selection-algorithm.md) itself is
  unchanged: no new Amendment was added for this finding, and its
  Amendment 1 (the `HttpClient` finding) stands as previously recorded.
