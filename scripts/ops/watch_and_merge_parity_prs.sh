#!/usr/bin/env bash
set -euo pipefail

# Poll parity PR readiness and merge automatically once all gates are satisfied.
# Requires approvals from another reviewer (cannot self-approve).

INTERVAL_SECONDS="${INTERVAL_SECONDS:-60}"
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-7200}"

elapsed=0

echo "Watching parity PRs (timeout=${TIMEOUT_SECONDS}s, interval=${INTERVAL_SECONDS}s)..."

while true; do
  echo "\n[$(date '+%Y-%m-%d %H:%M:%S')] Checking readiness..."

  if ./scripts/ops/check_parity_pr_merge_readiness.sh | tee /tmp/parity_readiness.out; then
    if grep -q "❌" /tmp/parity_readiness.out; then
      :
    else
      echo "✅ All PRs look ready. Executing merge pipeline (--apply)..."
      ./scripts/ops/merge_parity_prs.sh --apply
      echo "🎉 Merge pipeline completed."
      exit 0
    fi
  fi

  if (( elapsed >= TIMEOUT_SECONDS )); then
    echo "⏰ Timeout reached without full readiness. Exiting with code 2."
    exit 2
  fi

  sleep "$INTERVAL_SECONDS"
  elapsed=$((elapsed + INTERVAL_SECONDS))
done
