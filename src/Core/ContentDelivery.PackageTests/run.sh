#!/usr/bin/env bash
# The packed-consumer release gate, alone (it also runs with the solution's tests):
#   ./run.sh            clean feed -> pack current source -> restore -> fixture tests
#   ./run.sh baseline   the above, then regenerates BASELINE.md (non-gating measurements)
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"

dotnet test "$here/../ContentDelivery.SqlServer.Tests" --filter "Category=PackageGate"

if [ "${1:-}" = baseline ]; then
  CONTENT_DELIVERY_BASELINE_OUT="$here/BASELINE.md" dotnet test "$here" -c Release --no-restore --filter "Category=Baseline"
fi
