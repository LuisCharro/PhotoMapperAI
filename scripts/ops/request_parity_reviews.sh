#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <reviewer1> [reviewer2 ...]"
  exit 1
fi

PRS=(2 3 4 5)

for reviewer in "$@"; do
  for pr in "${PRS[@]}"; do
    echo "Requesting reviewer '$reviewer' on PR #$pr"
    gh pr edit "$pr" --add-reviewer "$reviewer"
  done
done

echo "Done."
