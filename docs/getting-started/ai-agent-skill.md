# AI Coding Agent Skill

Compono ships an official agent skill — guidance an AI coding agent (like
Claude Code) reads before writing, modifying, reviewing, or troubleshooting
Compono-based tests in your project. It's developer tooling, not a runtime
package: nothing here runs inside your test process, and it has no effect
on `dotnet build`/`dotnet test`.

## What it does

An agent without this skill knows Compono only from pretraining, and will
likely reach for AutoFixture-shaped habits that don't apply — `[Frozen]`
semantics, customization override, reflection-based construction. The
skill teaches the agent Compono's actual model: source-generated
composition, `[Composable]`'s narrow scope, registration/rule precedence,
`[Shared]`, deterministic seeding, the real `CMP0001`-`CMP0012` diagnostic
set, and the package-specific surface of `Compono.XunitV3`/
`Compono.NSubstitute`/`Compono.Bogus` — only recommending an integration's
API when that package is actually referenced in your project.

It also carries guardrails: it won't suggest reflection-based workarounds,
won't silently substitute AutoFixture, and won't add `[Composable]`
speculatively. And it stays out of the way for ordinary, non-Compono .NET
test work — it only activates on genuine Compono-related tasks.

## Install

The canonical source is the `skills/` directory of this repository. Add it
to a project via [`npx skills`](https://www.npmjs.com/package/skills)
(works with Claude Code and other `npx skills`-compatible agent hosts):

```bash
npx skills add LayeredCraft/compono
```

or, targeting the `skills/` directory explicitly:

```bash
npx skills add https://github.com/LayeredCraft/compono/tree/main/skills
```

This installs the `compono` skill into your project's agent-skill
directory (e.g. `.claude/skills/compono` for Claude Code). No NuGet
package, no `.csproj` change, no `dotnet` command — this is entirely
separate from installing the `Compono`/`Compono.XunitV3`/
`Compono.NSubstitute`/`Compono.Bogus` packages themselves (see
[Installation](installation.md) for those).

## Update

Re-run the same `npx skills add` command — it re-fetches the current
`skills/compono` content from this repository and overwrites your local
copy. There's no separate version pin to manage; you always get whatever
is currently on this repository's default branch.

## Which agents support it

Any agent host compatible with the `npx skills`/skills.sh convention — the
skill is plain Markdown (a `SKILL.md` plus `references/`), with no
Claude-specific mechanics baked in. It's developed and verified primarily
against Claude Code.

## Relationship to the NuGet packages

The skill and the packages are independent, and neither requires the
other:

- Installing the skill doesn't add any package reference to your project,
  and doesn't require Compono to already be in use — an agent with the
  skill installed can also help you *adopt* Compono in a project that
  doesn't have it yet, if you ask.
- Installing the packages without the skill works fine — the skill only
  changes how well an AI agent assists you; Compono itself doesn't know or
  care whether it's installed.
- The skill's guidance is checked against this repository's actual shipped
  API on every change — it should never describe an API that doesn't
  exist, or a roadmap item as if it were current.

See [ADR-0035](../adr/0035-compono-agent-skill-pack.md) for the design
decision behind the skill's structure.
