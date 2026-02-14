#!/usr/bin/env bash
set -euo pipefail

# Merge parity PRs in order once checks are green.
# Default mode is dry-run. Use --apply to actually merge.

APPLY=false
if [[ "${1:-}" == "--apply" ]]; then
  APPLY=true
fi

PARITY_PRS_STRING="${PARITY_PRS:-2 3 4 5}"
read -r -a PRS <<< "$PARITY_PRS_STRING"

for pr in "${PRS[@]}"; do
  echo "\n== PR #$pr =="

  # Wait until checks are completed; fail fast on explicit failures.
  while true; do
    OUT="$(gh pr checks "$pr" 2>&1 || true)"
    echo "$OUT"

    if echo "$OUT" | grep -qiE "\bfail\b|\berror\b|\bcancel\b"; then
      echo "❌ PR #$pr has failing checks. Stop and investigate."
      exit 1
    fi

    if echo "$OUT" | grep -qi "pending"; then
      echo "⏳ Checks still pending for PR #$pr. Waiting 20s..."
      sleep 20
      continue
    fi

    # No pending/fail markers -> treat as ready.
    break
  done

  if [[ "$APPLY" == true ]]; then
    echo "✅ Merging PR #$pr"
    gh pr merge "$pr" --squash --delete-branch
  else
    echo "🧪 Dry-run: PR #$pr is ready to merge"
  fi
done

echo "\nDone."
if [[ "$APPLY" == false ]]; then
  echo "Run with --apply to perform merges."
fi
