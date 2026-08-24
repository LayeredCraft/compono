# RESEARCH-0008: trivia-platform multi-entry Configure() dogfood — evidence, design, and validation workflow

Status: draft, pre-implementation. Companion to ADR-0048 (argument matching) and ADR-0049
(closed-instantiation configuration). Produced from a `trivia-platform` dogfood pass at
`Compono*` `0.7.0-preview.81`, branch `chore/compono-testdoubles-migration` (uncommitted
working tree).

Decision already made by product owner: **multi-entry, argument-distinguished response
configuration per member is a required capability**, not a gated one. This document is not
"should we build it" — it's the evidence inventory, the design, and the validation workflow.
No implementation has started.

---

## 1. Evidence inventory (trivia-platform)

All paths relative to `/Users/ncipollina/source/repos/ncipollina/trivia-platform`.

### 1.1 `LeaderboardServiceTests.cs` — silent overwrite, currently masked, live in the tree

`ILeaderboardRepository.GetLeaderboardEntryAsync(userId, period, type, ct)`, via
`[Shared] ILeaderboardRepository leaderboardRepository`, 5 test methods register the member
twice per test — Weekly args first, AllTime args second:

```csharp
leaderboardRepository.Configure().GetLeaderboardEntryAsync(currentPlayer.UserId, period,
        LeaderboardType.Weekly, Match.Any<CancellationToken>())
    .Returns(Task.FromResult<LeaderboardEntry?>(weeklyEntry));
leaderboardRepository.Configure().GetLeaderboardEntryAsync(currentPlayer.UserId,
        LeaderboardPeriods.AllTime, LeaderboardType.AllTime, Match.Any<CancellationToken>())
    .Returns(Task.FromResult<LeaderboardEntry?>(null));
```

Confirmed by an isolated probe against the pinned package (`Compono.TestDoubles
0.7.0-preview.81`, not documentation): the second `Configure()` call replaces both the matcher
*and* the return value of the first. Calls matching the first (discarded) matcher fall through
to the member's computed default.

Consequence, traced into production code (`LeaderboardService.BuildPlayerStatsAsync`): in
`ZeroScoreProjection_DoesNotQueryRank` and `NegativeRankResult_OmitsRank`, the Weekly stub is
silently lost, so `GetLeaderboardEntryAsync` returns `null` for the Weekly call. Both tests take
the `entry is null` early-return branch instead of the branch they were written to cover
(`entry.Score > 0` guard / rank-clamping). Both **currently pass** — the assertions in each test
don't happen to check the field that would expose the divergence. This is a false-pass, not a
hypothetical: `dotnet test` on the current working tree shows 17/17 green in this file, with two
of them exercising the wrong code path. Confirmed order-dependent: whichever registration is
textually last wins; the other 3 tests in the file happen to configure the same value (`null`)
on both branches, so the bug has no observable effect there — coincidental, not designed.

This is the **primary, sufficient acceptance case**: same member, same test, two disjoint
literal-argument-keyed entries, divergent non-default return values, currently unrepresentable
without either a hand-rolled fake or silent breakage.

### 1.2 `CachedLeaderboardRepositoryTests.cs` — reverted to NSubstitute

`ILeaderboardRepository.RetrieveTopEntriesAsync(type, period, count, ct)`, three tests, each
registering the same member twice with disjoint literal args and distinct non-null return
values (differing period / differing type / differing count). Currently backed by raw
NSubstitute (`Arg.Any<CancellationToken>()` + `.Returns(...)`, real per-signature stub table)
via a profile-level exception in `LeaderboardRepositoryProfile.cs`. This is a second, independent
occurrence of the same shape as 1.1, on a different member of the same interface.

### 1.3 `MultiStubLeaderboardRepository` — hand-written fake, deliberate workaround

