#!/usr/bin/env bash
# Asserts the packed .nupkg contents for all four publishable Compono packages
# match ADR-0031's package-readiness bar (PLAN-0008 Phase 0's package-contents-
# inspection CI job): lib/README/icon per TFM, no stray build artifacts,
# analyzers/dotnet/cs containing Compono.Generators.dll for Compono
# specifically (this is Compono.Generators' own verification - it's
# IsPackable=false per ADR-0003 and never gets an independent pack), and an
# exact-pin (bracketed) <dependency> version on Compono for each integration
# package's .nuspec, and (per ADR-0031 Amendment 1) a deliberate tested range
# - not a bare unbounded floor, not a blanket exact pin - on each integration
# package's third-party dependency.
set -euo pipefail

pack_output="${1:?usage: inspect-packed-nupkgs.sh <pack-output-dir>}"
work_dir=$(mktemp -d)
trap 'rm -rf "$work_dir"' EXIT

fail=0

extract() {
    local nupkg="$1"
    local dest="$2"
    mkdir -p "$dest"
    unzip -q -o "$nupkg" -d "$dest"
}

assert_exists() {
    local path="$1"
    local description="$2"
    if [ ! -e "$path" ]; then
        echo "FAIL: $description missing (expected at $path)" >&2
        fail=1
    else
        echo "OK: $description"
    fi
}

assert_no_stray_artifacts() {
    local extracted_dir="$1"
    local pkg_name="$2"
    # DebugType=embedded (Directory.Build.props) means no standalone .pdb should ship.
    local strays
    strays=$(find "$extracted_dir" -iname "*.pdb" -o -ipath "*/obj/*")
    if [ -n "$strays" ]; then
        echo "FAIL: $pkg_name contains stray build artifacts:" >&2
        echo "$strays" >&2
        fail=1
    else
        echo "OK: $pkg_name has no stray build artifacts"
    fi
}

assert_exact_pin_dependency() {
    local nuspec="$1"
    local pkg_name="$2"
    local dep_id="$3"
    local version
    version=$(grep -o "id=\"${dep_id}\" version=\"[^\"]*\"" "$nuspec" | head -1 | sed -E 's/.*version="([^"]*)".*/\1/')
    if [[ "$version" =~ ^\[.*\]$ ]]; then
        echo "OK: $pkg_name's .nuspec pins $dep_id at exact version $version"
    else
        echo "FAIL: $pkg_name's .nuspec dependency on $dep_id is '$version', not an exact-pin bracket like [x.y.z]" >&2
        fail=1
    fi
}

assert_manifest_field() {
    local nuspec="$1"
    local pkg_name="$2"
    local field="$3"
    local expected="$4"
    local actual
    actual=$(sed -n "s#.*<${field}>\(.*\)</${field}>.*#\1#p" "$nuspec" | head -1)
    if [ "$actual" = "$expected" ]; then
        echo "OK: $pkg_name's .nuspec <$field> is '$actual'"
    else
        echo "FAIL: $pkg_name's .nuspec <$field> is '$actual', expected '$expected'" >&2
        fail=1
    fi
}

assert_dependency_range() {
    local nuspec="$1"
    local pkg_name="$2"
    local dep_id="$3"
    local expected_range="$4"
    local version
    version=$(grep -o "id=\"${dep_id}\" version=\"[^\"]*\"" "$nuspec" | head -1 | sed -E 's/.*version="([^"]*)".*/\1/')
    if [ "$version" = "$expected_range" ]; then
        echo "OK: $pkg_name's .nuspec constrains $dep_id to the intended tested range $version"
    else
        echo "FAIL: $pkg_name's .nuspec dependency on $dep_id is '$version', expected the intended tested range '$expected_range'" >&2
        fail=1
    fi
}

for pkg in Compono Compono.XunitV3 Compono.NSubstitute Compono.Bogus; do
    nupkg=$(find "$pack_output" -maxdepth 1 -iname "${pkg}.[0-9]*.nupkg" | head -1)
    if [ -z "$nupkg" ]; then
        echo "FAIL: no .nupkg found for $pkg in $pack_output" >&2
        fail=1
        continue
    fi

    extract_dir="$work_dir/$pkg"
    extract "$nupkg" "$extract_dir"

    assert_exists "$extract_dir/lib/net10.0/${pkg}.dll" "$pkg lib/net10.0"
    assert_exists "$extract_dir/lib/net11.0/${pkg}.dll" "$pkg lib/net11.0"
    assert_exists "$extract_dir/README.md" "$pkg README.md"
    assert_exists "$extract_dir/icon.png" "$pkg icon.png"
    assert_no_stray_artifacts "$extract_dir" "$pkg"

    nuspec=$(find "$extract_dir" -maxdepth 1 -iname "*.nuspec" | head -1)
    assert_exists "${nuspec:-__missing__}" "$pkg .nuspec"

    # Directory.Build.props' centralized discovery metadata (ADR-0031) - shared across
    # all five packages, so checked once per package here rather than only trusting the
    # .nuspec exists. PackageTags' semicolon-separated MSBuild value becomes
    # space-separated in the packed .nuspec's <tags>.
    assert_manifest_field "$nuspec" "$pkg" "tags" "testing test-data source-generator dotnet"
    assert_manifest_field "$nuspec" "$pkg" "releaseNotes" "https://github.com/LayeredCraft/compono/releases"

    case "$pkg" in
        Compono)
            assert_manifest_field "$nuspec" "$pkg" "title" "Compono — Core Composition Engine"
            assert_exists "$extract_dir/analyzers/dotnet/cs/Compono.Generators.dll" "Compono.Generators.dll embedded in Compono's analyzers/dotnet/cs"
            ;;
        Compono.XunitV3)
            assert_manifest_field "$nuspec" "$pkg" "title" "Compono — xUnit v3 Integration"
            assert_exact_pin_dependency "$nuspec" "$pkg" "Compono"
            assert_dependency_range "$nuspec" "$pkg" "xunit.v3.extensibility.core" "[3.2.2, 4.0.0)"
            ;;
        Compono.NSubstitute)
            assert_manifest_field "$nuspec" "$pkg" "title" "Compono — NSubstitute Integration"
            assert_exact_pin_dependency "$nuspec" "$pkg" "Compono"
            assert_dependency_range "$nuspec" "$pkg" "NSubstitute" "[6.0.0, 7.0.0)"
            ;;
        Compono.Bogus)
            assert_manifest_field "$nuspec" "$pkg" "title" "Compono — Bogus Integration"
            assert_exact_pin_dependency "$nuspec" "$pkg" "Compono"
            assert_dependency_range "$nuspec" "$pkg" "Bogus" "[35.6.5, 36.0.0)"
            ;;
    esac
done

if [ "$fail" -ne 0 ]; then
    echo "One or more package-contents assertions failed." >&2
    exit 1
fi

echo "All package-contents assertions passed."
