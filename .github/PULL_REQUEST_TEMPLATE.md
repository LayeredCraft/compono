## Summary

<!-- What does this PR do, and why? -->

## Related issue

<!-- Closes #... , or "N/A" for a small, obvious fix -->

## Checklist

- [ ] PR title follows [Conventional Commits](https://www.conventionalcommits.org/)
      (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`, `ci:`,
      `revert:`) — add `!` or apply the `breaking-change` label if this
      breaks an existing public API.
- [ ] `dotnet build Compono.slnx -c Release` and
      `dotnet test Compono.slnx -c Release` pass locally.
- [ ] New/changed composition-engine, provider, or generator behavior has
      test coverage.
- [ ] Every new/changed public member has an XML doc comment.
- [ ] Relevant `docs/*.md` page(s) updated in this same PR, if behavior
      changed.
- [ ] If this PR adds or bumps a dependency, I checked the target
      package's license (see
      [Contributing → Dependency changes](https://layeredcraft.github.io/compono/contributing/#dependency-changes)).

## Additional context

<!-- Anything a reviewer needs to know that isn't obvious from the diff -->
