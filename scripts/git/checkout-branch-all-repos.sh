#!/usr/bin/env bash
# Create or check out the same branch name in the SEBT repos (monorepo + DC connector).
#
# Usage:
#   ./scripts/git/checkout-branch-all-repos.sh feature/my-branch
#
# Override repo root if your clones live elsewhere:
#   REPOS_ROOT=/path/to/parent ./scripts/git/checkout-branch-all-repos.sh feature/my-branch
#
set -euo pipefail

BRANCH="${1:?usage: $0 <branch-name>}"

REPOS_ROOT="${REPOS_ROOT:-$HOME/Projects}"

# state + co connectors are now in-repo (apps/connectors/*); only the DC connector is external.
REPOS=(
  sebt-self-service-portal
  sebt-self-service-portal-dc-connector
)

checkout_one() {
  local repo_dir="$1"
  echo ""
  echo "=== $(basename "$repo_dir") ==="
  cd "$repo_dir"

  git fetch origin --quiet 2>/dev/null || true

  if git show-ref --verify --quiet "refs/heads/$BRANCH"; then
    git checkout "$BRANCH"
    return 0
  fi

  if git show-ref --verify --quiet "refs/remotes/origin/$BRANCH"; then
    git checkout -b "$BRANCH" "origin/$BRANCH"
    return 0
  fi

  git checkout -b "$BRANCH"
}

failed=0
for name in "${REPOS[@]}"; do
  dir="$REPOS_ROOT/$name"
  if [[ ! -d "$dir/.git" ]]; then
    echo "warning: skip (not a git repo): $dir" >&2
    failed=1
    continue
  fi
  checkout_one "$dir" || failed=1
done

exit "$failed"
