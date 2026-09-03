#!/usr/bin/env bash
# PLAN-0059 task group 9's permanent, repeatable, CI-blocking supported-version x runner
# compatibility matrix for Compono.NUnit (ADR-0059 §6's monitoring requirement for the accepted
# NUnit Internal-namespace dependency risk). Builds test/Compono.NUnit.CompatibilityMatrix once per
# NUnit version leg against a freshly packed local Compono/Compono.NUnit, verifies the *resolved*
# NUnit assembly version from the real obj/project.assets.json output (not merely the requested
# PackageReference version - the exact discipline that caught the MSTest silent-upgrade near-miss,
# RESEARCH-0017 §17/RESEARCH-0018 §18), then runs the identical build artifact under both classic
# VSTest (`dotnet vstest`) and MTP (running the built executable directly) - RESEARCH-0018 §11's own
# methodology, and the only reliable way found during this project's own development to exercise
# both runners without a real regression (see the .csproj's own comment for the reproduced hazard).
#
# Blocking legs (ADR-0059 §3's supported [3.14.0, 5.0.0) range):
#   NUnit 3.14.0 (floor, exact-pinned) x classic VSTest
#   NUnit 3.14.0 (floor, exact-pinned) x MTP
#   newest resolvable stable NUnit 4.x (dynamically tracked, NOT a hardcoded literal - see below)
#     x classic VSTest
#   newest resolvable stable NUnit 4.x x MTP
#
# NUnit 5 prerelease is deliberately NOT a leg here - non-blocking forward-compatibility
# surveillance only (ADR-0059 §3), never part of the blocking support contract.
#
# Why the "current stable 4.x" leg is NOT a hardcoded version literal (fixed after PR #127 review):
# Compono.NUnit's own supported dependency range is `NUnit [3.14.0, 5.0.0)` - every NUnit 4.x
# release is a range match, so a consumer can resolve any of them. A leg that always builds against
# one fixed 4.x version (e.g. "4.6.1") stops being "current stable 4.x" the moment a newer 4.x
# release ships - CI stays green while a real break against the *actually resolvable* newest 4.x
# assembly would go completely undetected until a consumer hit it. This leg instead requests NuGet's
# own floating-version syntax (`4.*`, via the compatibility-matrix project's existing
# `NUnitMatrixVersion`/`VersionOverride` mechanism - the same restore-time override the floor leg
# already used, no project changes needed) so `dotnet build`'s own restore always asks the package
# source for whatever the newest stable 4.x release is *at run time*, then asserts the concretely
# *resolved* version (from project.assets.json, same discipline as every other leg) is genuinely a
# stable 4.x release - not a prerelease build and not (once NUnit 5 ships) an unexpectedly-resolved
# 5.x - before running it. The floor leg keeps its own exact-version assertion unchanged; only the
# "current stable" leg's assertion is relaxed from exact-match to stable-4.x-shaped, and only
# because its whole point is to track a moving target instead of a pinned one.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
project="$repo_root/test/Compono.NUnit.CompatibilityMatrix/Compono.NUnit.CompatibilityMatrix.csproj"
project_dir="$repo_root/test/Compono.NUnit.CompatibilityMatrix"
tfm="net10.0"
configuration="Release"

floor_version="3.14.0"
# NuGet floating-version syntax: "resolve the newest available 4.x release" - re-evaluated by
# restore every single run (never resolved once and cached as a literal), because `run_leg` below
# always deletes obj/bin before building, forcing a genuine fresh restore/resolution every
# invocation rather than reusing a previous run's project.assets.json.
current_stable_request="4.*"

# Set by run_leg on success - the caller reads this immediately after each call to report/assert
# the exact concrete version that was actually resolved and exercised (Bash functions can't return
# strings, so this is the simplest correct mechanism here; always consumed before the next call).
RESOLVED_VERSION=""

