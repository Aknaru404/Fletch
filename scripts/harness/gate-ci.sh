#!/bin/bash
# Gate G4: wait for the latest CI run on a branch and download its IPA and screenshots.
# Usage: scripts/harness/gate-ci.sh [branch]   (default: current branch)
set -uo pipefail

root="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
cd "$root"
repo="$(git remote get-url origin | sed -E 's#^https://github.com/##; s#\.git$##')"
branch="${1:-$(git branch --show-current)}"
sha="$(git rev-parse "origin/$branch" 2>/dev/null || git rev-parse HEAD)"
out="${CLAUDE_SCRATCH:-/tmp}/ci-$(echo "$sha" | cut -c1-7)"

echo "## G4 CI gate ($repo@$branch, ${sha:0:7})"

run=""
for _ in $(seq 1 30); do
  run="$(gh run list -R "$repo" -b "$branch" -c "$sha" -L 5 --json databaseId -q '.[].databaseId' | head -1)"
  [ -n "$run" ] && break
  sleep 10
done
[ -z "$run" ] && { echo "| CI | Rejected | no run found for ${sha:0:7}; was the branch pushed? |"; exit 1; }

gh run watch "$run" -R "$repo" --exit-status --interval 30 > /dev/null 2>&1
echo "| Job | Verdict | Detail |"
echo "|---|---|---|"
gh run view "$run" -R "$repo" --json jobs \
  -q '.jobs[] | "| \(.name) | \(if .conclusion == "success" then "Pass" elif .conclusion == "skipped" then "Skipped" else "Rejected" end) | \(.conclusion) |"'

mkdir -p "$out"
gh run download "$run" -R "$repo" -D "$out" > /dev/null 2>&1 && echo && echo "Artifacts: $out" && find "$out" -type f \( -name '*.ipa' -o -name '*.png' \) | sort

conclusion="$(gh run view "$run" -R "$repo" --json conclusion -q .conclusion)"
echo
if [ "$conclusion" = "success" ]; then echo "**G4 verdict: Pass**"; exit 0; fi
echo "**G4 verdict: Rejected** ($conclusion) - gh run view $run -R $repo --log-failed"
exit 1
