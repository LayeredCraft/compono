#!/usr/bin/env bash
# Packs Compono, Compono.XunitV3, and Compono.TestDoubles into the local NuGet feed this project
# restores against (test/Compono.TestDoubles.SampleTests/nuget.config), serialized behind a
# cross-process lock, and clears this restore's own isolated packages path before every pack.
#
# Mirrors Compono.XunitV3.SampleTests/pack-to-local-feed.sh almost verbatim (same shared
# .local-nuget-feed/ directory, same lock/clear reasoning) - see that script's own comment for the
# full account of why the lock exists and why $restore_packages_path is cleared here.
set -euo pipefail

compono_csproj="$1"
xunitv3_csproj="$2"
testdoubles_csproj="$3"
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
dotnet pack "$xunitv3_csproj" -c "$configuration" -o "$feed_dir" -p:Version=1.0.0 --nologo
dotnet pack "$testdoubles_csproj" -c "$configuration" -o "$feed_dir" -p:Version=1.0.0 --nologo