`test/.../Modules.Leaderboard.Tests/TestKit/MultiStubLeaderboardRepository.cs`, a 46-line
hand-implemented `ILeaderboardRepository` backed by two `Dictionary<(key tuple), value>` tables
(`StubEntry`/`StubRank`), used by
`LeaderboardServiceTests.RetrieveCurrentPlayerStatsAsync_LoadsWeeklyAndAllTimeStats`. Genuinely
divergent responses (two different `LeaderboardEntry`s, ranks 3 vs 7) for two disjoint
`(LeaderboardType, period)` keys on the same member, in the same test. Bypasses `[Compose]`
entirely — hand-constructed, not resolved. This is the third occurrence of the same shape.

### 1.4 What is *not* this problem (ruled out during the inventory, don't fold in)

- **`CallOrderRecordingLeaderboardService`** (`ILeaderboardService`) — asserts ordering between
  two *different* members (`SaveLeaderboardState` before `RetrieveCurrentPlayerStatsAsync`), not
  divergent responses from one member. Confirmed genuinely distinct; the repo's own doc comment
  on the class already draws this line. Its own referenced doc
  (`docs/gaps/leaderboard-call-order-verification.md`) doesn't exist in trivia-platform — broken
  link, worth telling the trivia-platform team about separately, not a Compono action.
- **Different closed generic instantiations of the same generic member** (e.g.
  `GetContextDataAsync<ConversationContext>(...)` and `GetContextDataAsync<UserContextBase>(...)`
  configured in the same test) — confirmed by probe to already work correctly today via
  ADR-0049's per-closed-`T` bucket. Not broken, not in scope.
- **`localizer.GetString(Arg.Any<string>()).Returns(x => x.ArgAt<string>(0))`** (several files) —
  a `Returns(Func<...>)` callback/echo, a different gap (no callback-response hook in
  `ReturnConfigBuilder<T>.Returns(T)` at all). Explicitly out of scope per the "narrowly focused"
  instruction.
- **`UpsellEligibilityRequestInterceptorTests.cs`'s `contextManager`** — its two-closed-generic
  usage is actually safe under Compono today (see above); its NSubstitute retention is for a
  different, already-documented reason (`SetContextDataAsync<T>`'s open-generic parameter, plus
  possibly overload/verification gaps) — worth re-justifying in trivia-platform separately, not a
  multi-entry case.
- No other file in trivia-platform has same-member/same-test-method `Configure()` duplication —
  confirmed by a repo-wide, brace-aware scan of all 150 `.Configure().Member(` call sites, grouped
  per test method (file-level grouping was checked and rejected as too coarse — no test class in
  the repo shares a double instance across tests via a fixture, so per-method is the correct
  grain).

**Net evidence: 3 real occurrences, all on `ILeaderboardRepository`/`ILeaderboardService`-shaped
members, all disjoint-literal-argument, all non-overlapping matchers, all order-sensitive under
today's last-write-wins field replacement.** No occurrence in the inventory needs overlapping
matchers (e.g. a `Match.Any` catch-all plus a specific override) — that shape is the design's
target ergonomic (per the "last matching registration wins" precedence, `Get(42)` overriding a
prior `Get(Match.Any<int>())`), but it isn't yet evidenced in trivia-platform. It's a reasonable
generalization of the disjoint case, not a leap unsupported by evidence — worth building since
"one matcher slot per entry, evaluated in an explicit order" gives it for free, but not something
to over-invest in tuning beyond what falls out naturally.

---

## 2. Current single-slot representation (why this doesn't already work)

Full detail in the generator, in three places, all needing to change:

**Runtime type** (`src/Compono/ReturnConfig.cs`, `ReturnConfigBuilder.cs`): `ReturnConfig<T>` is
a mutable struct — `bool HasValue; T? Value; Exception? Exception; int CallCount`. Exactly one
configured response lives in it at a time. `ReturnConfigBuilder<T>` is a `readonly ref struct`
wrapping `ref ReturnConfig<T> _slot` — `Returns`/`Throws` write directly into that one field.
This `ref`-to-one-field shape is *why* the design is single-slot: there's structurally nowhere
else for a second call to write.

