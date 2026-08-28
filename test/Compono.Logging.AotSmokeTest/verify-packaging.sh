#!/usr/bin/env bash
# PLAN-0055 task 17: makes the packaging behavior ADR-0055 Amendments 1-3 require permanent and
# reproducible, rather than relying only on the manual pack/inspect/consume spikes already done by
# hand during implementation. Mirrors
# test/Compono.Http.AotSmokeTest/AnalyzerContract/verify-analyzer-contract.sh's own
# "pack, then assert, fail loudly with the evidence" shape.
#
# Confirms, from a real packed .nupkg (not source, not a ProjectReference):
#   1. Compono.Logging.nupkg contains build/Compono.Logging.props and buildTransitive/Compono.Logging.props.
#   2. Compono.Logging.nupkg contains NO analyzers/ directory at all.
#   3. Compono.nupkg contains exactly one analyzer asset (Compono.Generators.dll), no duplicate.
#   4. A scratch PackageReference consumer of Compono.Logging ALONE (no explicit property, no
#      ProjectReference workaround) gets real generator-discovered ILogger<T> activation - proving
#      default-on works and the generator/property both flow transitively through Compono.Logging's
#      own dependency on Compono.
#   5. The same shape with <ComponoGeneratedLogging>false</ComponoGeneratedLogging> set explicitly
#      suppresses generation - LoggingProvider throws its documented missing-activation diagnostic
#      instead of silently working, proving the explicit opt-out is real and not a no-op.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
feed_dir="$repo_root/.local-nuget-feed-logging-aot-smoke"
work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

echo "verify-packaging.sh: packing Compono/Compono.Logging into the local feed ..."
bash "$script_dir/pack-compono.sh"

echo "verify-packaging.sh: inspecting packed nupkg contents ..."
logging_inspect="$work_dir/inspect-logging"
compono_inspect="$work_dir/inspect-compono"
mkdir -p "$logging_inspect" "$compono_inspect"
unzip -o -q "$feed_dir/Compono.Logging.1.0.0.nupkg" -d "$logging_inspect"
unzip -o -q "$feed_dir/Compono.1.0.0.nupkg" -d "$compono_inspect"

fail() {
    echo "verify-packaging.sh: FAIL - $1" >&2
    exit 1
}

[ -f "$logging_inspect/build/Compono.Logging.props" ] || fail "Compono.Logging.nupkg is missing build/Compono.Logging.props"
[ -f "$logging_inspect/buildTransitive/Compono.Logging.props" ] || fail "Compono.Logging.nupkg is missing buildTransitive/Compono.Logging.props"
[ -d "$logging_inspect/analyzers" ] && fail "Compono.Logging.nupkg unexpectedly contains an analyzers/ directory - it must ship no generator/analyzer DLL of its own"

analyzer_count=$(find "$compono_inspect/analyzers" -name '*.dll' 2>/dev/null | wc -l | tr -d ' ')
[ "$analyzer_count" = "1" ] || fail "Compono.nupkg should contain exactly 1 analyzer DLL, found $analyzer_count"
[ -f "$compono_inspect/analyzers/dotnet/cs/Compono.Generators.dll" ] || fail "Compono.nupkg's single analyzer DLL is not Compono.Generators.dll as expected"

echo "verify-packaging.sh: packaging shape confirmed (props present, no analyzer in Compono.Logging.nupkg, exactly one shared analyzer in Compono.nupkg)."

make_scratch_consumer() {
    local dir="$1"
    local extra_property="$2"
    mkdir -p "$dir"
    cat > "$dir/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="compono-local" value="$feed_dir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF
    cat > "$dir/Consumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <RestorePackagesPath>$dir/.nuget-packages</RestorePackagesPath>
$extra_property
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Compono.Logging" Version="1.0.0" />
  </ItemGroup>
</Project>
EOF
    cat > "$dir/Program.cs" <<'EOF'
using Compono;
using Compono.Logging;
using Microsoft.Extensions.Logging;

try
{
    var composer = Composer.Create(builder => builder.UseLogging());
    var service = composer.Create<OrderService>();
    service.PlaceOrder();
    var entries = service.Logger.GetCapturedEntries();
    Console.WriteLine(entries.Count == 1 ? "GENERATION-ENABLED" : "UNEXPECTED-ENTRY-COUNT");
}
catch (InvalidOperationException ex) when (ex.Message.Contains("no generated activation"))
{
    Console.WriteLine("GENERATION-DISABLED");
}

internal sealed class OrderService(ILogger<OrderService> logger)
{
    public ILogger<OrderService> Logger { get; } = logger;
    public void PlaceOrder() => Logger.LogWarning("retrying order {OrderId}", 1);
}
EOF
}

echo "verify-packaging.sh: verifying default-on generation (no property set, PackageReference only) ..."
default_dir="$work_dir/default-consumer"
make_scratch_consumer "$default_dir" ""
default_output=$(cd "$default_dir" && dotnet run -c Release 2>&1)
echo "$default_output" | grep -q "GENERATION-ENABLED" || {
    echo "verify-packaging.sh: FAIL - default-on generation did not work with only a PackageReference to Compono.Logging and no explicit property. Output:" >&2
    echo "$default_output" >&2
    exit 1
}
echo "verify-packaging.sh: confirmed - default-on works with no property set."

echo "verify-packaging.sh: verifying explicit false suppresses generation ..."
disabled_dir="$work_dir/disabled-consumer"
make_scratch_consumer "$disabled_dir" "    <ComponoGeneratedLogging>false</ComponoGeneratedLogging>"
disabled_output=$(cd "$disabled_dir" && dotnet run -c Release 2>&1)
echo "$disabled_output" | grep -q "GENERATION-DISABLED" || {
    echo "verify-packaging.sh: FAIL - explicit ComponoGeneratedLogging=false did not suppress generation as expected. Output:" >&2
    echo "$disabled_output" >&2
    exit 1
}
echo "verify-packaging.sh: confirmed - explicit false suppresses logging generation."

echo "verify-packaging.sh: PASS - all packaging/gating behaviors confirmed against real packed nupkgs."
