#!/usr/bin/env bash
# ADR-0051/PLAN-0051 Task 5, Proof A: automated, CI-checkable assertion that
# RespondJson(value, JsonSerializerOptions?) surfaces IL2026 (RequiresUnreferencedCode) + IL3050
# (RequiresDynamicCode) warnings at an AOT/trim-checked CONSUMER's own call site (not swallowed
# inside Compono.Http), while RespondJson(value, JsonTypeInfo<T>) produces neither - the exact
# methodology run as a one-off spike in docs/research/0009-...md §12, now committed and repeatable.
# Extended by ADR-0062/PLAN-0065 Task 6 to prove the identical contract for WithJsonBody<T>'s own
# two overloads (WithJsonBodyOptionsOverloadCaller/WithJsonBodyTypeInfoOverloadCaller, below).
#
# This does NOT require a full `dotnet publish -p:PublishAot=true` - IL2026/IL3050 are build-time
# analyzer diagnostics, enabled here via each project's own <IsAotCompatible>true</IsAotCompatible>.
# Every caller project references Compono.Http via PackageReference (against the same local feed
# ../pack-compono.sh populates for Proof B), not ProjectReference - verified empirically that this
# matters: the trim/AOT analyzer only enforces a Requires* attribute at a consumer's call site for
# a member reached through a package reference, not a same-solution ProjectReference to the
# defining project's own compiled output (see OptionsOverloadCaller.csproj's own comment). See
# ../pack-compono.sh/Program.cs for the separate real Native-AOT publish-and-run proof (Proof B) -
# the two proofs are deliberately independent.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "verify-analyzer-contract.sh: packing Compono/Compono.Http into the local feed (../pack-compono.sh) ..."
bash "$script_dir/../pack-compono.sh"

options_project="$script_dir/OptionsOverloadCaller/OptionsOverloadCaller.csproj"
jsontypeinfo_project="$script_dir/JsonTypeInfoOverloadCaller/JsonTypeInfoOverloadCaller.csproj"
withjsonbody_options_project="$script_dir/WithJsonBodyOptionsOverloadCaller/WithJsonBodyOptionsOverloadCaller.csproj"
withjsonbody_typeinfo_project="$script_dir/WithJsonBodyTypeInfoOverloadCaller/WithJsonBodyTypeInfoOverloadCaller.csproj"

# ../pack-compono.sh only clears its own project's (Compono.Http.AotSmokeTest's) restore cache -
# every caller project here declares its own separate, isolated RestorePackagesPath (see each
# .csproj). Every pack republishes the fixed version 1.0.0, and NuGet treats an already-extracted
# id+version as immutable, so without clearing these too, a rerun after editing Compono.Http source
# could silently keep building both callers against a stale extracted copy and report a false PASS
# on the previous contract instead of the current one.
rm -rf "$script_dir/OptionsOverloadCaller/obj/.nuget-packages"
rm -rf "$script_dir/JsonTypeInfoOverloadCaller/obj/.nuget-packages"
rm -rf "$script_dir/WithJsonBodyOptionsOverloadCaller/obj/.nuget-packages"
rm -rf "$script_dir/WithJsonBodyTypeInfoOverloadCaller/obj/.nuget-packages"

echo "verify-analyzer-contract.sh: building OptionsOverloadCaller (expected to FAIL: IL2026+IL3050 as errors) ..."
if dotnet build "$options_project" -c Release --nologo -p:WarningsAsErrors="IL2026%3BIL3050" > "$script_dir/.options-build.log" 2>&1; then
    echo "verify-analyzer-contract.sh: FAIL - expected RespondJson(value, JsonSerializerOptions?) to" >&2
    echo "produce IL2026/IL3050 at its call site, but the build succeeded with no such error." >&2
    cat "$script_dir/.options-build.log" >&2
    exit 1
fi
if ! grep -q "IL2026" "$script_dir/.options-build.log" || ! grep -q "IL3050" "$script_dir/.options-build.log"; then
    echo "verify-analyzer-contract.sh: FAIL - OptionsOverloadCaller's build failed, but not with the" >&2
    echo "expected IL2026/IL3050 diagnostics. Build log:" >&2
    cat "$script_dir/.options-build.log" >&2
    exit 1
fi
echo "verify-analyzer-contract.sh: confirmed - RespondJson(value, JsonSerializerOptions?) surfaces IL2026+IL3050 at the consumer call site."

