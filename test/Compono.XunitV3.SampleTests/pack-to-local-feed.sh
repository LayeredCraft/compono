#!/usr/bin/env bash
# Packs Compono, Compono.XunitV3, Compono.NSubstitute, and Compono.Bogus into the local NuGet feed this
# project restores against (test/Compono.XunitV3.SampleTests/nuget.config), serialized behind a
# cross-process lock, and clears this restore's own isolated packages path before every pack.
#
# Why the lock: this script is invoked from a PackToLocalFeed MSBuild target
# (BeforeTargets="Restore") on every restore of this project. CI (and RealRunnerTests, which shells
# out `dotnet test` against this project) runs Compono.XunitV3.Tests for multiple TFMs concurrently,
# so two independent nested `dotnet test` processes can each trigger this script at the same moment,
# both packing the same src/Compono and src/Compono.Generators projects into the same
# .local-nuget-feed/ directory - a real race (reproduced locally: "The process cannot access the
# file '.../Compono.1.0.0.nupkg' because it is being used by another process", and in CI: "Could not
# find a part of the path '.../Compono.Generators/bin/Debug/netstandard2.0'"). `mkdir` is atomic even
# across processes and platforms, so it works as a portable mutex without a custom MSBuild task.
#
# Why $restore_packages_path is cleared here (PR #26 review, second round): NuGet treats a package
# id+version already present in a packages folder as immutable and never re-extracts it, so this
# script repacking different content under the same fixed "1.0.0" version would otherwise let a stale
# entry silently satisfy a later restore. Two earlier fixes were tried and reverted first (see
# PLAN-0004 Phase 3 Notes for the full account): clearing NuGet's *shared global* packages folder here
# raced against a sibling process's own restore still reading from it after this script's lock
# released ("Could not find a part of the path '.../packages/compono/1.0.0'", reproduced locally and
# caught in CI); a per-evaluation-random package version (via MSBuild's $([System.Guid]::NewGuid()))
# turned out unstable, because NuGet's restore-graph evaluation pass and the actual build evaluation
# pass each re-evaluate that property function independently, producing two different GUIDs for the
# same nominal build (a real NU1102 version mismatch, reproduced locally). $restore_packages_path
# (scoped per $(TargetFramework) by the caller) has neither problem: it's private to this one
# project+TFM's own restore, so clearing it can never affect a concurrent sibling process running a
# different TFM, and there is nothing left to make "the same value across evaluation passes" in the
# first place - it's a plain path, not a value that needs to agree with anything else.
set -euo pipefail

compono_csproj="$1"
xunitv3_csproj="$2"
nsubstitute_csproj="$3"
bogus_csproj="$4"
feed_dir="$5"
configuration="$6"
restore_packages_path="$7"

lock_dir="$feed_dir/.pack.lock"

# Bounded wait, not an unconditional retry loop (PR #26 review, sixth round) - if a prior holder was
# killed before its own EXIT trap could run (SIGKILL, a canceled build, a machine restart), the lock
# directory is never cleaned up, and every subsequent restore of this project would otherwise wait on
# it forever. A real pack of both projects normally takes well under a minute, so 120 attempts (~2
# minutes) is generous headroom for a genuinely concurrent sibling while still failing fast, with
# actionable remediation, on a truly abandoned lock rather than hanging indefinitely.
max_wait_attempts=120
attempt=0

until mkdir "$lock_dir" 2>/dev/null; do
    attempt=$((attempt + 1))
    if [ "$attempt" -ge "$max_wait_attempts" ]; then
        echo "pack-to-local-feed.sh: timed out after ${max_wait_attempts}s waiting for the lock at '$lock_dir'." >&2
        echo "This usually means a previous run was killed before it could release the lock. If no other" >&2
        echo "'dotnet pack'/'dotnet test' against this project is currently running, remove it manually and" >&2
        echo "retry: rm -rf '$lock_dir'" >&2
        exit 1
    fi
    sleep 1
done
trap 'rmdir "$lock_dir"' EXIT

rm -rf "$restore_packages_path"

dotnet pack "$compono_csproj" -c "$configuration" -o "$feed_dir" -p:Version=1.0.0 --nologo
dotnet pack "$xunitv3_csproj" -c "$configuration" -o "$feed_dir" -p:Version=1.0.0 --nologo
dotnet pack "$nsubstitute_csproj" -c "$configuration" -o "$feed_dir" -p:Version=1.0.0 --nologo
dotnet pack "$bogus_csproj" -c "$configuration" -o "$feed_dir" -p:Version=1.0.0 --nologo
