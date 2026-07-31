#!/usr/bin/env bash
# Packs Compono and Compono.XunitV3 into the local NuGet feed this project restores against
# (test/Compono.XunitV3.SampleTests/nuget.config), serialized behind a cross-process lock.
#
# Why the lock: this script is invoked from a PackToLocalFeed MSBuild target
# (BeforeTargets="Restore") on every restore of this project. CI (and RealRunnerTests, which shells
# out `dotnet test` against this project) runs Compono.XunitV3.Tests for multiple TFMs concurrently,
# so two independent nested `dotnet test` processes can each trigger this script at the same moment,
# both packing the same src/Compono and src/Compono.Generators projects into the same
# .local-nuget-feed/ directory - a real race (reproduced locally: "The process cannot access the
# file '.../Compono.1.0.0.nupkg' because it is being used by another process", and in CI: "Could not
# find a part of the path '.../Compono.Generators/bin/Debug/netstandard2.0'"). `mkdir` is atomic even
# across processes and platforms, so it works as a portable mutex without a custom MSBuild task.
set -euo pipefail

compono_csproj="$1"
xunitv3_csproj="$2"
feed_dir="$3"
configuration="$4"

lock_dir="$feed_dir/.pack.lock"

until mkdir "$lock_dir" 2>/dev/null; do
    sleep 1
done
trap 'rmdir "$lock_dir"' EXIT

dotnet pack "$compono_csproj" -c "$configuration" -o "$feed_dir" -p:Version=1.0.0 --nologo
dotnet pack "$xunitv3_csproj" -c "$configuration" -o "$feed_dir" -p:Version=1.0.0 --nologo
