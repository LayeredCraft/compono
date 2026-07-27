# ADR-0001: Source Generation First

## Status

Accepted

## Decision

Generated composition plans are the primary execution mechanism.

Runtime reflection is intentionally excluded from the default architecture.

If reflection support is ever added, it must require an explicit opt-in by the consuming project.

## Rationale

Compono is being designed for modern .NET rather than older reflection-based architectures.

Choosing source generation by default improves performance, diagnostics, trimming, Native AOT readiness, and deterministic execution while keeping the runtime simpler.

## Consequences

Compatibility scenarios may require an explicit compatibility mode. That tradeoff is intentional.
