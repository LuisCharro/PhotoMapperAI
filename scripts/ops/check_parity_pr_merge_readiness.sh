#!/usr/bin/env bash
set -euo pipefail

PRS=(2 3 4 5)

for pr in "${PRS[@]}"; do
  echo "\n== PR #$pr =="

  state=$(gh pr view "$pr" --json state --jq '.state')
  approvals=$(gh pr view "$pr" --json reviews --jq '[.reviews[] | select(.state=="APPROVED")] | length')
  checks=$(gh pr checks "$pr" 2>&1 || true)

  echo "state: $state"
  echo "approvals: $approvals"
  echo "$checks"

  if [[ "$state" != "OPEN" ]]; then
    echo "skip: not open"
    continue
  fi

  if [[ "$approvals" -lt 1 ]]; then
    echo "❌ missing required approval"
    continue
  fi

  if echo "$checks" | grep -qi "pending"; then
    echo "⏳ checks pending"
    continue
  fi

  if echo "$checks" | grep -qiE "\bfail\b|\berror\b|\bcancel\b"; then
    echo "❌ failing checks"
    continue
  fi

  echo "✅ ready to merge"
done
