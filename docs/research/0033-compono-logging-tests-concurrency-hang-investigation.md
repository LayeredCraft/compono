# [RESEARCH-0033] Compono.Logging.Tests Concurrency Hang Investigation

**Status:** Done

**Date:** 2026-09-14

## Scope

A full-solution `dotnet test` run of this repository has intermittently hung for extended
periods (27+ minutes observed locally; CI runs killed after 5+ minutes of no progress),
with `test/Compono.Logging.Tests`' test host consuming very high CPU when the hang was
first noticed. This research reproduces the hang directly, identifies the exact test and
code path responsible, captures live diagnostic evidence, and separately evaluates whether
three specific historical CI failures share this root cause.

This is a bug-analysis record, not a product design record, and creates no ADR - the
underlying defect is in test code (`test/Compono.Logging.Tests/ConcurrencyTests.cs`), not
`Compono.Logging`'s shipped production code, and the fix (bounding an unbounded test loop)
needs no architectural decision.

## Reproduction

Reproduced directly: a full-solution `dotnet test` (Debug, all TFMs) hung with
`Compono.Logging.Tests` net9.0 and net10.0 both starting but never completing, while every
other project in the solution - including projects that run *after* `Compono.Logging.Tests`
in the default ordering (`Compono.MSTest.Tests`, `Compono.DependencyInjection.Tests`, all
four TFMs each) - continued and completed normally. This confirms the hang is scoped to
those two specific test-host processes, not a solution-wide stall.

Live process state at the time of capture (net9.0 host, pid 37075):

```
PID ELAPSED  %CPU    RSS       VSZ
37075 03:48  95.7   7,609,184 KB   477,019,648 KB   (~7.6 GB RSS)
37075 03:54  149.7  6,738,544 KB   481,084,464 KB   (~6.7 GB RSS, multi-core CPU)
```

RSS in the multiple-gigabyte range, oscillating (not monotonically climbing, consistent
with GC reclaiming some of it) after under 4 minutes of runtime for a test project whose
entire suite normally completes in ~0.5-1.5 seconds. `%CPU` over 100% confirms multiple
threads actively working, not a single thread deadlocked and idle.

It did **not** reproduce on the same run for net8.0/net11.0 (both completed in 539ms each)
- see "TFM dependence" below.

## Exact hanging test and code path

`test/Compono.Logging.Tests/ConcurrencyTests.cs`,
`ReadsConcurrentWithWrites_NeverThrowOrCorrupt`:

```csharp
[Fact]
public async Task ReadsConcurrentWithWrites_NeverThrowOrCorrupt()
{
    var logger = new CapturingLogger<ConcurrencyTests>();
    using var cts = new CancellationTokenSource();

    var writer = Task.Run(() =>
    {
        while (!cts.IsCancellationRequested)
            logger.LogInformation("write");
    }, TestContext.Current.CancellationToken);

    var reader = Task.Run(() =>
    {
        for (var i = 0; i < 500; i++)
        {
            var entries = logger.GetCapturedEntries();
            foreach (var entry in entries)
                _ = entry.Message;
        }
    }, TestContext.Current.CancellationToken);

    await reader;
    cts.Cancel();
    await writer;
}
```

A grep of the whole test project for unbounded loops (`while`, `for (;;)`, spin-wait
helpers) turns up exactly one candidate: this method's writer loop. The other two
`ConcurrencyTests` methods (`ManyParallelLogCalls_NoLostEntries`,
`LoggingFactoryRegistry_ConcurrentRegisterAndTryCreate_IsSafe`) are both bounded (a fixed
`200 * 20` and `50` iterations respectively) and terminate in well under a second even
under contention - they are not candidates for a multi-minute hang.

## Root cause

**This is a test-code defect: an unbounded, unthrottled producer racing a bounded
consumer, with no backpressure, combined with an O(n) "read everything captured so far"
operation on the shared side.**