run_leg() {
    local nunit_version="$1"
    local leg_id="$2"
    # "exact" (default) - resolved must equal the requested version literally, for a pinned leg
    # (the floor, and the NUnit 5 surveillance leg). "stable-4x" - resolved must be a genuine
    # stable NUnit 4.x release (MAJOR.MINOR.PATCH, MAJOR=4, no prerelease suffix) rather than
    # literally equal to the floating request string - the whole point of that leg is that the
    # concrete version is expected to change over time as NUnit ships new 4.x releases.
    local assertion="${3:-exact}"

    echo "::group::NUnit ${nunit_version} - build + resolved-version check"

    rm -rf "$project_dir/obj" "$project_dir/bin"

    dotnet build "$project" -f "$tfm" -c "$configuration" \
        -p:NUnitMatrixVersion="$nunit_version" \
        -p:Compono_LocalPackagesId="$leg_id" \
        --nologo

    local resolved
    resolved=$(python3 -c "
import json
with open('$project_dir/obj/project.assets.json') as f:
    data = json.load(f)
for lib in data['libraries']:
    if lib.lower().startswith('nunit/'):
        print(lib.split('/', 1)[1])
        break
")

    case "$assertion" in
        exact)
            if [ "$resolved" != "$nunit_version" ]; then
                echo "::error::Requested NUnit ${nunit_version} but project.assets.json resolved ${resolved:-<none>} - a transitive dependency silently changed the version (the exact MSTest near-miss RESEARCH-0017 §17 warned about)." >&2
                # `return`, not `exit` - the surveillance leg below calls this function inside an
                # `if` so a failure there is non-fatal by design; `exit` would bypass that and kill
                # the whole script regardless of call-site context, which is only correct for the
                # blocking legs (where a non-zero return from an unguarded top-level call still
                # trips `set -e` as intended).
                return 1
            fi
            ;;
        stable-4x)
            if [ -z "$resolved" ]; then
                echo "::error::Requested the newest stable NUnit 4.x (floating '${nunit_version}') but no NUnit version could be resolved from project.assets.json at all." >&2
                return 1
            fi
            if ! [[ "$resolved" =~ ^4\.[0-9]+\.[0-9]+$ ]]; then
                echo "::error::Requested the newest stable NUnit 4.x (floating '${nunit_version}') but project.assets.json resolved '${resolved}' - not a stable 4.x release (MAJOR.MINOR.PATCH with MAJOR=4, no prerelease suffix). This leg's own \"current stable NUnit 4.x\" contract would silently drift outside its intended range (e.g. onto a 4.x prerelease, or a 5.x release once one exists) if this were allowed through." >&2
                return 1
            fi
            ;;
        *)
            echo "::error::Internal script error: unknown assertion mode '${assertion}'." >&2
            return 1
            ;;
    esac

    echo "Resolved NUnit assembly version confirmed: ${resolved}"
    RESOLVED_VERSION="$resolved"
    echo "::endgroup::"

    local dll="$project_dir/bin/$configuration/$tfm/Compono.NUnit.CompatibilityMatrix.dll"
    local exe="$project_dir/bin/$configuration/$tfm/Compono.NUnit.CompatibilityMatrix"

    echo "::group::NUnit ${resolved} - classic VSTest"
    # Explicit exit-code capture, not bare `set -e` propagation: `run_leg` is called as the
    # condition of an `if` for the surveillance leg below, and Bash disables `errexit` inside a
    # function for the duration of a call made in that context - a failing `dotnet vstest`/`$exe`
    # here would otherwise be silently swallowed (execution falls through to the next line instead
    # of aborting), and the function would return the *last* command's exit status (a trailing
    # `echo`, which always succeeds) rather than the real failure - hiding exactly the compatibility
    # signal this script exists to collect.
    if ! dotnet vstest "$dll"; then
        echo "::error::NUnit ${resolved} failed under classic VSTest." >&2
        return 1
    fi
    echo "::endgroup::"

    echo "::group::NUnit ${resolved} - MTP"
    if ! "$exe"; then
        echo "::error::NUnit ${resolved} failed under MTP." >&2
        return 1
    fi
    echo "::endgroup::"
}

run_leg "$floor_version" "matrix-floor" "exact"
resolved_floor="$RESOLVED_VERSION"

run_leg "$current_stable_request" "matrix-current-stable" "stable-4x"
resolved_current_stable="$RESOLVED_VERSION"

echo "All blocking NUnit compatibility-matrix legs passed: floor ${resolved_floor}, and current stable 4.x ${resolved_current_stable} (dynamically resolved from a floating '${current_stable_request}' request, not a hardcoded literal), each under classic VSTest and MTP."

# --- Non-blocking forward-compatibility surveillance (ADR-0059 §3) ---
# NUnit 5 is prerelease-only and outside the [3.14.0, 5.0.0) support contract - this leg is
# informational only and must never fail the job, and stays exact-pinned (unlike the current-stable
# leg above) because there is no "current stable 5.x" to track yet - only a specific named
# prerelease build. Promote to a blocking `run_leg` call above (with its own "stable-5x" assertion
# mode, mirroring "stable-4x") once NUnit 5.0.0 ships stable and ADR-0059's range is amended.
surveillance_version="5.0.0-beta.1"
echo "::group::NUnit ${surveillance_version} - forward-compatibility surveillance (non-blocking)"
if run_leg "$surveillance_version" "matrix-surveillance" "exact"; then
    echo "Surveillance leg passed: NUnit ${surveillance_version} works today. No action needed."
else
    echo "::warning::NUnit ${surveillance_version} surveillance leg failed - not a blocking failure (NUnit 5 is outside the current [3.14.0, 5.0.0) support contract), but worth investigating before that range is ever widened."
fi
echo "::endgroup::"
