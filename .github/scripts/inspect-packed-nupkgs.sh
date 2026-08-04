#!/usr/bin/env bash
# Asserts the packed .nupkg contents for all four publishable Compono packages
# match ADR-0031's package-readiness bar (PLAN-0008 Phase 0's package-contents-
# inspection CI job): the .nupkg's file listing matches the expected shape
# exactly (an allowlist, not a denylist - nothing unexpected snuck in, not just
# "no known-bad pattern"), analyzers/dotnet/cs containing Compono.Generators.dll
# for Compono specifically (this is Compono.Generators' own verification - it's
# IsPackable=false per ADR-0003 and never gets an independent pack), an
# exact-pin <dependency> version on Compono for each integration package's
# .nuspec that matches that package's own version (not merely "some bracketed
# string"), and (per ADR-0031 Amendment 1) a deliberate tested range - not a
# bare unbounded floor, not a blanket exact pin - on each integration
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

assert_exact_file_listing() {
    local nupkg="$1"
    local pkg_name="$2"
    local extra_paths="$3"
    # An allowlist, not a denylist of known-bad patterns (*.pdb/obj/) - the prior
    # denylist would stay green if packing accidentally included something that
    # isn't a .pdb or under obj/ at all (a stray test DLL, a .deps.json, a leaked
    # runtime folder). DebugType=embedded (Directory.Build.props) means no
    # standalone .pdb ships; GenerateDocumentationFile=true means each TFM's .xml
    # doc file does. _rels/.rels, [Content_Types].xml, and
    # package/services/metadata/core-properties/nuget.psmdcp are NuGet's own
    # required OPC-package plumbing, present in every .nupkg regardless of content.
    local expected
    expected=$(cat <<EOF
_rels/.rels
${pkg_name}.nuspec
README.md
icon.png
lib/net10.0/${pkg_name}.dll
lib/net10.0/${pkg_name}.xml
lib/net11.0/${pkg_name}.dll
lib/net11.0/${pkg_name}.xml
[Content_Types].xml
package/services/metadata/core-properties/nuget.psmdcp
EOF
)
    if [ -n "$extra_paths" ]; then
        expected="${expected}"$'\n'"${extra_paths}"
    fi
    expected=$(echo "$expected" | sort)

    local actual
    actual=$(unzip -Z1 "$nupkg" | sort)

    if [ "$actual" = "$expected" ]; then
        echo "OK: $pkg_name's .nupkg file listing matches the expected shape exactly"
    else
        echo "FAIL: $pkg_name's .nupkg file listing doesn't match the expected shape:" >&2
        diff <(echo "$expected") <(echo "$actual") | sed 's/^/  /' >&2 || true
        fail=1
    fi
}

assert_exact_pin_dependency() {
    local nuspec="$1"
    local pkg_name="$2"
    local dep_id="$3"
    # Lockstep (ADR-0031) means the Compono dependency must equal this integration
    # package's *own* packed version exactly - not merely "some bracketed string" (a
    # regex like ^\[.*\]$ would wrongly accept a stale pin, e.g. [0.9.0] inside a
    # 1.0.0 package, or an inclusive range like [0.9.0,1.0.0]).
    local own_version
    own_version=$(sed -n 's#.*<version>\(.*\)</version>.*#\1#p' "$nuspec" | head -1)
    local expected="[${own_version}]"
    local version
    version=$(grep -o "id=\"${dep_id}\" version=\"[^\"]*\"" "$nuspec" | head -1 | sed -E 's/.*version="([^"]*)".*/\1/')
    if [ "$version" = "$expected" ]; then
        echo "OK: $pkg_name's .nuspec pins $dep_id at exact version $version, matching its own package version"
    else
        echo "FAIL: $pkg_name's .nuspec dependency on $dep_id is '$version', expected an exact pin matching its own package version: '$expected'" >&2
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

    extra_paths=""
    if [ "$pkg" = "Compono" ]; then
        extra_paths="analyzers/dotnet/cs/Compono.Generators.dll"
    fi
    assert_exact_file_listing "$nupkg" "$pkg" "$extra_paths"

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