The writer loop has no yield point (no `await Task.Yield()`, no `Thread.Sleep`, no
`Task.Delay`) and no upper bound - it calls `logger.LogInformation("write")` as fast as the
CPU allows until `cts.IsCancellationRequested` becomes true. `cts.Cancel()` is only called
*after* `await reader` completes - so nothing bounds how many entries the writer can add
before the reader's fixed 500 iterations finish and let cancellation happen. Under normal,
uncontended conditions the reader's 500 iterations finish in a few milliseconds and the
writer loop only runs briefly. Under sufficient CPU/thread-pool contention (see "Conditions
required" below), the reader can be starved long enough for the writer to add a very large,
unbounded number of entries before cancellation ever triggers.

Two mechanisms compound this once the writer gets far enough ahead:

1. **`LogEntryCollector.GetEntries()`** (`src/Compono.Logging/LogEntryCollector.cs`) does:
   ```csharp
   public IReadOnlyList<CapturedLogEntry> GetEntries()
   {
       lock (_lock)
       {
           return [.. _entries];
       }
   }
   ```
   an `O(n)` full-array copy under lock, called by `GetCapturedEntries()` on **every** one
   of the reader's 500 iterations. As the writer-fed list grows, each of the reader's own
   500 calls gets progressively more expensive - turning what should be `O(n)` total work
   into effectively superlinear total work relative to how many entries have accumulated by
   the time each call happens. This is the direct reason the reader's fixed 500-iteration
   loop can take minutes instead of milliseconds once the list is large: it isn't stuck, it
   is doing genuinely more work per iteration than intended.

2. **Every writer iteration allocates.** `LogEntryCollector.Record` builds a full
   `CapturedLogEntry` (message, extracted structured-state properties array, a scope
   snapshot array, a timestamp) and locks briefly to append it to the internal
   `List<CapturedLogEntry>`. At high call rates this drives sustained GC pressure, further
   compounded by the backing list's own periodic reallocate-and-copy as it grows past each
   capacity doubling. This is directly visible in a live thread sample (below).

Neither `LogEntryCollector`'s locking nor `CapturingLogger<T>`'s implementation is
incorrect by itself - the lock is narrow, uncontended critical sections are correct, and
nothing here is a classic deadlock (no cyclic lock-ownership) or livelock (the threads
aren't stuck repeatedly undoing each other's progress - real work keeps completing:
allocation, garbage collection, and locked copies all continue throughout). This is more
precisely a **resource-starvation / performance-runaway** pathology: an unthrottled
producer racing an O(n)-per-call consumer with no backpressure, where the *amount* of real
work required keeps expanding faster than either side can keep up, once external
contention is severe enough to let the producer get far enough ahead.

### Diagnostic evidence

`dotnet-dump collect` could not attach on this machine (`task_for_pid` failed - macOS
SIP/entitlement restriction in this environment, not a tool problem); macOS's built-in
`sample` utility worked instead and captured native call stacks. The dominant thread stack
(net9.0 host, PID 37075, 5-second sample @ 1ms resolution, `.NET TP Worker` thread) splits
almost entirely between two states:

- **~1035/3978 samples** in `JIT_New` → `AllocateObject` → `WKS::GCHeap::Alloc` →
  `try_allocate_more_space` → `trigger_gc_for_alloc` → a full blocking
  `WKS::gc_heap::garbage_collect` (`mark_phase`, `plan_phase`, `make_free_lists`, ...) -
  i.e. actively allocating and triggering/running garbage collection.
- **~1820/3978 samples** blocked in `JIT_MonReliableEnter_Portable` → `AwareLock::Enter()`
  → `DoAppropriateWaitWorker` → `pthread_cond_wait` - i.e. waiting to acquire a managed
  `lock`/`Monitor` (consistent with `LogEntryCollector`'s `lock (_lock)`, contended because
  both the writer and the reader are calling into it constantly).

This is exactly the signature of high CPU with no real progress: threads splitting their
time between "doing GC because allocation is happening far faster than the process can
usefully consume it" and "waiting on a lock that's held only briefly but is being
acquired extremely often from both sides."

## Why isolation doesn't reproduce it

Running `Compono.Logging.Tests` alone (as previously done ad hoc, and confirmed in this
investigation by the fact that the bug did not fire on net8.0/net11.0 in the same
full-solution run that did trigger it on net9.0/net10.0) removes the contention this bug
depends on: with no other test project's `Task.Run` work, no other project's own
concurrently-running xUnit v3 test collections, and no OS-level competition from sibling
`dotnet test` processes for CPU cores, the reader task gets scheduled promptly and finishes
its fixed 500 iterations in milliseconds - long before the writer can meaningfully outpace
it. The bug is a genuine race, not a deterministic defect; it needs enough contention that
the OS/thread-pool scheduler can starve the reader relative to the writer for a
non-trivial span of time.

## Conditions required to trigger it

- **Solution-level concurrency is necessary in practice.** Isolated single-project runs
  did not reproduce it in this investigation or in the session's earlier ad hoc check.
- **`Compono.Logging.Tests`' own internal test parallelism is a contributing factor, not
  the root cause.** `xunit.runner.json` for this project is empty (`{}`), so xUnit v3's
  default parallel-collection execution applies - `ConcurrencyTests`' three methods and
  every other test class in the assembly can all be scheduled concurrently on the same
  process-local thread pool, adding to the contention the bug depends on, but the test
  method itself is defective regardless of what else is running.
- **No evidence found of shared/static cross-test state being at fault.** Each
  `ConcurrencyTests` method constructs its own `CapturingLogger<ConcurrencyTests>` /
  `LoggingOptions` - `LoggingFactoryRegistry`'s static `ConcurrentDictionary` (used by a
  different method) is unrelated to this one's `CapturingLogger` instance.
