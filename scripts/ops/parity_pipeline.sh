#!/usr/bin/env bash
set -euo pipefail

# Unified helper for parity PR operations.
# Commands:
#   status
#   request <reviewer1> [reviewer2...]
#   comment
#   merge [--apply]
#   watch

ROOT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT_DIR"

cmd="${1:-status}"
shift || true

case "$cmd" in
  status)
    ./scripts/ops/check_parity_pr_merge_readiness.sh
    ;;
  request)
    if [[ $# -lt 1 ]]; then
      echo "Usage: $0 request <reviewer1> [reviewer2 ...]"
      exit 1
    fi
    ./scripts/ops/request_parity_reviews.sh "$@"
    ;;
  comment)
    ./scripts/ops/comment_parity_pr_blocker.sh
    ;;
  merge)
    if [[ "${1:-}" == "--apply" ]]; then
      ./scripts/ops/merge_parity_prs.sh --apply
    else
      ./scripts/ops/merge_parity_prs.sh
    fi
    ;;
  watch)
    ./scripts/ops/watch_and_merge_parity_prs.sh
    ;;
  *)
    cat <<EOF
Unknown command: $cmd

Usage:
  $0 status
  $0 request <reviewer1> [reviewer2 ...]
  $0 comment
  $0 merge [--apply]
  $0 watch
EOF
    exit 1
    ;;
esac
