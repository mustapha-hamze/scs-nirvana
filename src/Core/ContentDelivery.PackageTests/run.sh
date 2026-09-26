#!/usr/bin/env bash
# Release validation for the Content Delivery SDK packages - not part of the solution's tests.
# Proves a website gets a working SDK from the packed .nupkg files of the current source:
#   1. clears this fixture's generated feed, build output and cached Cms.ContentDelivery* packages;
#   2. proves the fixture cannot restore without freshly packed packages (NU1101);
#   3. packs the current source into obj/feed, restores the fixture from it (nuget.config maps
#      Cms.ContentDelivery* to that feed only) and runs its tests, including the check that the
#      loaded DLLs are the packed ones.
# Prerequisite (release/CI only): NuGet access (nuget.org) for third-party packages on a machine
# whose obj/nuget-packages cache is cold.
#   ./run.sh            the release gate
#   ./run.sh baseline   the gate, then regenerates BASELINE.md (non-gating measurements)
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
feed="$here/obj/feed"
cache="$here/obj/nuget-packages"

# bin too: packed files carry a fixed timestamp, so a same-size DLL from a newer pack would be
# skipped by MSBuild's unchanged-file copy and the previous binary tested.
rm -rf "$feed" "$here/bin" "$cache/cms.contentdelivery" "$cache/cms.contentdelivery.sqlserver"
mkdir -p "$feed"

if output="$(dotnet restore "$here" --force 2>&1)" || ! grep -q NU1101 <<<"$output"; then
  echo "$output"
  echo "FAIL: the fixture restored without freshly packed Cms.ContentDelivery packages." >&2
  exit 1
fi
echo "OK: restore fails without freshly packed packages (NU1101)."

for project in ContentDelivery ContentDelivery.SqlServer; do
  dotnet pack "$here/../$project/$project.csproj" -c Release -o "$feed"
done
dotnet restore "$here" --force
dotnet test "$here" -c Release --no-restore --filter "Category!=Baseline"

if [ "${1:-}" = baseline ]; then
  CONTENT_DELIVERY_BASELINE_OUT="$here/BASELINE.md" dotnet test "$here" -c Release --no-restore --filter "Category=Baseline"
fi
