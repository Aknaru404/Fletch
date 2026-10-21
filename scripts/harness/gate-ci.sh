#!/bin/bash
# Gate G4: wait for every CI workflow run on a branch's head commit; pass only if all required ones succeed.
# Usage: scripts/harness/gate-ci.sh [branch]   (default: current branch)
set -uo pipefail

root="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
cd "$root"
repo="$(git remote get-url origin | sed -E 's#^https://github.com/##; s#\.git$##')"
branch="${1:-$(git branch --show-current)}"
sha="$(git rev-parse "origin/$branch" 2>/dev/null || git rev-parse HEAD)"
out="${CLAUDE_SCRATCH:-/tmp}/ci-$(echo "$sha" | cut -c1-7)"

echo "## G4 CI gate ($repo@$branch, ${sha:0:7})"

# Every workflow on the commit must pass: iOS Build (push) and .NET Desktop Build (PR, carries the Linux tests).
required=("iOS Build" ".NET Desktop Build")
runs=""
for _ in $(seq 1 30); do
  runs="$(gh run list -R "$repo" -b "$branch" -c "$sha" -L 20 --json databaseId,workflowName \
    -q '.[] | "\(.databaseId)\t\(.workflowName)"' | sort -s -t$'\t' -k2,2 -u)"
  missing=0
  for w in "${required[@]}"; do grep -qF "$(printf '\t%s' "$w")" <<<"$runs" || missing=1; done
  [ "$missing" -eq 0 ] && break
  sleep 10
done

echo "| Workflow / job | Verdict | Detail |"
echo "|---|---|---|"
failed=0
for w in "${required[@]}"; do
  grep -qF "$(printf '\t%s' "$w")" <<<"$runs" || { echo "| $w | Rejected | no run for ${sha:0:7}; is the draft PR open? |"; failed=1; }
done
while IFS=$'\t' read -r run name; do
  [ -n "$run" ] || continue
  gh run watch "$run" -R "$repo" --interval 30 > /dev/null 2>&1
  conclusion="$(gh run view "$run" -R "$repo" --json conclusion -q .conclusion)"
  [ "$conclusion" = "success" ] || failed=1
  echo "| **$name** | $([ "$conclusion" = success ] && echo Pass || echo Rejected) | run $run: $conclusion |"
  gh run view "$run" -R "$repo" --json jobs \
    -q '.jobs[] | "| \(.name) | \(if .conclusion == "success" then "Pass" elif .conclusion == "skipped" then "Skipped" else "Rejected" end) | \(.conclusion) |"'
  # Raw logs and artifacts are served from blob storage that cloud sessions may not reach; annotations always load.
  for job in $(gh api "repos/$repo/actions/runs/$run/jobs" -q '.jobs[] | select(.conclusion == "failure") | .id'); do
    gh api "repos/$repo/check-runs/$job/annotations" -q '.[] | select(.annotation_level == "failure") | "    " + .message' | cut -c1-300
  done
  if [ "$name" = "iOS Build" ]; then
    mkdir -p "$out"
    if gh run download "$run" -R "$repo" -D "$out" > /dev/null 2>&1; then
      echo "    Artifacts: $out"; find "$out" -type f \( -name '*.ipa' -o -name '*.png' \) | sort | sed 's/^/    /'
    else
      echo "    Artifacts: not downloadable here; get them from the run page on github.com (run $run)"
    fi
  fi
done <<<"$runs"

echo
if [ "$failed" -eq 0 ]; then echo "**G4 verdict: Pass**"; exit 0; fi
echo "**G4 verdict: Rejected**"
exit 1