**Generated state** (`TestDouble.scriban`): three mutually exclusive shapes today, per ADR-0043/
0044/0045 (plain/overloaded), ADR-0048 (matching-eligible), ADR-0049 (closed-instantiation).
Matching-eligible members (ADR-0048, the ones this problem lives on) get exactly:
```csharp
internal ReturnConfig<TSlot> __Member;
internal Match<TParam>? __Member_m_{param};   // one per parameter, unconditionally overwritten
internal readonly List<(...)> __Member_calls = [];
```
One slot, one matcher set per parameter. `Configure()`'s own doc comment already narrates the
consequence: a second `Configure()` call has to null out the old matchers "to make 'the second
Configure() call overwrites' true" — the overwrite is intentional, documented v1/v2 behavior,
not an oversight.

**Analyzer model** (`TestDoubleAnalyzer.cs` / `TestDoubleMemberInfo.cs`): exactly one
`SlotTypeFullyQualifiedName` per member (or per overload, or per closed `T` under ADR-0049) —
nothing in the record models "a list of matcher+response entries."

**ADR-0048 already considered and rejected this exact shape**, explicitly citing lack of
evidence: *"Ordered, append-only response chain, last-match-wins... unevidenced, and introduces
a real new failure mode a single-slot design doesn't have."* That evidence now exists (§1). This
document supersedes that specific call in ADR-0048 with new evidence-backed decisions; it does
not reopen anything else ADR-0048 decided (overload-exclusion of matching, matcher/call-log field
separation, stateless `CallVerifier`).

The one place today that already holds >1 simultaneous configuration per member is ADR-0049's
`Dictionary<Type, object>` bucket, keyed by closed generic type `T`. That's a different axis
(type identity, not argument value) but it's a useful existing precedent for "per-member,
keyed, multi-entry storage generated and dispatched safely" — the new design reuses its shape
conceptually (see §3.3) rather than inventing an unrelated mechanism.

---

## 3. Design

### 3.1 Response-selection semantics: last-matching-registration-wins

Adopting the product owner's stated preference, and it's the right call for three independent
reasons, not just deference:

1. **Matches every piece of evidence in §1.** All three real occurrences are disjoint-matcher
   cases where order didn't functionally matter for correctness — but the *bug* they're
   currently hitting (silent field overwrite) already behaves like "last wins" for the matcher
   and value together; giving that semantics a defined, safe multi-entry implementation instead
   of an accidental single-field collision is a direct, minimal fix in spirit.
2. **No matcher-specificity ranking magic.** "Most specific wins" requires a partial order over
   `Match<T>` values (equality vs. predicate vs. `Any`) that doesn't exist today and would need
   its own design/ADR to justify — not evidenced as needed here, and explicitly rejected by the
   product owner.
3. **Cheap, predictable override idiom**, matching the example given:
   ```csharp
   repo.Configure().Get(Match.Any<int>()).Returns(defaultValue);
   repo.Configure().Get(42).Returns(specialValue);
   // Get(42) -> specialValue (last matching entry wins)
   ```
   Implemented naturally by walking entries in reverse registration order and returning the
   first match — no comparison between matchers needed at all, just registration order, which
   the generated code already has for free (append order = registration order).

`Configure()`'s **first call after double construction still behaves exactly as today** — one
entry, no consumer-visible change for the overwhelmingly common single-`Configure()`-per-member
case. Only a *second* `Configure()` call on the same member changes behavior: today it silently
replaces; after this change it accumulates as a second entry, with the newer entry's matcher
checked first (reverse order) but yielding to the older entry when it doesn't match. This is the
one explicit **pre-1.0 semantic correction** in this design — flagged prominently in the ADR and
in release notes for the version that ships it. No consumer source change is required either way;
consumers that only ever call `Configure()` once per member (the overwhelming majority, per §1's
"no other file in the repo" finding) see zero behavioral difference.

