#!/usr/bin/env bash
# PLAN-0074 phase B4: makes the packaging shape ADR-0072 requires permanent and reproducible,
# rather than relying only on manual pack/inspect/consume verification during implementation.
# Mirrors test/Compono.Logging.AotSmokeTest/verify-packaging.sh's own "pack, then assert, fail
# loudly with the evidence" shape.
#
# Confirms, from a real packed .nupkg (not source, not a ProjectReference):
#   1. Compono.Metrics.nupkg ships no analyzer/props of its own.
#   2. Compono.Metrics.nupkg's .nuspec has exactly one dependency group per target TFM (4), each with
#      exactly one dependency - Compono - pinned to the exact version (ADR-0072: zero dependency
#      beyond core Compono; the exact-version pin target is the same one every other package uses).
#   3. A scratch PackageReference consumer of Compono.Metrics ALONE (no explicit property, no
#      ProjectReference workaround) gets real generator-discovered IMeterFactory composition, capture,
#      tag matching, and Verify() - proving the whole chain flows transitively through Compono.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
feed_dir="$repo_root/.local-nuget-feed-metrics-aot-smoke"
work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

echo "verify-packaging.sh: packing Compono/Compono.Metrics into the local feed ..."
bash "$script_dir/pack-compono.sh"

echo "verify-packaging.sh: inspecting packed nupkg contents ..."
metrics_inspect="$work_dir/inspect-metrics"
mkdir -p "$metrics_inspect"
unzip -o -q "$feed_dir/Compono.Metrics.1.0.0.nupkg" -d "$metrics_inspect"

fail() {
    echo "verify-packaging.sh: FAIL - $1" >&2
    exit 1
}

[ -d "$metrics_inspect/analyzers" ] && fail "Compono.Metrics.nupkg unexpectedly contains an analyzers/ directory - it must ship no generator/analyzer DLL of its own"
[ -d "$metrics_inspect/build" ] && fail "Compono.Metrics.nupkg unexpectedly contains a build/ directory - it must ship no props of its own"

nuspec=$(find "$metrics_inspect" -maxdepth 1 -name '*.nuspec' | head -n 1)
[ -n "$nuspec" ] || fail "Compono.Metrics.nupkg is missing its .nuspec"

group_count=$(grep -o '<group ' "$nuspec" | wc -l | tr -d ' ')
dep_count=$(grep -o '<dependency ' "$nuspec" | wc -l | tr -d ' ')
[ "$group_count" = "4" ] || fail "Compono.Metrics.nuspec should have exactly 4 dependency groups (one per TFM), found $group_count"
[ "$dep_count" = "4" ] || fail "Compono.Metrics.nuspec should have exactly 4 dependencies (Compono once per TFM), found $dep_count"

non_compono_deps=$(grep -o '<dependency id="[^"]*"' "$nuspec" | grep -v 'id="Compono"' || true)
[ -z "$non_compono_deps" ] || fail "Compono.Metrics.nuspec has a dependency that is not Compono: $non_compono_deps"

exact_version_deps=$(grep -o 'version="\[1.0.0\]"' "$nuspec" | wc -l | tr -d ' ')
[ "$exact_version_deps" = "4" ] || fail "every Compono dependency must be pinned to the exact version [1.0.0], found $exact_version_deps exact pins"

echo "verify-packaging.sh: packaging shape confirmed (no analyzer/props, 4 TFM groups, single exact-version Compono dependency each)."

echo "verify-packaging.sh: verifying a scratch PackageReference consumer of Compono.Metrics alone ..."
consumer_dir="$work_dir/consumer"
mkdir -p "$consumer_dir"
cat > "$consumer_dir/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="compono-local" value="$feed_dir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF
cat > "$consumer_dir/Consumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <RestorePackagesPath>$consumer_dir/.nuget-packages</RestorePackagesPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Compono.Metrics" Version="1.0.0" />
  </ItemGroup>
</Project>
EOF
cat > "$consumer_dir/Program.cs" <<'EOF'
using System.Diagnostics.Metrics;
using Compono;
using Compono.Metrics;

var composer = Composer.Create(builder => builder.UseMetrics());
var meters = composer.Create<IMeterFactory>();
using var _ = meters;

var meter = meters.Create("Smoke");
var counter = meter.CreateCounter<long>("smoke.counter");
counter.Add(1, new KeyValuePair<string, object?>("region", "eu"));

meters.Verify<long>("smoke.counter").WithTag("region", "eu").WithValue(1).Once();
Console.WriteLine(meters is CapturingMeterFactory ? "METRICS-COMPOSITION-OK" : "UNEXPECTED-FACTORY");
EOF

consumer_output=$(cd "$consumer_dir" && dotnet run -c Release 2>&1)
echo "$consumer_output" | grep -q "METRICS-COMPOSITION-OK" || {
    echo "verify-packaging.sh: FAIL - a PackageReference-only consumer of Compono.Metrics did not compose/capture as expected. Output:" >&2
    echo "$consumer_output" >&2
    exit 1
}
echo "verify-packaging.sh: confirmed - PackageReference-only composition works."

echo "verify-packaging.sh: PASS - all packaging/consumption behaviors confirmed against a real packed nupkg."
