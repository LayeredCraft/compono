#!/usr/bin/env bash
# Asserts the packed .nupkg contents for all seven publishable Compono packages
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
# package's third-party dependency, read from Directory.Packages.props itself
# (issue #122) rather than a second hardcoded literal that Dependabot can't see.
set -euo pipefail

fail=0

# Evaluates Directory.Packages.props' <PackageVersion> items via `dotnet msbuild
# -getItem` (SDK-native, already a CI/dev dependency - no new tool) and returns
# the JSON verbatim. This is the one authoritative read of repository policy;
# every assert_dependency_range call below looks up its expected range from
# this same JSON rather than a duplicated string literal, so a Dependabot bump
# to Directory.Packages.props is automatically the new expected value.
load_authoritative_versions() {
    local props_file="$1"
    dotnet msbuild "$props_file" -nologo -getItem:PackageVersion
}

# Looks up a single dependency's authoritative version/range by its
# Directory.Packages.props <PackageVersion Include="..."> identity, from the
# JSON produced by load_authoritative_versions. Returns empty (not an error)
# when the id isn't found - callers must check for that distinctly from a
# packed-range mismatch, since "policy lookup failed" and "packed output is
# wrong" are different classes of failure (issue #122, point 10).
get_authoritative_range() {
    local authoritative_json="$1"
    local dep_id="$2"
    jq -r --arg id "$dep_id" \
        '.Items.PackageVersion[]? | select(.Identity == $id) | .Version' \
        <<<"$authoritative_json" | head -1
}

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
lib/net8.0/${pkg_name}.dll
lib/net8.0/${pkg_name}.xml
lib/net9.0/${pkg_name}.dll
lib/net9.0/${pkg_name}.xml
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
    local authoritative_json="$4"
    local expected_range
    expected_range=$(get_authoritative_range "$authoritative_json" "$dep_id")
    if [ -z "$expected_range" ]; then
        # A distinct failure class from a packed-range mismatch below (issue #122,
        # point 10): this means Directory.Packages.props itself has no
        # <PackageVersion Include="$dep_id">, a validator/configuration problem,
        # not evidence about what got packed.
        echo "FAIL: could not determine authoritative PackageVersion for $dep_id in Directory.Packages.props" >&2
        fail=1
        return
    fi
    local version
    version=$(grep -o "id=\"${dep_id}\" version=\"[^\"]*\"" "$nuspec" | head -1 | sed -E 's/.*version="([^"]*)".*/\1/')
    if [ "$version" = "$expected_range" ]; then
        echo "OK: $pkg_name's .nuspec constrains $dep_id to the intended tested range $version (matches Directory.Packages.props)"
    else
        echo "FAIL: $pkg_name's .nuspec dependency on $dep_id is '$version', expected the intended tested range '$expected_range' (from Directory.Packages.props)" >&2
        fail=1
    fi
}

main() {
    local pack_output="${1:?usage: inspect-packed-nupkgs.sh <pack-output-dir>}"
    local script_dir
    script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
    local packages_props="$script_dir/../../Directory.Packages.props"
    # Not `local`: the EXIT trap below still needs to see this after main()
    # itself returns and the script falls off the end (bash pops local scope
    # on function return, before the EXIT trap runs).
    work_dir=$(mktemp -d)
    trap 'rm -rf "$work_dir"' EXIT

    local authoritative_json
    authoritative_json=$(load_authoritative_versions "$packages_props") || {
        echo "FAIL: could not evaluate $packages_props via dotnet msbuild" >&2
        exit 1
    }

    local pkg nupkg extract_dir extra_paths nuspec
    for pkg in Compono Compono.XunitV3 Compono.NSubstitute Compono.Bogus Compono.TUnit Compono.TestDoubles Compono.DependencyInjection Compono.Http Compono.MSTest; do
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
        # build/ + buildTransitive/ Compono.props: the CompilerVisibleProperty declaration for
        # ComponoGeneratedTestDoubles (ADR-0043 Amendment 4, Finding F) - without it,
        # AnalyzerConfigOptionsProvider can never see a consumer's MSBuild setting for the opt-in.
        extra_paths=$'analyzers/dotnet/cs/Compono.Generators.dll\nbuild/Compono.props\nbuildTransitive/Compono.props'
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
            assert_dependency_range "$nuspec" "$pkg" "xunit.v3.extensibility.core" "$authoritative_json"
            ;;
        Compono.NSubstitute)
            assert_manifest_field "$nuspec" "$pkg" "title" "Compono — NSubstitute Integration"
            assert_exact_pin_dependency "$nuspec" "$pkg" "Compono"
            assert_dependency_range "$nuspec" "$pkg" "NSubstitute" "$authoritative_json"
            ;;
        Compono.Bogus)
            assert_manifest_field "$nuspec" "$pkg" "title" "Compono — Bogus Integration"
            assert_exact_pin_dependency "$nuspec" "$pkg" "Compono"
            assert_dependency_range "$nuspec" "$pkg" "Bogus" "$authoritative_json"
            ;;
        Compono.TUnit)
            assert_manifest_field "$nuspec" "$pkg" "title" "Compono — TUnit Integration"
            assert_exact_pin_dependency "$nuspec" "$pkg" "Compono"
            assert_dependency_range "$nuspec" "$pkg" "TUnit.Core" "$authoritative_json"
            ;;
        Compono.TestDoubles)
            assert_manifest_field "$nuspec" "$pkg" "title" "Compono — Generated Test Doubles"
            assert_exact_pin_dependency "$nuspec" "$pkg" "Compono"
            ;;
        Compono.DependencyInjection)
            assert_manifest_field "$nuspec" "$pkg" "title" "Compono — Dependency Injection Bridge"
            assert_exact_pin_dependency "$nuspec" "$pkg" "Compono"
            # No third-party dependency: row.AsServiceProvider() returns a plain
            # System.IServiceProvider (BCL) - nothing else to range-assert here (ADR-0047
            # Amendment 1).
            ;;
        Compono.Http)
            assert_manifest_field "$nuspec" "$pkg" "title" "Compono — HTTP Client Testing"
            assert_exact_pin_dependency "$nuspec" "$pkg" "Compono"
            # No third-party dependency: TestHttpHandler is a plain HttpMessageHandler subclass
            # over System.Net.Http (BCL) - nothing else to range-assert here (ADR-0051 "Minimal
            # dependency graph").
            ;;
        Compono.MSTest)
            assert_manifest_field "$nuspec" "$pkg" "title" "Compono — MSTest Integration"
            assert_exact_pin_dependency "$nuspec" "$pkg" "Compono"
            assert_dependency_range "$nuspec" "$pkg" "MSTest.TestFramework" "$authoritative_json"
            ;;
    esac
    done

    if [ "$fail" -ne 0 ]; then
        echo "One or more package-contents assertions failed." >&2
        exit 1
    fi

    echo "All package-contents assertions passed."
}

# Sourced (by the regression-test script) vs. executed directly: only run main
# when this file is the actual entry point, so tests can source it to reach
# the functions above without triggering a real pack-output scan.
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi
