#!/usr/bin/env bash
# dogfood-validate.sh
#
# Packs Compono, Compono.NSubstitute, Compono.TestDoubles, and Compono.XunitV3 from the CURRENT
# working tree into a local NuGet feed under a throwaway prerelease version, then restores and
# tests a CONSUMER repo (a real dogfood project pinned to Compono via NuGet Central Package
# Management) against that freshly-packed version - without ever editing the consumer's real
# Directory.Packages.props.
#
# Why this exists: before a nontrivial change to Compono's generators lands in a PR, we want to
# prove it against a real consumer's real test suite using the actual packaged artifact (analyzers,
# CompilerVisibleProperty declarations, PrivateAssets, etc. - not a ProjectReference, which would
# mask packaging mistakes) rather than trusting Compono's own unit tests alone. This mirrors the
# reasoning behind the six existing per-sample-project pack-to-local-feed.sh scripts (see e.g.
# test/Compono.TestDoubles.SampleTests/pack-to-local-feed.sh) but targets an external consumer repo
# instead of an in-repo sample project, and runs on demand rather than as an MSBuild pre-restore
# target.
#
# The no-edit CPM override mechanism: NuGet's Central Package Management support resolves which
# Directory.Packages.props file to import via the MSBuild property $(DirectoryPackagesPropsPath)
# (see NuGet.props in the .NET SDK - it only auto-computes that path by walking up from the project
# directory when the property isn't already set). Passing -p:DirectoryPackagesPropsPath=<path to a
# generated temp copy> on `dotnet restore`/`dotnet test` points MSBuild at OUR temp file - with the
# four Compono package versions swapped to this run's local version - instead of the consumer's real
# file. The consumer's actual Directory.Packages.props is never opened for writing at any point in
# this script. Verified empirically against trivia-platform (LayeredCraft's dogfood consumer): the
# resolved version in project.assets.json exactly matched the generated local version after a
# restore using this override, and the consumer's `git status --porcelain` was byte-identical before
# and after.
#
# One caveat discovered empirically: -p:DirectoryPackagesPropsPath works for both `dotnet restore`
# and `dotnet test`, but `--configfile` (used to point restore at our local feed + nuget.org without
# touching the consumer's own NuGet source configuration) is a restore-only flag. Passing it to
# `dotnet test` on a project using the Microsoft Testing Platform (MTP) runner fails hard - MTP
# forwards unrecognized dotnet-test options straight through to the test executable's own CLI parser
# ("Unknown option '--configfile'"), which is not the same as a NuGet-level flag. So: `--configfile`
# is passed to the explicit `dotnet restore` step only; the later `dotnet test --no-restore` step
# relies on that already-completed restore and doesn't need it.
set -euo pipefail

# ---------------------------------------------------------------------------------------------
# Configuration (env var, overridable by matching CLI flag)
# ---------------------------------------------------------------------------------------------

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"

feed_dir="${DOGFOOD_FEED_DIR:-$repo_root/.local-nuget-feed-dogfood}"
consumer_repo="${DOGFOOD_CONSUMER_REPO:-/Users/ncipollina/source/repos/ncipollina/trivia-platform}"
consumer_solution="${DOGFOOD_CONSUMER_SOLUTION:-}"
configuration="${DOGFOOD_CONFIGURATION:-Release}"

