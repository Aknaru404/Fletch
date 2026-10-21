#!/bin/bash
# Gate G1: build, full test suite and rule guards on Linux.
# Usage: scripts/harness/gate-local.sh [dotnet test --filter expression]
set -uo pipefail

root="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
cd "$root"
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

results="$(mktemp -d)"
filter=()
[ $# -gt 0 ] && filter=(--filter "$1")

echo "## G1 Local gate ($(git rev-parse --short HEAD), state $(tr -d '[:space:]' < docs/fletch/STATE))"
echo

# Upstream code carries nullable warnings, so warnings are not errors here; judges review nullability on our diffs.
if ! dotnet build QuiverLauncher.Tests/QuiverLauncher.Tests.csproj -c Release > "$results/build.log" 2>&1; then
  echo "| Check | Verdict | Detail |"
  echo "|---|---|---|"
  echo "| Build | Rejected | see errors below |"
  grep -E ' error ' "$results/build.log" | sort -u | head -30
  exit 1
fi

dotnet test QuiverLauncher.Tests/QuiverLauncher.Tests.csproj -c Release --no-build "${filter[@]}" \
  --logger "trx;LogFileName=g1.trx" --results-directory "$results" > "$results/test.log" 2>&1

# Linux permission tests cannot fail as root (root ignores chmod); they still run in CI as a normal user.
allow_root='LibraryFileSafetyTests.Linux_permission_failures|LauncherUpdateBackupTests.Linux_unreadable_source'
[ "$(id -u)" -ne 0 ] && allow_root='^$'

python3 -I - "$results/g1.trx" "$allow_root" <<'EOF'
import re, sys, xml.etree.ElementTree as ET
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
root = ET.parse(sys.argv[1]).getroot()
allowed = re.compile(sys.argv[2])
c = root.find('.//t:Counters', ns).attrib
failed = [u for u in root.iterfind('.//t:UnitTestResult', ns) if u.get('outcome') == 'Failed']
real = [u for u in failed if not allowed.search(u.get('testName'))]
print("| Check | Verdict | Detail |")
print("|---|---|---|")
print("| Build | Pass | Release |")
print(f"| Tests | {'Rejected' if real else 'Pass'} | {c['passed']} passed, {len(real)} failed, "
      f"{len(failed) - len(real)} root-only tolerated, {int(c['total']) - int(c['executed'])} skipped |")
for u in real:
    m = u.find('.//t:Message', ns)
    print(f"    FAIL {u.get('testName')}: {(m.text or '').strip()[:240]}")
sys.exit(1 if real else 0)
EOF
tests=$?

echo
scripts/harness/guards.sh
guards=$?

echo
if [ $tests -eq 0 ] && [ $guards -eq 0 ]; then echo "**G1 verdict: Pass**"; exit 0; fi
echo "**G1 verdict: Rejected**"
exit 1
