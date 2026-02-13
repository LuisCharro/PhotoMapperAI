#!/usr/bin/env bash
set -euo pipefail

PRS=(2 3 4 5)

BODY="Automation status update: CI checks are green, but merge is blocked by repository rule requiring 1 approval from another reviewer (authors cannot self-approve).

To unblock this PR: add one approval review, then run:
\`./scripts/ops/check_parity_pr_merge_readiness.sh\`
\`./scripts/ops/merge_parity_prs.sh --apply\`"

for pr in "${PRS[@]}"; do
  echo "Posting blocker comment to PR #$pr"
  gh pr comment "$pr" --body "$BODY"
done

echo "Done."
