#!/usr/bin/env bash
# Packs Compono and Compono.NUnit into the local NuGet feed this project restores against
# (test/Compono.NUnit.CompatibilityMatrix/nuget.config). No cross-process lock (unlike
# Compono.NUnit.SampleTests' pack-to-local-feed.sh) - the compatibility-matrix script
# (.github/scripts/nunit-compatibility-matrix.sh) runs every leg sequentially against this one
# project, never concurrently.
set -euo pipefail

compono_csproj="$1"
nunit_csproj="$2"
feed_dir="$3"
configuration="$4"
restore_packages_path="$5"

rm -f "$feed_dir"/Compono.*.nupkg
rm -rf "$restore_packages_path"

dotnet pack "$compono_csproj" -c "$configuration" -o "$feed_dir" -p:Version=1.0.0 --nologo
dotnet pack "$nunit_csproj" -c "$configuration" -o "$feed_dir" -p:Version=1.0.0 --nologo
