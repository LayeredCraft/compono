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
#   NUnit 3.14.0 (floor) x classic VSTest
#   NUnit 3.14.0 x MTP
#   current stable NUnit 4.x x classic VSTest
#   current stable NUnit 4.x x MTP
#
# NUnit 5 prerelease is deliberately NOT a leg here - non-blocking forward-compatibility
# surveillance only (ADR-0059 §3), never part of the blocking support contract.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
project="$repo_root/test/Compono.NUnit.CompatibilityMatrix/Compono.NUnit.CompatibilityMatrix.csproj"
project_dir="$repo_root/test/Compono.NUnit.CompatibilityMatrix"
tfm="net10.0"
configuration="Release"

floor_version="3.14.0"
current_stable_version="4.6.1"

run_leg() {
    local nunit_version="$1"
    local leg_id="$2"

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

    if [ "$resolved" != "$nunit_version" ]; then
        echo "::error::Requested NUnit ${nunit_version} but project.assets.json resolved ${resolved:-<none>} - a transitive dependency silently changed the version (the exact MSTest near-miss RESEARCH-0017 §17 warned about)." >&2
        # `return`, not `exit` - the surveillance leg below calls this function inside an `if` so a
        # failure there is non-fatal by design; `exit` would bypass that and kill the whole script
        # regardless of call-site context, which is only correct for the blocking legs (where a
        # non-zero return from an unguarded top-level call still trips `set -e` as intended).
        return 1
    fi

    echo "Resolved NUnit assembly version confirmed: ${resolved}"
    echo "::endgroup::"

    local dll="$project_dir/bin/$configuration/$tfm/Compono.NUnit.CompatibilityMatrix.dll"
    local exe="$project_dir/bin/$configuration/$tfm/Compono.NUnit.CompatibilityMatrix"

    echo "::group::NUnit ${nunit_version} - classic VSTest"
    dotnet vstest "$dll"
    echo "::endgroup::"

    echo "::group::NUnit ${nunit_version} - MTP"
    "$exe"
    echo "::endgroup::"
}

run_leg "$floor_version" "matrix-floor"
run_leg "$current_stable_version" "matrix-current-stable"

echo "All blocking NUnit compatibility-matrix legs passed: ${floor_version} and ${current_stable_version}, each under classic VSTest and MTP."

# --- Non-blocking forward-compatibility surveillance (ADR-0059 §3) ---
# NUnit 5 is prerelease-only and outside the [3.14.0, 5.0.0) support contract - this leg is
# informational only and must never fail the job. Promote to a blocking `run_leg` call above once
# NUnit 5.0.0 ships stable and ADR-0059's range is amended.
surveillance_version="5.0.0-beta.1"
echo "::group::NUnit ${surveillance_version} - forward-compatibility surveillance (non-blocking)"
if run_leg "$surveillance_version" "matrix-surveillance"; then
    echo "Surveillance leg passed: NUnit ${surveillance_version} works today. No action needed."
else
    echo "::warning::NUnit ${surveillance_version} surveillance leg failed - not a blocking failure (NUnit 5 is outside the current [3.14.0, 5.0.0) support contract), but worth investigating before that range is ever widened."
fi
echo "::endgroup::"
