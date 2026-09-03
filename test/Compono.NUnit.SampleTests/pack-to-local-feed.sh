#!/usr/bin/env bash
# Packs Compono, Compono.NUnit, and Compono.NSubstitute into the local NuGet feed this project
# restores against (test/Compono.NUnit.SampleTests/nuget.config), serialized behind a cross-process
# lock, and clears this restore's own isolated packages path before every pack. Mirrors
# test/Compono.MSTest.SampleTests/pack-to-local-feed.sh exactly - see that script's own comment for
# the full reasoning behind the lock (concurrent nested `dotnet test` invocations racing on the same
# .local-nuget-feed/ and src/Compono*/bin/obj output) and the isolated restore-packages-path clear
# (NuGet never re-extracts an already-present package id+version, so a fixed "1.0.0" version would
# otherwise let a stale entry silently satisfy a later restore).
set -euo pipefail

compono_csproj="$1"
nunit_csproj="$2"
nsubstitute_csproj="$3"
feed_dir="$4"
configuration="$5"
restore_packages_path="$6"

lock_dir="$feed_dir/.pack.lock"

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
dotnet pack "$nunit_csproj" -c "$configuration" -o "$feed_dir" -p:Version=1.0.0 --nologo
dotnet pack "$nsubstitute_csproj" -c "$configuration" -o "$feed_dir" -p:Version=1.0.0 --nologo
