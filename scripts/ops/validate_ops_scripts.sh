#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT_DIR"

echo "Validating shell syntax..."
for f in scripts/ops/*.sh; do
  bash -n "$f"
  echo "  ✓ $f"
done

if command -v shellcheck >/dev/null 2>&1; then
  echo
  echo "Running shellcheck..."
  shellcheck scripts/ops/*.sh
else
  echo
  echo "shellcheck not found; skipped lint step."
fi

echo
echo "All ops script validation checks passed."