- **Runtime-specific behavior:** none established. The single live reproduction in this
  investigation hit net9.0 and net10.0, not net8.0/net11.0 - the opposite TFM pairing from
  every historical CI failure examined below (net8.0 every time). This is consistent with
  a genuine scheduling race with no fixed TFM affinity, not a runtime-version-specific bug
  - see the CI comparison for the caveat on sample size.
- **Category:** test-runner/concurrency interaction plus a genuine test-code defect (an
  unbounded producer with no backpressure against a bounded consumer, compounded by an
  O(n)-per-call read), not production code, not cross-test shared state, and not a runtime
  or genuine external-resource-starvation issue on its own (the "resource starvation" here
  is self-inflicted by the test's own design, not caused by anything outside the test).

## Relationship to historical CI failures

Three independent CI failures were examined, all showing GitHub Actions' generic
`The runner has received a shutdown signal.` / `The operation was canceled.` message with
no other visible error. Re-reading all three logs specifically for each test project's
*completion* line (not just what printed immediately before the cutoff, since solution-level
execution is concurrent and the last line printed is not necessarily the process that is
actually stuck) produces a single, exactly consistent pattern:

| CI run | Logging.Tests net8.0 | net9.0 | net10.0 | net11.0 |
|---|---|---|---|---|
| PR #140 build, ~17:51 UTC | **started, never completed** | passed (27.2s) | passed (6.4s) | passed (4.2s) |
| PR #140 build, ~18:11 UTC | **started, never completed** | passed (24.9s) | passed (15.5s) | passed (1.4s) |
| `Publish Preview` run [34892945289](https://github.com/LayeredCraft/compono/actions/runs/34892945289), 20:26 UTC | **failed after 5m 25.85s, Exit code: 137** | passed (15.5s) | passed (1.1s) | passed (1.2s) |

The third run is unambiguous: `Compono.Logging.Tests` net8.0 started at `20:29:12.20Z`,
every other project in the solution (including ones that start after it) continued and
completed normally, and at `20:34:38.07Z` - simultaneous with `The runner has received a
shutdown signal.` - the aggregator reported net8.0 as `failed` with an elapsed time of
`5m 25s 850ms` and `Exit code: 137` (SIGKILL). No other project or test host shows any
anomalous duration or missing completion line in any of the three logs. The exact `dotnet
test` invocation for the third run: `dotnet test --solution Compono.slnx --configuration
"Release" --no-build` (job `build / publish`, step `Run dotnet test`, workflow
`.github/workflows/publish-preview.yaml`, which delegates the job itself to
`LayeredCraft/devops-templates/.github/workflows/publish-preview.yml@v10.5` - that reusable
workflow's own job configuration, including any `timeout-minutes`, is not visible from this
repository, so whether a configured job timeout (vs. some other GitHub-side runner
reclamation) triggered the shutdown could not be determined from available logs).

**Conclusion: the relationship is confirmed for all three examined CI failures** - not
merely plausible. All three show the identical signature this investigation's local
reproduction also confirmed: `Compono.Logging.Tests` starts, its sibling TFMs in the same
project complete normally, and it alone never reports completion (or is killed) while nets
of *other* projects run to completion around it. The specific mechanism (the unbounded
`ReadsConcurrentWithWrites_NeverThrowOrCorrupt` writer loop, compounded by
`LogEntryCollector.GetEntries()`'s O(n)-per-call copy) established by the local
reproduction is the most direct explanation available and no other candidate in this test
project fits the "runs long, consumes resources, doesn't reliably reproduce" profile.

**One open point:** all three CI failures independently landed on net8.0 specifically,
while the single local reproduction landed on net9.0 and net10.0. With only three CI data
points, this could be a genuine TFM-independent race that happened to land on net8.0 three
times by chance (GitHub-hosted runners have far fewer cores than the local machine used for
this investigation, which could bias scheduling more consistently toward whichever TFM's
assembly happens to start first/last in that constrained environment), or there could be a
net8.0-specific factor (e.g. `List<T>`/GC behavior differences across runtime versions)
narrowing the window further on that TFM specifically. The evidence available does not
distinguish between these - flagged as unresolved rather than asserted either way.

## Fix implemented

The initial proposal in this section (a `Task.Yield()`-throttled but still functionally
unbounded writer) was superseded before implementation: `Task.Yield()` improves scheduling
fairness but establishes no deterministic upper bound on allocations or execution time, so
it would not have removed the actual defect (the writer's termination still depending on
`cts.IsCancellationRequested`, itself still set only after the reader finished).

`ReadsConcurrentWithWrites_NeverThrowOrCorrupt` (`test/Compono.Logging.Tests/ConcurrencyTests.cs`)
was redesigned instead so that **neither participant's termination depends on the other**:

```csharp
[Fact]
public async Task ReadsConcurrentWithWrites_NeverThrowOrCorrupt()
{
    var logger = new CapturingLogger<ConcurrencyTests>();
    const int writeCount = 2_000;
    const int readIterations = 50;

    using var startGate = new Barrier(2);

    var writer = Task.Run(() =>
    {
        startGate.SignalAndWait(TestContext.Current.CancellationToken);

        for (var i = 0; i < writeCount; i++)
            logger.LogInformation("write {Index}", i);
    }, TestContext.Current.CancellationToken);

    var reader = Task.Run(() =>
    {
        startGate.SignalAndWait(TestContext.Current.CancellationToken);

        for (var i = 0; i < readIterations; i++)
        {
            var entries = logger.GetCapturedEntries();
            foreach (var entry in entries)
                _ = entry.Message;
        }
    }, TestContext.Current.CancellationToken);

    await Task.WhenAll(writer, reader);

    logger.GetCapturedEntries().Should().HaveCount(writeCount);
}
```

- **Both workloads are explicitly bounded** - the writer always performs exactly
  `writeCount` (2,000) iterations; the reader always performs exactly `readIterations`
  (50) iterations, each doing one `GetCapturedEntries()` call. Neither loop has a
  `while (condition)` shape at all anymore.
- **Deterministic upper bound on captured entries, and therefore on `GetCapturedEntries()`'s
  cost:** the list can never exceed `writeCount` entries, so each reader call's `O(n)` copy
  is bounded by `O(writeCount)`, and the reader's total work is bounded by
  `O(readIterations * writeCount)` = 100,000 element-copies worst case - a few
  milliseconds, not unbounded.
- **No participant depends on winning a scheduling race** - the writer doesn't stop because
  the reader finished (there is no cancellation token in this test anymore at all); the
  reader doesn't stop because the writer finished. Each simply does its own fixed amount of
  work and returns.
- **A `Barrier(2)` start gate, not timing-based synchronization.** Both tasks call
  `SignalAndWait` before doing any logging/reading work, which *guarantees* both are started
  and have reached that point before either workload begins - substantially improving the
  opportunity for genuine concurrent execution compared to plain `Task.Run`. This does
  **not** guarantee operation-level interleaving after the barrier releases - the scheduler
  could still, in principle, let one loop run ahead of (or even to completion before) the
  other makes further progress. What it rules out is the weaker failure mode of no gate at
  all: one loop running to completion before the other is even scheduled, which would still
  pass but would exercise essentially no concurrent access at all.
- **The test's existing contract is unchanged** - it still asserts, via the same
  `_ = entry.Message` touch on every captured entry read while the writer is concurrently
  appending, that concurrent reads and writes never throw or observe corrupted state. A new
  final assertion (`logger.GetCapturedEntries().Should().HaveCount(writeCount)`) is added as
  a concrete, deterministic proof that no entries are lost or duplicated - stronger than the
  original test's implicit assertion, not a scope change.
- **No production `Compono.Logging` code changed.** `LogEntryCollector`'s locking (a plain
  `lock` around a `List<T>`, `O(n)` `GetEntries()`) is unchanged - nothing in this
  investigation's evidence showed that behavior to be incorrect; the defect was entirely in
  how the test drove it.

This does not prove the original probabilistic hang can never recur through some other
path - it proves the new structure removes the actual mechanism identified above: neither
loop can grow unboundedly, and neither loop's termination is contingent on the other's
progress or on any particular scheduling outcome.

### Validation

Full validation results (all supported TFMs, repeated full-solution runs, memory
observation) are recorded in this repo's PR/commit history rather than duplicated here -
see the implementing commit's message for the exact run counts and outcomes.

## Recommended CI diagnostic instrumentation (observability, not mitigation)

This investigation did not need to change CI, and none of the above required it - the local
reproduction plus the CI logs' own completion-line evidence was already sufficient to reach
attribution for all three examined runs. For a *future* occurrence, the smallest
observability addition that would make a stuck project/TFM attributable without cross-
referencing timestamps by hand would be surfacing MTP's own per-assembly progress/timeout
reporting more directly in the CI log (or, at minimum, ensuring the workflow step's own
timeout value is visible in-repo rather than only inside the external `devops-templates`
reusable workflow, so a future investigator doesn't have to guess whether a shutdown was a
configured timeout or something else). Neither of these was implemented as part of this
investigation, per scope.

## Links

- [ADR-0055](../adr/0055-compono-logging-testing-support-package.md) - `Compono.Logging`'s
  own design, `LogEntryCollector`/`CapturingLogger`/`CapturingLogger{T}` shape.
- [project_ci_test_hang_flake memory] - this repo's earlier, weaker-evidence hypothesis
  that CI's runner-shutdown failures were "likely" related to the local hang; this research
  supersedes that with a confirmed mechanism for the three runs examined here.