usage() {
    cat <<EOF
Usage: $(basename "$0") [options]

Packs Compono/Compono.NSubstitute/Compono.TestDoubles/Compono.XunitV3 from the current working
tree into a local NuGet feed, then restores and runs the full test suite of a consumer repo
against that freshly-packed version (no edits to the consumer's own Directory.Packages.props).

Options:
  --feed-dir <path>          Local NuGet feed directory (default: DOGFOOD_FEED_DIR env var, or
                              $repo_root/.local-nuget-feed-dogfood)
  --consumer-repo <path>     Path to the consumer repo to restore/test (default:
                              DOGFOOD_CONSUMER_REPO env var, or the trivia-platform dogfood repo)
  --consumer-solution <path> Solution/slnx file within the consumer repo to restore/test
                              (default: DOGFOOD_CONSUMER_SOLUTION env var, or auto-detected if
                              exactly one *.slnx or *.sln exists at the consumer repo root)
  --configuration <config>   Build configuration used for both pack and test (default: Release)
  -h, --help                 Show this help text

Exit code is 0 only if packing, restore, version verification, and the consumer's full test suite
all succeed. The consumer repo's git working tree is left exactly as it was found, regardless of
outcome.
EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        --feed-dir)
            feed_dir="$2"
            shift 2
            ;;
        --consumer-repo)
            consumer_repo="$2"
            shift 2
            ;;
        --consumer-solution)
            consumer_solution="$2"
            shift 2
            ;;
        --configuration)
            configuration="$2"
            shift 2
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "dogfood-validate.sh: unknown argument '$1'" >&2
            usage >&2
            exit 1
            ;;
    esac
done

if [ -z "$consumer_repo" ]; then
    echo "dogfood-validate.sh: no consumer repo configured. Pass --consumer-repo or set DOGFOOD_CONSUMER_REPO." >&2
    exit 1
fi
if [ ! -d "$consumer_repo" ]; then
    echo "dogfood-validate.sh: consumer repo '$consumer_repo' does not exist or is not a directory." >&2
    exit 1
fi
consumer_repo="$(cd "$consumer_repo" && pwd)"

if [ ! -f "$consumer_repo/Directory.Packages.props" ]; then
    echo "dogfood-validate.sh: '$consumer_repo/Directory.Packages.props' not found - this script" >&2
    echo "only supports a consumer repo using NuGet Central Package Management." >&2
    exit 1
fi

if [ -z "$consumer_solution" ]; then
    candidates=()
    while IFS= read -r line; do
        candidates+=("$line")
    done < <(find "$consumer_repo" -maxdepth 1 \( -iname "*.slnx" -o -iname "*.sln" \) | sort)
    if [ "${#candidates[@]}" -eq 0 ]; then
        echo "dogfood-validate.sh: no *.slnx/*.sln found at the root of '$consumer_repo'. Pass --consumer-solution." >&2
        exit 1
    elif [ "${#candidates[@]}" -gt 1 ]; then
        echo "dogfood-validate.sh: multiple solution files found at the root of '$consumer_repo':" >&2
        printf '  %s\n' "${candidates[@]}" >&2
        echo "Pass --consumer-solution to disambiguate." >&2
        exit 1
    fi
    consumer_solution="${candidates[0]}"
