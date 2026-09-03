#!/usr/bin/env bash
# Regression coverage for inspect-packed-nupkgs.sh's authoritative-range lookup
# (issue #122): proves the validator derives its expected dependency range from
# Directory.Packages.props itself, not a hardcoded literal that Dependabot
# can't update. No test framework is set up for shell scripts in this repo, so
# this is a plain, dependency-free bash script - source the real script (its
# main() only runs when executed directly, not sourced) and exercise its
# functions directly against small fixture files.
set -euo pipefail

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck source=./inspect-packed-nupkgs.sh
source "$script_dir/inspect-packed-nupkgs.sh"

work_dir=$(mktemp -d)
trap 'rm -rf "$work_dir"' EXIT

tests_failed=0

make_props() {
    local path="$1"
    local dep_id="$2"
    local range="$3"
    cat >"$path" <<EOF
<Project>
  <ItemGroup>
    <PackageVersion Include="$dep_id" Version="$range" />
  </ItemGroup>
</Project>
EOF
}

make_nuspec() {
    local path="$1"
    local dep_id="$2"
    local range="$3"
    cat >"$path" <<EOF
<?xml version="1.0"?>
<package>
  <metadata>
    <dependencies>
      <group>
        <dependency id="$dep_id" version="$range" exclude="Build,Analyzers" />
      </group>
    </dependencies>
  </metadata>
</package>
EOF
}

expect_pass() {
    local description="$1"
    fail=0
    "${@:2}"
    if [ "$fail" -eq 0 ]; then
        echo "PASS: $description"
    else
        echo "TEST FAILURE: expected $description to pass, but it failed" >&2
        tests_failed=1
    fi
}

expect_fail() {
    local description="$1"
    fail=0
    "${@:2}" >/dev/null 2>&1 || true
    if [ "$fail" -ne 0 ]; then
        echo "PASS: $description"
    else
        echo "TEST FAILURE: expected $description to fail, but it passed" >&2
        tests_failed=1
    fi
}

# 1. Passing case: the packed nuspec matches Directory.Packages.props exactly.
props_current="$work_dir/props-current.props"
nuspec_matching="$work_dir/matching.nuspec"
make_props "$props_current" "TUnit.Core" "[1.65.63, 2.0.0)"
make_nuspec "$nuspec_matching" "TUnit.Core" "[1.65.63, 2.0.0)"
json_current=$(load_authoritative_versions "$props_current")
expect_pass "packed range matching current authoritative range" \
    assert_dependency_range "$nuspec_matching" "Compono.TUnit" "TUnit.Core" "$json_current"

# 2. Failing case: the packed nuspec disagrees with the authoritative range.
nuspec_stale="$work_dir/stale.nuspec"
make_nuspec "$nuspec_stale" "TUnit.Core" "[1.65.38, 2.0.0)"
expect_fail "packed range disagreeing with authoritative range" \
    assert_dependency_range "$nuspec_stale" "Compono.TUnit" "TUnit.Core" "$json_current"

# 3. The original bug class (issue #122): bump ONLY the authoritative props
# file (as Dependabot would) and re-pack with the SAME new range, with no
# validator literal touched anywhere - validation must still pass.
props_bumped="$work_dir/props-bumped.props"
nuspec_bumped="$work_dir/bumped.nuspec"
make_props "$props_bumped" "TUnit.Core" "[1.65.99, 2.0.0)"
make_nuspec "$nuspec_bumped" "TUnit.Core" "[1.65.99, 2.0.0)"
json_bumped=$(load_authoritative_versions "$props_bumped")
expect_pass "Dependabot-style bump: nuspec and Directory.Packages.props move together" \
    assert_dependency_range "$nuspec_bumped" "Compono.TUnit" "TUnit.Core" "$json_bumped"

# 4. A bump to the authoritative range that the packed nuspec does NOT reflect
# must still fail - this is not "always pass," the validator still compares
# independently against packed output.
nuspec_not_rebumped="$work_dir/not-rebumped.nuspec"
make_nuspec "$nuspec_not_rebumped" "TUnit.Core" "[1.65.63, 2.0.0)"
expect_fail "authoritative range bumped but packed nuspec left behind" \
    assert_dependency_range "$nuspec_not_rebumped" "Compono.TUnit" "TUnit.Core" "$json_bumped"

# 5. Distinct diagnostic: the authoritative lookup itself fails (no matching
# PackageVersion in Directory.Packages.props) - a validator/configuration
# error, not a "packed range mismatch".
props_missing="$work_dir/props-missing.props"
make_props "$props_missing" "SomeOtherPackage" "[1.0.0, 2.0.0)"
json_missing=$(load_authoritative_versions "$props_missing")
missing_output=$(fail=0; assert_dependency_range "$nuspec_matching" "Compono.TUnit" "TUnit.Core" "$json_missing" 2>&1 || true)
if echo "$missing_output" | grep -q "could not determine authoritative PackageVersion for TUnit.Core"; then
    echo "PASS: missing authoritative entry produces a distinct diagnostic"
else
    echo "TEST FAILURE: expected a distinct 'could not determine authoritative PackageVersion' message, got:" >&2
    echo "$missing_output" >&2
    tests_failed=1
fi

