#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

echo "Cleaning temporary/build artifacts in $repo_root"

find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
rm -rf .pytest_cache .mypy_cache .ruff_cache
find . -maxdepth 2 -type f \( -name '*.log' -o -name '*.tmp' \) -delete

echo "Done."
