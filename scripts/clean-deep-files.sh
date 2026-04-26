#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

echo "Deep cleaning $repo_root"
find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
rm -rf publish .pytest_cache .mypy_cache .ruff_cache .vs
find . -maxdepth 4 -type f \( -name '*.log' -o -name '*.tmp' \) -delete

echo "Done."
