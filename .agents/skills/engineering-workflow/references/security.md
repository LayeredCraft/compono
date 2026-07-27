# Security standards

Compono is a library, not a service — there's no database, no network
listener, and no end-user-facing input surface to defend the way an
application would. The security concerns that actually apply here are
about what a library and its source generator can do to *consuming*
projects, and about the supply chain the packages ship through.

- **The source generator's input is the consuming project's own source
  code** — not adversarial in the security sense (a developer isn't
  attacking their own build), but it must still be treated as untrusted in
  the "don't assume well-formed" sense: malformed, incomplete, or
  in-progress (mid-edit) source is the normal case inside an IDE, not an
  edge case. A generator that throws an unhandled exception on invalid
  input breaks the consuming project's build/IDE experience entirely, so
  malformed input must produce a diagnostic (`docs/architecture.md`'s
  "Diagnostic failure" resolution-pipeline outcome), never an unhandled
  exception from generator code.
- **No dynamic code execution from data.** Composition requests, profile
  rules, and provider registrations are all authored in the consumer's own
  C#, resolved through the generated plan or the runtime pipeline
  (`ICompositionProvider`/`ICompositionPlan<T>`) — never build a path where
  a string (a config value, an attribute argument, anything not fully
  known at compile/generation time) gets `Emit`/`Reflection.Invoke`'d or
  otherwise turned into executable code at runtime. That would reintroduce
  exactly the runtime-reflection risk and unpredictability
  `docs/adr/0001-source-generation-first.md` deliberately excluded, plus a
  genuine injection surface on top.
- **NuGet package supply chain**: packages are signed and published
  through the standard NuGet.org pipeline — don't hand-roll a publish
  step that bypasses package signing, and don't add a build-time dependency
  (an analyzer, a source generator dependency) without checking it's a
  legitimate, actively maintained package. A compromised transitive
  dependency in an analyzer/source-generator package runs inside every
  consuming project's build, which is a materially bigger blast radius
  than a runtime-only dependency would be.
- **No secrets, ever, anywhere in this repo** — there's no runtime
  deployment target that would need them (no connection strings, no API
  keys, no service credentials), so if something that looks like a secret
  shows up in a sample, a test fixture, or a config file, that's a strong
  signal it was pasted in by mistake, not a legitimate default that needs
  an env-var override.
- Trimming and Native AOT compatibility (`README.md`'s stated goals) are a
  correctness/compatibility concern more than a security one, but they
  intersect here: reflection-based fallbacks tend to be exactly the code
  paths that break trimming, so keeping reflection out of the default path
  serves both goals at once — another reason not to reach for it quietly
  to unblock one feature (see `design-decisions.md` on that).
