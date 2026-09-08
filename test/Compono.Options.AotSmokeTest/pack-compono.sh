#!/usr/bin/env bash
# Packs Compono and Compono.Options into this project's own local NuGet feed (nuget.config) so the
# AOT publish-and-run proof consumes Compono.Options via an ordinary PackageReference - the same way
# any real consumer does - rather than a ProjectReference. Mirrors
# test/Compono.Logging.AotSmokeTest/pack-compono.sh, packing Compono.Options instead of
# Compono.Logging.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
feed_dir="$script_dir/../../.local-nuget-feed-options-aot-smoke"
restore_packages_path="$script_dir/obj/.nuget-packages"

mkdir -p "$feed_dir"
rm -f "$feed_dir"/Compono.*.nupkg

# NuGet treats a package id+version already present in a packages folder as immutable and never
# re-extracts it - without clearing this project's own isolated restore path, a rerun after changing
# Compono/Compono.Options source would silently keep serving the first run's cached packages instead
# of the freshly repacked nupkgs below.
rm -rf "$restore_packages_path"

dotnet pack "$script_dir/../../src/Compono/Compono.csproj" -c Release -o "$feed_dir" -p:Version=1.0.0 --nologo
dotnet pack "$script_dir/../../src/Compono.Options/Compono.Options.csproj" -c Release -o "$feed_dir" -p:Version=1.0.0 --nologo