elif [[ "$consumer_solution" != /* ]]; then
    consumer_solution="$consumer_repo/$consumer_solution"
fi
if [ ! -f "$consumer_solution" ]; then
    echo "dogfood-validate.sh: consumer solution '$consumer_solution' does not exist." >&2
    exit 1
fi

echo "dogfood-validate.sh: consumer repo:      $consumer_repo"
echo "dogfood-validate.sh: consumer solution:  $consumer_solution"
echo "dogfood-validate.sh: feed dir:           $feed_dir"
echo "dogfood-validate.sh: configuration:      $configuration"

# ---------------------------------------------------------------------------------------------
# Safety net: prove (and, if it were ever somehow dirtied, restore) the consumer repo's git tree.
# This script never opens the consumer's own Directory.Packages.props for writing - CPM version
# pinning happens entirely through a generated temp copy referenced via -p:DirectoryPackagesPropsPath
# - but this trap exists as an unconditional last line of defense on ANY exit path (success,
# failure, or interrupt), per the same reasoning as the fallback the task asked for if the no-edit
# mechanism hadn't worked.
# ---------------------------------------------------------------------------------------------

work_tmp_dir=""
lock_dir=""
lock_owned=0
consumer_status_before_file=""
consumer_packages_props_backup=""

cleanup() {
    local exit_code=$?

    # Only release the lock if THIS process actually acquired it. If we timed out waiting (or were
    # interrupted before acquiring), `lock_dir` is already set for messaging purposes but owned by
    # some other still-running process - removing it here would let a third validator race into
    # packing concurrently against the same src/Compono*/bin/obj outputs. Codex review, PR #108
    # (round 2).
    if [ "$lock_owned" -eq 1 ] && [ -n "$lock_dir" ] && [ -d "$lock_dir" ]; then
        rmdir "$lock_dir" 2>/dev/null || true
    fi

    if [ -n "$consumer_status_before_file" ] && [ -f "$consumer_status_before_file" ]; then
        local status_after
        status_after="$(cd "$consumer_repo" && git status --porcelain)"
        if [ "$status_after" != "$(cat "$consumer_status_before_file")" ]; then
            echo "dogfood-validate.sh: WARNING - consumer repo git status changed during this run;" >&2
            echo "restoring Directory.Packages.props to its pre-run content as a safety net." >&2
            # Restore from the byte-for-byte snapshot taken before this run, NOT `git checkout`
            # (which resets to committed HEAD) - the consumer repo may legitimately have had its
            # own uncommitted edits to this exact file before this script ever ran (e.g. a version
            # pin bump in progress), and `git checkout` would silently discard that real work
            # instead of restoring the pre-run state. Codex review, PR #108 (round 1).
            if [ -n "$consumer_packages_props_backup" ] && [ -f "$consumer_packages_props_backup" ]; then
                cp "$consumer_packages_props_backup" "$consumer_repo/Directory.Packages.props"
            fi
            local status_restored
            status_restored="$(cd "$consumer_repo" && git status --porcelain)"
            if [ "$status_restored" != "$(cat "$consumer_status_before_file")" ]; then
                echo "dogfood-validate.sh: ERROR - consumer repo git status still differs after safety-net restore." >&2
                echo "--- before ---" >&2
                cat "$consumer_status_before_file" >&2
                echo "--- after ---" >&2
                echo "$status_restored" >&2
                # The restore only covers Directory.Packages.props - if some OTHER tracked file
                # (e.g. packages.lock.json) also changed and can't be accounted for, the consumer
                # repo is left dirty. Reporting success in that case would be a false-green gate.
                # Codex review, PR #108 (round 2).
                exit_code=1
            else
                echo "dogfood-validate.sh: safety-net restore succeeded; consumer repo git status matches pre-run state." >&2
            fi
        fi
    fi

    if [ -n "$work_tmp_dir" ] && [ -d "$work_tmp_dir" ]; then
        rm -rf "$work_tmp_dir"
    fi

    exit "$exit_code"
}
trap cleanup EXIT

# ---------------------------------------------------------------------------------------------
# Step 0: record the consumer repo's git status before touching anything.
# ---------------------------------------------------------------------------------------------

work_tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/dogfood-validate.XXXXXX")"
consumer_status_before_file="$work_tmp_dir/consumer-status-before.txt"
(cd "$consumer_repo" && git status --porcelain) > "$consumer_status_before_file"
consumer_packages_props_backup="$work_tmp_dir/Directory.Packages.props.before"
cp "$consumer_repo/Directory.Packages.props" "$consumer_packages_props_backup"

# ---------------------------------------------------------------------------------------------
# Step 1: generate a unique local prerelease version. 0.0.0 sorts below every real 0.x release
# regardless of prerelease label; the timestamp plus PID plus $RANDOM suffix guarantees uniqueness
# even across quick successive runs (a bare seconds-granularity timestamp alone would not).
# ---------------------------------------------------------------------------------------------

version="0.0.0-local.$(date +%Y%m%d%H%M%S)-$$-$RANDOM"
echo "dogfood-validate.sh: local package version: $version"

# ---------------------------------------------------------------------------------------------
# Step 2: pack the four packages from the current working tree into the local feed, serialized
# behind a cross-process mkdir lock. Deliberately at a fixed repo-root path, NOT under $feed_dir -
# the actual shared resource two concurrent packers can corrupt is src/Compono*/bin/obj, which is
# the same regardless of which feed directory either caller targets, so the lock needs to be keyed
# on the repo, not on the (possibly different) output directory. This does not coordinate with the
# existing per-sample-project pack-to-local-feed.sh scripts (each locks under its own project-local
# feed dir, a pre-existing repo-wide pattern this script doesn't change) - only with concurrent runs
# of dogfood-validate.sh itself. Codex review, PR #108 (round 3): the prior comment here overclaimed
# coordination with the sample scripts, which this lock never actually provided.
# ---------------------------------------------------------------------------------------------

mkdir -p "$feed_dir"
lock_dir="$repo_root/.pack.lock"

max_wait_attempts=120
attempt=0
until mkdir "$lock_dir" 2>/dev/null; do
    attempt=$((attempt + 1))
    if [ "$attempt" -ge "$max_wait_attempts" ]; then
        echo "dogfood-validate.sh: timed out waiting for the lock at '$lock_dir'." >&2
        echo "If no other dogfood-validate.sh/pack run is in progress, remove it manually: rm -rf '$lock_dir'" >&2
        exit 1
    fi
    sleep 1
done
lock_owned=1

packages=(Compono Compono.NSubstitute Compono.TestDoubles Compono.XunitV3)
for pkg in "${packages[@]}"; do
    csproj="$repo_root/src/$pkg/$pkg.csproj"
    if [ ! -f "$csproj" ]; then
        echo "dogfood-validate.sh: expected project file not found: $csproj" >&2
        exit 1
    fi
    echo "dogfood-validate.sh: packing $pkg @ $version ..."
    dotnet pack "$csproj" -c "$configuration" -o "$feed_dir" -p:Version="$version" --nologo
done

rmdir "$lock_dir"
lock_dir=""
lock_owned=0

for pkg in "${packages[@]}"; do
    if [ ! -f "$feed_dir/$pkg.$version.nupkg" ]; then
        echo "dogfood-validate.sh: expected package not found after pack: $feed_dir/$pkg.$version.nupkg" >&2
        exit 1
    fi
done
echo "dogfood-validate.sh: all four packages packed into $feed_dir"

# ---------------------------------------------------------------------------------------------
# Step 3/4: generate a temp nuget.config (local feed + nuget.org) and a temp Directory.Packages.props
# copy with the four Compono package versions swapped to $version - the consumer's real
# Directory.Packages.props is never opened for writing. Restore is pointed at both via
# --configfile and -p:DirectoryPackagesPropsPath.
# ---------------------------------------------------------------------------------------------

dogfood_nuget_config="$work_tmp_dir/nuget.config"
cat > "$dogfood_nuget_config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="dogfood-local" value="$feed_dir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF

dogfood_packages_props="$work_tmp_dir/Directory.Packages.props"
cp "$consumer_repo/Directory.Packages.props" "$dogfood_packages_props"
for pkg in "${packages[@]}"; do
    sed -i.bak -E "s#(<PackageVersion Include=\"$pkg\" Version=\")[^\"]+(\")#\1$version\2#" "$dogfood_packages_props"
    rm -f "$dogfood_packages_props.bak"
done

for pkg in "${packages[@]}"; do
    if ! grep -qF "<PackageVersion Include=\"$pkg\" Version=\"$version\" />" "$dogfood_packages_props"; then
        echo "dogfood-validate.sh: failed to pin $pkg to $version in the generated Directory.Packages.props copy." >&2
        echo "Does '$consumer_repo/Directory.Packages.props' have a <PackageVersion Include=\"$pkg\" .../> entry?" >&2
        exit 1
    fi
done
echo "dogfood-validate.sh: generated temp CPM override at $dogfood_packages_props (consumer's real file untouched)"

echo "dogfood-validate.sh: restoring $consumer_solution ..."
dotnet restore "$consumer_solution" \
    --configfile "$dogfood_nuget_config" \
    -p:DirectoryPackagesPropsPath="$dogfood_packages_props"

# ---------------------------------------------------------------------------------------------
# Step 5: anti-stale-cache assertion - grep every restored project's project.assets.json for the
# four package ids and assert every resolved version equals $version.
# ---------------------------------------------------------------------------------------------

echo "dogfood-validate.sh: verifying resolved package versions ..."
assets_files=()
while IFS= read -r line; do
    assets_files+=("$line")
done < <(find "$consumer_repo" -type f -name project.assets.json -not -path "*/node_modules/*")
if [ "${#assets_files[@]}" -eq 0 ]; then
    echo "dogfood-validate.sh: no project.assets.json files found under '$consumer_repo' after restore." >&2
    exit 1
fi

# Tracks, per package (not a single shared flag), whether it was found anywhere - a consumer that
# only references e.g. Compono.TestDoubles must not let that one hit silently satisfy the check for
# Compono/Compono.NSubstitute/Compono.XunitV3 too. Plain indexed array parallel to $packages, NOT
# an associative array (`declare -A`) - macOS's system /bin/bash is 3.2, which predates bash 4's
# associative-array support (same constraint noted above for `mapfile`). Codex review, PR #108
# (round 3).
pkg_found=()
for pkg in "${packages[@]}"; do
    pkg_found+=(0)
done
mismatch=0
pkg_index=0
for pkg in "${packages[@]}"; do
    for f in "${assets_files[@]}"; do
        # Matches lines like:  "Compono/0.0.0-local...": {   or   "Compono.TestDoubles/1.2.3": {
        matches="$(grep -oE "\"$pkg/[^\"]+\"" "$f" | sed -E 's#^"'"$pkg"'/##; s#"$##' || true)"
        for resolved in $matches; do
            pkg_found[$pkg_index]=1
            if [ "$resolved" != "$version" ]; then
                echo "dogfood-validate.sh: STALE VERSION - $f resolved $pkg @ $resolved, expected $version" >&2
                mismatch=1
            fi
        done
    done
    pkg_index=$((pkg_index + 1))
