#!/bin/bash
# Builds Fletch.iOS for one runtime identifier, logging to artifacts/ and turning build errors into
# GitHub annotations (readable through the API when the raw job log is not reachable).
# Usage: scripts/ci/ios-build.sh <rid> [msbuild args...]
set -uo pipefail

rid="$1"; shift
mkdir -p artifacts
log="artifacts/build-$rid.log"

dotnet build Fletch.iOS/Fletch.iOS.csproj -c Release -r "$rid" "$@" 2>&1 | tee "$log"
code=${PIPESTATUS[0]}
if [ "$code" -ne 0 ]; then
  grep -E '(error|Error) [A-Z]+[0-9]+|: error ' "$log" | sed -E 's/^ +//' | sort -u | head -20 | sed 's/^/::error::/'
fi
exit "$code"
