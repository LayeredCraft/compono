#!/usr/bin/env bash
# Packs Compono and Compono.NUnit into this project's own local NuGet feed (nuget.config) so the
# AOT publish-and-run proof consumes Compono.NUnit via an ordinary PackageReference - the same way
# any real consumer does - rather than a ProjectReference, which propagates dotnet publish's global
# properties (PublishAot/RuntimeIdentifier/SelfContained) down into Compono.csproj's own
# analyzer-only ProjectReference to Compono.Generators.csproj (netstandard2.0) and fails with
# NETSDK1207 ("AOT is not supported for the target framework") for a project that's never actually
# part of the published app. No concurrency lock (unlike Compono.NUnit.SampleTests'
# pack-to-local-feed.sh) - this project is a one-shot manual smoke test, not run concurrently across
# TFMs in CI. Mirrors test/Compono.MSTest.AotSmokeTest/pack-compono.sh, packing the same two
# projects.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
feed_dir="$script_dir/../../.local-nuget-feed-nunit-aot-smoke"
restore_packages_path="$script_dir/obj/.nuget-packages"

mkdir -p "$feed_dir"
rm -f "$feed_dir"/Compono.*.nupkg

# NuGet treats a package id+version already present in a packages folder as immutable and never
# re-extracts it - without clearing this project's own isolated restore path (see the .csproj's
# RestorePackagesPath), a rerun after changing Compono/Compono.NUnit source would silently keep
# serving the first run's cached packages instead of the freshly repacked nupkgs below.
rm -rf "$restore_packages_path"

dotnet pack "$script_dir/../../src/Compono/Compono.csproj" -c Release -o "$feed_dir" -p:Version=1.0.0 --nologo
dotnet pack "$script_dir/../../src/Compono.NUnit/Compono.NUnit.csproj" -c Release -o "$feed_dir" -p:Version=1.0.0 --nologo
