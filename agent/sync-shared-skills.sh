#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
config_dir="$repo_root/agent"
config_path="$config_dir/.shared-skills-source"

usage() {
  cat <<EOF
Usage:
  $(basename "$0") [shared-skills-repo-path]

What it does:
  - finds the shared skills repo on this machine
  - stores the resolved source path in agent/.shared-skills-source
  - refreshes .agent-local/skills from agent/skills-manifest.txt

Resolution order:
  1. explicit path argument
  2. SHARED_SKILLS_REPO environment variable
  3. stored local source path in agent/.shared-skills-source
  4. common sibling/default locations such as ../skills or ~/Repos/skills
EOF
}

resolve_source_repo() {
  local candidate="${1:-}"
  local resolved=""

  [[ -n "$candidate" ]] || return 1
  [[ -d "$candidate" ]] || return 1

  resolved="$(cd "$candidate" && pwd)"
  [[ -f "$resolved/scripts/repo-skill-sync.sh" ]] || return 1

  printf '%s\n' "$resolved"
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

explicit_path="${1:-}"
stored_path=""

if [[ -f "$config_path" ]]; then
  stored_path="$(head -n 1 "$config_path")"
fi

repo_parent="$(cd "$repo_root/.." && pwd)"
candidates=(
  "$explicit_path"
  "${SHARED_SKILLS_REPO:-}"
  "$stored_path"
  "$repo_parent/skills"
  "$repo_parent/shared-skills"
  "$HOME/Repos/skills"
  "$HOME/repos/skills"
)

source_repo=""

for candidate in "${candidates[@]}"; do
  if source_repo="$(resolve_source_repo "$candidate" 2>/dev/null)"; then
    break
  fi
done

if [[ -z "$source_repo" ]]; then
  cat >&2 <<EOF
Could not find the shared skills repo for this consumer repo.

Try one of:
  ./agent/sync-shared-skills.sh /path/to/skills
  SHARED_SKILLS_REPO=/path/to/skills ./agent/sync-shared-skills.sh

This repo expects a shared skills clone that contains:
  scripts/repo-skill-sync.sh
EOF
  exit 1
fi

mkdir -p "$config_dir"
printf '%s\n' "$source_repo" > "$config_path"

exec "$source_repo/scripts/repo-skill-sync.sh" update "$repo_root"