No-match-found behavior is unchanged: falls through to ADR-0045's existing "configuration
required" throw or computed default, exactly as today — no new failure model invented.

### 3.2 Verification stays untouched

`Verify()`/`CallVerifier`/the call-record `List<(...)>` are completely orthogonal to response
selection and need zero changes. `CallVerifier` is already stateless count-only; the call log
already records every invocation regardless of which (if any) entry answered it. Multiple
response entries share one call log per member, exactly as today — "what was called" stays
independent from "what should it return," per the product owner's explicit instruction not to
fork call histories per response entry.

### 3.3 Generated storage: reuse ADR-0049's bucket shape, generalize its key

Today, matching-eligible members (ADR-0048) get:
```csharp
internal ReturnConfig<TSlot> __Member;
internal Match<TParam1>? __Member_m_p1;
internal Match<TParam2>? __Member_m_p2;
```

Proposed replacement — an ordered list of entries, each entry bundling its own matcher set with
its own `ReturnConfig<TSlot>`:

```csharp
internal readonly struct __Member_Entry
{
    internal readonly Match<TParam1>? Matcher_p1;
    internal readonly Match<TParam2>? Matcher_p2;
    internal ReturnConfig<TSlot> Config;
}
internal readonly List<__Member_Entry> __Member_entries = [];
```

`Configure()` appends a new `__Member_Entry` (matchers from the call site, empty
`ReturnConfig<TSlot>` handed back via a builder) instead of overwriting the one field. Dispatch
walks `__Member_entries` **in reverse** (`for (int i = entries.Count - 1; i >= 0; i--)`), checking
each entry's full matcher set against the actual call args exactly as today's single `matches`
boolean does per-entry, and returns/throws on the first entry whose `Config.HasConfiguredValue ||
Config.HasConfiguredException` is true and whose matchers all pass. No match across all entries
→ existing ADR-0045 fallback.

