#!/bin/bash
# Static rule checks from CLAUDE.md. Each rule switches on at the milestone it belongs to (docs/fletch/STATE).
# Prints one row per rule (Pass/Rejected/Inactive) and exits non-zero if any rule is Rejected.
set -uo pipefail

root="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
cd "$root"
state="$(tr -d '[:space:]' < docs/fletch/STATE)"
current="${state#M}"
failed=0

row() { printf '| %-11s | %-8s | %s |\n' "$1" "$2" "$3"; }

check() {
  local rule="$1" from="$2" desc="$3" hits="$4"
  if [ "$current" -lt "$from" ]; then
    row "$rule" "Inactive" "from M$from: $desc"
  elif [ -n "$hits" ]; then
    row "$rule" "Rejected" "$desc"
    printf '%s\n' "$hits" | sed 's/^/    /'
    failed=1
  else
    row "$rule" "Pass" "$desc"
  fi
}

ios_files() {
  { [ -d Fletch.iOS ] && find Fletch.iOS -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*'
    git ls-files '*Ios*.cs' '*IOS*.cs' '*iOS*.cs'; } 2>/dev/null | sort -u
}

echo "| Rule        | Verdict  | Detail |"
echo "|-------------|----------|--------|"

hits="$(git ls-files '*.p12' '*.mobileprovision' '*.keystore' '*.jks')"
check G-SECRETS 0 "no signing material committed" "$hits"

# Comment-only lines are skipped; string literals still count because users see them.
hits="$(git grep -nIiE 'posthog|quiverlauncher\.com|convex\.site' -- '*.cs' ':!QuiverLauncher.Tests/**' 2>/dev/null \
  | grep -vE '^[^:]+:[0-9]+:[[:space:]]*(//|/?\*)')"
check G-UPSTREAM 2 "no calls to upstream telemetry, catalog or announcements" "$hits"

hits=""
if [ -d Fletch.iOS ]; then
  hits="$(grep -RInE 'Quiver' Fletch.iOS --include='*.plist' --include='*.csproj' --include='*.cs' \
    | grep -vE 'QuiverLauncher\.(App|Core)|using QuiverLauncher|namespace QuiverLauncher|ProjectReference')"
fi
hits+="$(git grep -nE '(DisplayName|ApplicationId|AppName)\s*=\s*"[^"]*Quiver' -- '*.cs' '*.csproj' ':!QuiverLauncher.Tests/**' 2>/dev/null)"
check G-BRAND 2 "no Quiver branding in identifiers" "$hits"

files="$(ios_files)"
hits=""
if [ -n "$files" ]; then
  # shellcheck disable=SC2086
  hits="$(grep -nE 'Process\.Start|Reflection\.Emit|DynamicMethod|Assembly\.Load' $files)"
fi
check G-IOSAPI 1 "no process launch or dynamic code on iOS paths" "$hits"

exit "$failed"