done

missing_packages=()
pkg_index=0
for pkg in "${packages[@]}"; do
    if [ "${pkg_found[$pkg_index]}" -eq 0 ]; then
        missing_packages+=("$pkg")
    fi
    pkg_index=$((pkg_index + 1))
done
if [ "${#missing_packages[@]}" -gt 0 ]; then
    echo "dogfood-validate.sh: the following packed package(s) were not found in any" >&2
    echo "project.assets.json under '$consumer_repo', so this run did not validate them:" >&2
    printf '  %s\n' "${missing_packages[@]}" >&2
    echo "Check the consumer repo's csproj files, or pass --consumer-solution to target a solution" >&2
    echo "that actually references all four packages." >&2
    exit 1
fi
if [ "$mismatch" -ne 0 ]; then
    echo "dogfood-validate.sh: resolved version mismatch detected - see STALE VERSION lines above." >&2
    exit 1
fi
echo "dogfood-validate.sh: confirmed - every resolved Compono/Compono.NSubstitute/Compono.TestDoubles/Compono.XunitV3 reference resolves to $version"

# ---------------------------------------------------------------------------------------------
# Step 6: run the full consumer test suite and propagate its exit code.
#
# NOTE: --configfile is NOT passed here (deliberately) - it's a restore-only flag, and on a
# Microsoft Testing Platform (MTP) test project, `dotnet test` forwards unrecognized options
# straight to the test executable's own CLI parser, which rejects it outright. Restore already
# happened in the step above with --configfile, so the build/test step below doesn't need it -
# it only still needs -p:DirectoryPackagesPropsPath so MSBuild resolves the same pinned versions
# during build.
# ---------------------------------------------------------------------------------------------

echo "dogfood-validate.sh: running consumer test suite ..."
set +e
dotnet test "$consumer_solution" \
    --no-restore \
    -c "$configuration" \
    -p:DirectoryPackagesPropsPath="$dogfood_packages_props"
test_exit_code=$?
set -e

if [ "$test_exit_code" -eq 0 ]; then
    echo "dogfood-validate.sh: PASS - consumer test suite succeeded against local Compono $version"
else
    echo "dogfood-validate.sh: FAIL - consumer test suite failed (exit $test_exit_code) against local Compono $version" >&2
fi

exit "$test_exit_code"