This is a strict generalization of the current shape (a list of length ≤ 1 behaves identically
to today's single field, byte-for-byte in observable behavior), which is what makes it safe as a
pre-1.0 correction rather than a breaking rewrite: the zero-or-one-`Configure()`-call path is
unchanged in outcome, only in internal representation.

**Why a `List<T>` and not `Dictionary<Type, object>` (ADR-0049's shape):** ADR-0049's bucket is
keyed by closed generic type, which has a natural, cheap equality/hash (`typeof(T)`). Here the
key is a *matcher combination*, which has no cheap hash and whose match semantics
(`Match.Any`/`Match.Is`/equality) are not simple equality — a `Dictionary` buys nothing and would
force either boxing the matcher tuple or inventing a synthetic hash, both against the "no
reflection, allocation-conscious, no premature micro-optimization" constraints. A flat list with
linear reverse-scan is the smallest correct representation; per-member entry counts in the
evidenced cases are 2 (never more), so O(n) scan cost is a non-issue in practice, and no consumer
is expected to register dozens of entries on one member — if that ever shows up as real evidence,
it's a follow-up, not something to pre-optimize for now.

**Runtime types needed:**
- `ReturnConfig<T>` — unchanged, reused per-entry exactly as today.
- `ReturnConfigBuilder<T>` — needs a new construction path that targets an *appended* entry's
  `Config` field rather than an existing `ref` to a pre-declared field. Since `List<T>`'s elements
  aren't stably addressable by `ref` after further appends (resize invalidates prior `ref`s), the
  entry struct's `Config` field can't be handed out as a live `ref` the way the single-field case
  does today. Options: (a) make `__Member_Entry` a reference type (small class) so `Config` is
  reachable indirectly and stable across appends — cheap, one small alloc per `Configure()` call,
  consistent with "allocation-conscious, not zero-allocation-obsessed"; or (b) keep `__Member_Entry`
  a struct but have `ReturnConfigBuilder<T>` capture `(List<Entry> list, int index)` instead of a
  `ref`, indexing back in on `Returns`/`Throws`. (b) avoids the per-entry allocation but adds a
  small indirection; recommend (b) — it keeps the "no incidental heap allocation" property ADR-0043
  cared about, at the cost of `ReturnConfigBuilder<T>` gaining a second constructor shape (`ref`
  for single-slot members that don't need this, list+index for multi-entry members). Needs a small
  prototype before locking this in — flagged as the one open implementation question, not a
  blocking design gap.
- `Match<T>` — unchanged, reused as-is.
- `CallVerifier` — unchanged.

**Analyzer/model changes** (`TestDoubleMemberInfo.cs`): add nothing conceptually new to the
member's eligibility flags (`IsEligibleForMatching` still gates this shape exactly as it does
today) — the change is confined to what the *template* emits for that same eligibility bucket.
`TestDoubleAnalyzer.cs`'s extensive name-collision reservation machinery needs one addition: the
new nested entry type/list field names (`__Member_entries`, `__Member_Entry`) need the same
CS0694-avoidance treatment already given to `__Member`/`__Member_m_{param}`.

**ADR-0049's closed-instantiation members** (generic-return, per-closed-`T` bucket): out of
scope for this change. Their nested `_State<T>` class already has its own single `Config` field
plus matcher fields, structurally identical to today's ADR-0048 shape *within* one closed `T`'s
bucket — if evidence later shows the same multi-entry need *within* one closed `T`, the same
`__Member_Entry`-list technique applies there too, but nothing in §1's evidence needs that today,
so it's explicitly deferred, not designed here.

### 3.4 Compatibility with existing ADRs

- **ADR-0043**: "no dictionary, no boxing, no reflection" — preserved; this design adds a
  `List<T>`, not a dictionary, and no boxing/reflection appears anywhere in it.
- **ADR-0044**: overloaded members are untouched — this shape only applies to `IsEligibleForMatching`
  members, which are by definition non-overloaded (ADR-0048's own restriction, unaffected here).
- **ADR-0045**: "configuration required" fallback semantics unchanged — still the last resort
  when no entry matches.
- **ADR-0048**: this document formally amends ADR-0048's "single slot, one configured response
  per member" decision, on the strength of the evidence in §1 that ADR-0048 itself said was
  missing at the time. Everything else ADR-0048 decided (overload exclusion, `Match<T>` shape,
  matcher/call-log field separation, stateless `CallVerifier`) stands unchanged.
- **ADR-0049**: independent axis, no conflict; see §3.3's closing note on the deferred
  within-closed-`T` extension.

### 3.5 Explicitly out of scope (per product owner's instruction, not forgotten — separate evidenced problems)

Sequential/call-count-based returns; `Returns(Func<...>)` callbacks; `Received.InOrder`-style
call-order verification; open-generic method-own-`T` parameter matching (`SetContextDataAsync<T>`);
overloaded-member argument-value matching (`RetrieveTopPlayers`); cross-assembly generated-registry
first-registration-wins (`ISkillLocalizer`); recursive substitutes. Each of these has its own
evidence trail from this dogfood pass and deserves its own ADR-0049-style writeup — tracked
separately, not folded into this one.

---

## 4. Acceptance criteria

Not satisfied by Compono's own test suite alone. Required, in order:

1. Compono unit/generator/integration tests green for the new shape (new generator snapshot
   tests analogous to `TestDoubleVerifyTests.cs`'s existing matching-eligible snapshot, plus a
   runtime execution test proving reverse-order last-wins dispatch).
2. `CachedLeaderboardRepositoryTests.cs` (§1.2) migrated off NSubstitute onto `.Configure()`,
   expressing all three period/type/count-divergent cases directly.
3. `LeaderboardServiceTests.cs`'s 5 affected tests (§1.1) re-verified to actually exercise the
   branches they claim to — this is the sharpest acceptance signal, since today they pass for
   the wrong reason. Add or tighten assertions (e.g. on `HasScore`/`IsRanked`) so the tests would
   have caught the current silent-overwrite bug, proving the fix is real, not just
   non-crashing.
4. `MultiStubLeaderboardRepository` (§1.3) removed, its one call site migrated to
   `.Configure()` with two entries.
5. Full trivia-platform suite green against the newly-packed local Compono packages (not
   nuget.org preview) — see §5.

---

## 5. Local-package validation workflow

### 5.1 What already exists (reused, not reinvented)

- **Pack-to-local-feed pattern**: six existing per-sample-project scripts
  (`test/Compono.TestDoubles.SampleTests/pack-to-local-feed.sh` is the closest template — packs
  `Compono`, `Compono.XunitV3`, `Compono.TestDoubles`), each invoked automatically via an MSBuild
  `Target Name="PackToLocalFeed" BeforeTargets="Restore"`, guarded by a `mkdir`-based cross-process
  lock, clearing the restore package cache before each pack. All hardcode `-p:Version=1.0.0` —
  none of them need to interoperate with the real `0.x.y-preview.N` scheme, since that scheme is
  computed entirely outside this repo by the external `LayeredCraft/devops-templates` reusable
  workflow (confirmed: no NBGV/MinVer/GitVersion in this repo at all).
- **Local feed config shape**: `nuget.config` per consumer, `<clear/>` + a directory-relative
  `compono-local` source + `nuget.org` as fallback.
- **No existing wiring points at an external repo** — every existing "dogfood" precedent
  (`cosmere-tracker`, `lightsaber-skill`) consumes real *published* preview packages from
  nuget.org, never a local folder feed. trivia-platform has no `nuget.config` today at all. The
  local-feed → external-consumer wiring this task calls for is genuinely new, but it's a thin
  reuse of the existing per-sample-project pieces, not new invention.

### 5.2 Why a fixed `1.0.0` local version is wrong for this workflow specifically

The existing sample projects all use one static local version because they always restore
*within the same repo, same build* — a stale NuGet cache entry for `1.0.0` is flushed by their
own `RESTORE_PACKAGES_PATH`-clearing step every time, so the fixed version never causes a stale
resolve. trivia-platform is a **separate repo with its own independent NuGet cache and its own
`Directory.Packages.props` pin** — reusing a static version there risks exactly the failure mode
the workflow is designed to prevent (a stale cached `1.0.0` silently satisfying the restore
without picking up the latest local pack). Per the task's own instruction, unique versions per
validation run are required here, not overwritten-in-place versions.

### 5.3 Proposed workflow (documented process, minimal new tooling)

**Version scheme**: `0.0.0-local.{yyyyMMddHHmmss}` (timestamp-suffixed prerelease, guaranteed
monotonic and unique per run, sorts below any real `0.x` release so it can never accidentally
satisfy a `>=` floating constraint meant for a real version). Generated inline by the pack
command, not by a new versioning tool.

**Step-by-step** (extending the existing `pack-to-local-feed.sh` pattern, not replacing it):

```bash
# 1. From compono repo root
VERSION="0.0.0-local.$(date +%Y%m%d%H%M%S)"
FEED_DIR="/tmp/compono-trivia-platform-local-feed"   # or a fixed path both repos agree on
mkdir -p "$FEED_DIR"
for proj in src/Compono src/Compono.NSubstitute src/Compono.TestDoubles src/Compono.XunitV3; do
  dotnet pack "$proj" -c Release -o "$FEED_DIR" -p:Version="$VERSION"
done

# 2. Point trivia-platform at it (one-time nuget.config addition, see below) and pin the version
#    trivia-platform/Directory.Packages.props: bump all four Compono* PackageVersion entries to $VERSION

# 3. Restore + full suite, from trivia-platform repo root
dotnet restore
dotnet test
```

**trivia-platform-side one-time setup**: add a `nuget.config` at trivia-platform's root (none
exists today):
```xml
<configuration>
  <packageSources>
    <clear />
    <add key="compono-local" value="/tmp/compono-trivia-platform-local-feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```
(Not committed to trivia-platform's repo unless the team wants this permanently — it can also be
supplied per-invocation via `dotnet restore --configfile <path>` to avoid touching trivia-platform
at all. Recommend the per-invocation flag for now, since this is a validation-time tool, not a
permanent trivia-platform dependency.)

**Confirming step 6 of the task's ask ("actually using the new behavior, not a fake/fallback/stale
cache")**: run `dotnet test` with `-v:detailed` on just the affected test file(s) first (e.g.
`CachedLeaderboardRepositoryTests`), confirm zero NSubstitute-exception output and that the
test file itself has been migrated (no `Substitute.For`/`Arg.Is` left in it) — the test *content*
is the real proof, not the package version string, since a stale package would still show old
behavior even with a correctly-bumped version number if something else were wrong. Cross-check
`obj/project.assets.json` in the test project for the resolved `Compono.TestDoubles` version
string as a final sanity check that restore actually pulled the local pack and not a cached one.

### 5.4 Repeat discipline (steps 1–7 after every PR feedback change)

Document this as a single copy-pasteable block (candidate location: a new
`docs/dogfooding-local-validation.md`, or a section in `AGENTS.md` alongside its existing
local-feed-verification prose) rather than a script, for now — the task explicitly says not to
build CI infrastructure without evidence it's needed, and this is a manual, low-frequency
(per-PR-feedback-round) loop, not a per-commit one. If it turns out to run more than a
few-times-per-PR, the natural next step is a single `scripts/pack-local-for-trivia-platform.sh`
wrapping §5.3's loop — not proposed yet, since it hasn't been used enough times to know if the
timestamp-version/feed-dir choices above are the right defaults.

### 5.5 Recommended standing-workflow doc change

Add a short section to `AGENTS.md` (near its existing local-feed-verification paragraph) naming
this as the required validation gate for any TestDoubles-generator change with a live consumer
dogfood repo: Compono tests green **and** the named consumer's full suite green against a
freshly-packed, uniquely-versioned local package — not just "packed once at the start of the PR."
This generalizes the discipline the task asks for beyond just this one change.

---

## 6. Open questions — resolved

1. **`ReturnConfigBuilder<T>` construction shape**: resolved by spike — reference-type `Entry`
   (holds its `Config` field as an ordinary mutable field; `Configure()` takes a `ref` into the
   local variable before appending it to the list). Proven safe under list reallocation by
   construction (a `List<T>.Add()` only copies the reference, never relocates the referenced
   object) and confirmed empirically. Zero changes needed to `ReturnConfigBuilder<T>`/
   `ReturnConfig<T>`. See ADR-0050's "Considered Options."
2. **ADR-0049 composition**: resolved by spike — folds in cleanly, same `Entry` abstraction reused
   inside `_State<T>`, no new machinery beyond fully-qualifying the nested type name from the
   separate extension-method class. See ADR-0050's "Considered Options."
3. **Local-feed directory convention**: resolved — `scripts/dogfood-validate.sh` defaults to
   `.local-nuget-feed-dogfood` at the compono repo root (gitignored), overridable via
   `--feed-dir`/`DOGFOOD_FEED_DIR`.
4. **Fresh ADR vs. amendment**: resolved — [ADR-0050](../adr/0050-testdoubles-multi-entry-argument-distinguished-configuration.md),
   a new ADR that formally amends/supersedes ADR-0048's rejected ordered-response-chain option.

All four resolved; see ADR-0050 for the locked design and `scripts/dogfood-validate.sh` for the
validation script (built and run end-to-end against trivia-platform: 783/783 passing against a
freshly-packed local version, consumer git tree left unmodified, failure-path file-restore proven
under a deliberately forced failure). No implementation, commit, push, or PR made yet — awaiting
go-ahead to begin `implement.md` against ADR-0050.
