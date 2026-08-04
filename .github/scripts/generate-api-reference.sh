#!/usr/bin/env bash
# Regenerates docs/reference/api/<package>/ from each of the four publishable
# packages' compiled net10.0 assembly + XML doc file, via DefaultDocumentation
# (ADR-0032, PLAN-0008 Phase 1). Not Compono.Generators - IsPackable=false, an
# internal analyzer implementation verified as package content elsewhere
# (inspect-packed-nupkgs.sh), not documented as public API here.
#
# Deterministic (byte-identical output for identical input, per ADR-0032's
# drift-detection requirement) - verified during the Phase 1 tool bake-off.
# Requires each package already built in Release for net10.0
# (`dotnet build <csproj> -c Release -f net10.0`) and the local dotnet tool
# manifest restored (`dotnet tool restore`).
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

# Compono core first, then the three integration packages, which all depend
# on it (never the other way around - design-decisions.md rule 3). Core's
# --LinksOutputFilePath is fed to each integration package's
# --ExternLinksFilePaths so a <see cref="Compono.Composer"/> in e.g.
# Compono.XunitV3's XML docs resolves to a local ../Compono/*.md link instead
# of DefaultDocumentation's DotnetApiFactory fallback, which treats any type
# it doesn't recognize as a BCL type and links to a learn.microsoft.com URL
# that 404s for a Compono type - caught during the Phase 1 tool bake-off.
core_pkg=Compono
integration_pkgs=(Compono.XunitV3 Compono.NSubstitute Compono.Bogus)

links_dir=$(mktemp -d)
trap 'rm -rf "$links_dir"' EXIT
core_links_file="$links_dir/Compono.links"

generate_one() {
    local pkg="$1"
    shift
    local dll="src/${pkg}/bin/Release/net10.0/${pkg}.dll"
    if [ ! -f "$dll" ]; then
        echo "::error::$dll not found - build $pkg for net10.0 in Release before running this script." >&2
        exit 1
    fi

    local out_dir="docs/reference/api/${pkg}"
    # Regenerated from scratch each run, not incrementally - DefaultDocumentation
    # has no --clean option (unlike xmldocmd), so a stale page from a since-removed
    # member would otherwise survive regeneration and silently keep shipping.
    rm -rf "$out_dir"
    mkdir -p "$out_dir"

    echo "Generating API reference for $pkg..."
    dotnet defaultdocumentation \
        --AssemblyFilePath "$dll" \
        --OutputDirectoryPath "$out_dir" \
        --GeneratedAccessModifiers Public \
        --AssemblyPageName index \
        "$@"
}

generate_one "$core_pkg" --LinksOutputFilePath "$core_links_file" --LinksBaseUrl "../${core_pkg}/"

for pkg in "${integration_pkgs[@]}"; do
    generate_one "$pkg" --ExternLinksFilePaths "$core_links_file"
done

# DefaultDocumentation names a parameterless constructor's page after the raw
# CLR metadata name "#ctor" (e.g. Compono.ComposableAttribute.#ctor.md) - '#'
# is the URL fragment delimiter, so MkDocs (and any other static host) parses
# a link into that filename as "...ComposableAttribute." plus a "ctor.md#..."
# fragment and 404s. Caught by a real `mkdocs build` during the Phase 1 tool
# bake-off. Rename every such file and fix up the (small, deterministic) set
# of other generated pages that link to it - across all four packages' output,
# since a future integration package could plausibly <see cref/> a core
# constructor the same way.
#
# DefaultDocumentation's own same-page anchor names on a "#ctor"-named page
# are built as "<part of the filename after its own last '#'>#<member id>"
# (e.g. name='ctor.md#Compono.ComposableAttribute.ComposableAttribute()') -
# internally consistent only because it assumes the filename still contains a
# literal '#' acting as the real fragment delimiter. Renaming the file to
# remove that '#' already fixes every *href* pointing at this page (the
# substitution above), but leaves the page's own in-page anchor `name`
# values carrying that now-stale "ctor.md#" prefix, so a deep link into a
# specific overload lands at the top of the page instead - caught by PR #47
# review. Strip that leftover prefix from the renamed file's own anchors.
while IFS= read -r -d '' old_path; do
    new_path="${old_path//\#ctor/.ctor}"
    old_name="$(basename "$old_path")"
    new_name="$(basename "$new_path")"
    mv "$old_path" "$new_path"
    grep -rl --include='*.md' -F -- "$old_name" docs/reference/api | while IFS= read -r referencing_file; do
        sed -i.bak "s/${old_name//./\\.}/${new_name}/g" "$referencing_file"
        rm -f "${referencing_file}.bak"
    done

    stale_anchor_prefix="${old_name##*#}#"
    sed -i.bak "s/name='${stale_anchor_prefix//./\\.}/name='/g" "$new_path"
    rm -f "${new_path}.bak"
done < <(find docs/reference/api -name '*#ctor*.md' -print0)

# A public member's XML docs can <see cref/> an internal Compono type (e.g. a
# CompositionConfiguration mentioned from CompositionBuilder's public docs).
# DocItemFactory can't resolve it locally (this run only generated Public
# DocItems, and it isn't in any --ExternLinksFilePaths either), so it falls
# through to DotnetApiFactory, which treats any unrecognized symbol as a BCL
# type and fabricates a "learn.microsoft.com/en-us/dotnet/api/compono.*" URL
# that 404s - flagged by PR #47 review (38 such dead links found). Rewrite
# every such link to plain inline code instead of leaving a dead link -
# there is no real page to point it at without publishing internal types,
# which would contradict generating only the public surface in the first
# place.
python3 - <<'PYEOF'
import pathlib
import re

pattern = re.compile(
    r"\[([^\]]+)\]\(https://learn\.microsoft\.com/en-us/dotnet/api/compono\.[^\s)]+ '[^']*'\)"
)
unescape = re.compile(r"\\(.)")

for path in pathlib.Path("docs/reference/api").rglob("*.md"):
    text = path.read_text()
    new_text = pattern.sub(lambda m: f"`{unescape.sub(r'\1', m.group(1))}`", text)
    if new_text != text:
        path.write_text(new_text)
PYEOF

echo "API reference generation complete."