echo "verify-analyzer-contract.sh: building JsonTypeInfoOverloadCaller (expected to PASS: zero IL2026/IL3050) ..."
if ! dotnet build "$jsontypeinfo_project" -c Release --nologo -p:WarningsAsErrors="IL2026%3BIL3050" > "$script_dir/.jsontypeinfo-build.log" 2>&1; then
    echo "verify-analyzer-contract.sh: FAIL - expected RespondJson(value, JsonTypeInfo<T>) to build" >&2
    echo "cleanly with IL2026/IL3050 as errors, but the build failed. Build log:" >&2
    cat "$script_dir/.jsontypeinfo-build.log" >&2
    exit 1
fi
if grep -qE "IL2026|IL3050" "$script_dir/.jsontypeinfo-build.log"; then
    echo "verify-analyzer-contract.sh: FAIL - JsonTypeInfoOverloadCaller's build succeeded, but" >&2
    echo "still emitted an IL2026/IL3050 diagnostic somewhere - it should be warning-free. Build log:" >&2
    cat "$script_dir/.jsontypeinfo-build.log" >&2
    exit 1
fi
echo "verify-analyzer-contract.sh: confirmed - RespondJson(value, JsonTypeInfo<T>) produces zero IL2026/IL3050 warnings."

rm -f "$script_dir/.options-build.log" "$script_dir/.jsontypeinfo-build.log"

# ADR-0062/PLAN-0065 Task 6, Proof A: the identical contract, now for WithJsonBody<T>'s own two
# overloads - kept as its own pair of caller projects, never mixed into Proof B's clean smoke path
# (test/Compono.Http.AotSmokeTest/Program.cs), and never suppressed to force a false green here.
echo "verify-analyzer-contract.sh: building WithJsonBodyOptionsOverloadCaller (expected to FAIL: IL2026+IL3050 as errors) ..."
if dotnet build "$withjsonbody_options_project" -c Release --nologo -p:WarningsAsErrors="IL2026%3BIL3050" > "$script_dir/.withjsonbody-options-build.log" 2>&1; then
    echo "verify-analyzer-contract.sh: FAIL - expected WithJsonBody<T>(predicate, JsonSerializerOptions?)" >&2
    echo "to produce IL2026/IL3050 at its call site, but the build succeeded with no such error." >&2
    cat "$script_dir/.withjsonbody-options-build.log" >&2
    exit 1
fi
if ! grep -q "IL2026" "$script_dir/.withjsonbody-options-build.log" || ! grep -q "IL3050" "$script_dir/.withjsonbody-options-build.log"; then
    echo "verify-analyzer-contract.sh: FAIL - WithJsonBodyOptionsOverloadCaller's build failed, but not" >&2
    echo "with the expected IL2026/IL3050 diagnostics. Build log:" >&2
    cat "$script_dir/.withjsonbody-options-build.log" >&2
    exit 1
fi
echo "verify-analyzer-contract.sh: confirmed - WithJsonBody<T>(predicate, JsonSerializerOptions?) surfaces IL2026+IL3050 at the consumer call site."

echo "verify-analyzer-contract.sh: building WithJsonBodyTypeInfoOverloadCaller (expected to PASS: zero IL2026/IL3050) ..."
if ! dotnet build "$withjsonbody_typeinfo_project" -c Release --nologo -p:WarningsAsErrors="IL2026%3BIL3050" > "$script_dir/.withjsonbody-typeinfo-build.log" 2>&1; then
    echo "verify-analyzer-contract.sh: FAIL - expected WithJsonBody<T>(predicate, JsonTypeInfo<T>) to" >&2
    echo "build cleanly with IL2026/IL3050 as errors, but the build failed. Build log:" >&2
    cat "$script_dir/.withjsonbody-typeinfo-build.log" >&2
    exit 1
fi
if grep -qE "IL2026|IL3050" "$script_dir/.withjsonbody-typeinfo-build.log"; then
    echo "verify-analyzer-contract.sh: FAIL - WithJsonBodyTypeInfoOverloadCaller's build succeeded," >&2
    echo "but still emitted an IL2026/IL3050 diagnostic somewhere - it should be warning-free. Build log:" >&2
    cat "$script_dir/.withjsonbody-typeinfo-build.log" >&2
    exit 1
fi
echo "verify-analyzer-contract.sh: confirmed - WithJsonBody<T>(predicate, JsonTypeInfo<T>) produces zero IL2026/IL3050 warnings."

rm -f "$script_dir/.withjsonbody-options-build.log" "$script_dir/.withjsonbody-typeinfo-build.log"
echo "verify-analyzer-contract.sh: PASS - JSON/AOT attribute-propagation contract confirmed for both RespondJson and WithJsonBody<T> overload pairs."
