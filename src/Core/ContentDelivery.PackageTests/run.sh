#!/usr/bin/env bash
# Packs the Content Delivery SDK into a local feed and runs this package-consumer fixture on it.
#   ./run.sh            package boundary, consumer compatibility and contract fixtures
#   ./run.sh baseline   the above, then regenerates BASELINE.md (non-gating measurements)
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"

rm -rf "$here/obj/feed" "$here/obj/nuget-packages/cms.contentdelivery" "$here/obj/nuget-packages/cms.contentdelivery.sqlserver"
for project in ContentDelivery ContentDelivery.SqlServer; do
  dotnet pack "$here/../$project/$project.csproj" -c Release -o "$here/obj/feed"
done

dotnet test "$here" -c Release --filter "Category!=Baseline"

if [ "${1:-}" = baseline ]; then
  CONTENT_DELIVERY_BASELINE_OUT="$here/BASELINE.md" dotnet test "$here" -c Release --no-build --filter "Category=Baseline"
fi