# 6a. assert_dependency_range_per_tfm: Compono.Logging's real shape - per-TFM ranges for net8/9/10,
# absent for net11.0 (satisfied by net11.0's own shared framework). Uses the real repository
# Directory.Packages.props (via dotnet msbuild -p:TargetFramework=X per TFM, the same mechanism the
# real function uses) rather than a synthetic fixture, since the whole point of this coverage is
# proving the function reads the *actual* per-TFM authoritative values correctly - a synthetic props
# file would just test that the function echoes back whatever synthetic value it was given.
make_multi_tfm_nuspec() {
    local path="$1"
    local dep_id="$2"
    local net8_range="$3"
    local net9_range="$4"
    local net10_range="$5"
    local net11_has_entry="$6" # "yes" or "no"
    local net11_line=""
    if [ "$net11_has_entry" = "yes" ]; then
        net11_line="        <dependency id=\"$dep_id\" version=\"[99.0.0, 100.0.0)\" exclude=\"Build,Analyzers\" />"
    fi
    cat >"$path" <<EOF
<?xml version="1.0"?>
<package>
  <metadata>
    <dependencies>
      <group targetFramework="net8.0">
        <dependency id="$dep_id" version="$net8_range" exclude="Build,Analyzers" />
      </group>
      <group targetFramework="net9.0">
        <dependency id="$dep_id" version="$net9_range" exclude="Build,Analyzers" />
      </group>
      <group targetFramework="net10.0">
        <dependency id="$dep_id" version="$net10_range" exclude="Build,Analyzers" />
      </group>
      <group targetFramework="net11.0">
$net11_line
      </group>
    </dependencies>
  </metadata>
</package>
EOF
}

repo_root="$script_dir/../.."
real_props="$repo_root/Directory.Packages.props"
real_net8=$(dotnet msbuild "$real_props" -nologo -getItem:PackageVersion -p:TargetFramework=net8.0 2>/dev/null | jq -r '.Items.PackageVersion[]? | select(.Identity == "Microsoft.Extensions.Logging.Abstractions") | .Version')
real_net9=$(dotnet msbuild "$real_props" -nologo -getItem:PackageVersion -p:TargetFramework=net9.0 2>/dev/null | jq -r '.Items.PackageVersion[]? | select(.Identity == "Microsoft.Extensions.Logging.Abstractions") | .Version')
real_net10=$(dotnet msbuild "$real_props" -nologo -getItem:PackageVersion -p:TargetFramework=net10.0 2>/dev/null | jq -r '.Items.PackageVersion[]? | select(.Identity == "Microsoft.Extensions.Logging.Abstractions") | .Version')

nuspec_per_tfm_matching="$work_dir/per-tfm-matching.nuspec"
make_multi_tfm_nuspec "$nuspec_per_tfm_matching" "Microsoft.Extensions.Logging.Abstractions" "$real_net8" "$real_net9" "$real_net10" "no"
expect_pass "per-TFM range matching Directory.Packages.props for net8/9/10, absent for net11.0" \
    assert_dependency_range_per_tfm "$nuspec_per_tfm_matching" "Compono.Logging" "Microsoft.Extensions.Logging.Abstractions" "$real_props"

# 6b. A stale net9.0 range must fail even though net8.0/net10.0 still match - proves each TFM is
# checked independently, not just "at least one matches".
nuspec_per_tfm_stale_net9="$work_dir/per-tfm-stale-net9.nuspec"
make_multi_tfm_nuspec "$nuspec_per_tfm_stale_net9" "Microsoft.Extensions.Logging.Abstractions" "$real_net8" "[0.0.1, 0.0.2)" "$real_net10" "no"
expect_fail "per-TFM check fails when only net9.0's range disagrees" \
    assert_dependency_range_per_tfm "$nuspec_per_tfm_stale_net9" "Compono.Logging" "Microsoft.Extensions.Logging.Abstractions" "$real_props"

# 6c. An unexpected net11.0 dependency entry must fail - proves the "must be absent" direction is
# actually checked, not merely unchecked.
nuspec_per_tfm_unexpected_net11="$work_dir/per-tfm-unexpected-net11.nuspec"
make_multi_tfm_nuspec "$nuspec_per_tfm_unexpected_net11" "Microsoft.Extensions.Logging.Abstractions" "$real_net8" "$real_net9" "$real_net10" "yes"
expect_fail "per-TFM check fails when net11.0 unexpectedly declares the dependency" \
    assert_dependency_range_per_tfm "$nuspec_per_tfm_unexpected_net11" "Compono.Logging" "Microsoft.Extensions.Logging.Abstractions" "$real_props"

# 6. Sanity check against the real repository policy file, so this test suite
# breaks if Directory.Packages.props' shape (Identity/Version JSON) ever stops
# being what the validator expects - independent of any specific package.
repo_root="$script_dir/../.."
real_json=$(load_authoritative_versions "$repo_root/Directory.Packages.props")
real_range=$(get_authoritative_range "$real_json" "NSubstitute")
if [ -n "$real_range" ]; then
    echo "PASS: authoritative lookup resolves a real range for NSubstitute from the repository's own Directory.Packages.props ($real_range)"
else
    echo "TEST FAILURE: could not resolve NSubstitute's range from the real Directory.Packages.props" >&2
    tests_failed=1
fi

if [ "$tests_failed" -ne 0 ]; then
    echo "One or more inspect-packed-nupkgs.sh regression tests failed." >&2
    exit 1
fi

echo "All inspect-packed-nupkgs.sh regression tests passed."
