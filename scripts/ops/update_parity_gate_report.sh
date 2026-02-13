#!/usr/bin/env bash
set -euo pipefail

# Generates a markdown status report for parity PR merge gates.

OUT="docs/PARITY_GATE_LIVE.md"
PRS=(2 3 4 5)

{
  echo "# Parity Gate Live Report"
  echo
  echo "Generated: $(date '+%Y-%m-%d %H:%M:%S %Z')"
  echo
  echo "| PR | Branch | State | Approvals | Checks |"
  echo "|---|---|---|---:|---|"

  for pr in "${PRS[@]}"; do
    branch=$(gh pr view "$pr" --json headRefName --jq '.headRefName')
    state=$(gh pr view "$pr" --json state --jq '.state')
    approvals=$(gh pr view "$pr" --json reviews --jq '[.reviews[] | select(.state=="APPROVED")] | length')

    checks_raw=$(gh pr checks "$pr" 2>&1 || true)
    if echo "$checks_raw" | grep -qiE "\bfail\b|\berror\b|\bcancel\b"; then
      checks="failing"
    elif echo "$checks_raw" | grep -qi "pending"; then
      checks="pending"
    else
      checks="passing"
    fi

    echo "| #$pr | \`$branch\` | $state | $approvals | $checks |"
  done

  echo
  echo "## Recommended next action"
  echo
  echo "- If approvals >= 1 on all PRs and checks are passing:"
  echo "  - \`./scripts/ops/parity_pipeline.sh merge --apply\`"
  echo "- Otherwise:"
  echo "  - request approvals and re-run this report"
} > "$OUT"

echo "Updated $OUT"
