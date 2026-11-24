#!/usr/bin/env bash
# Tests for scripts/demo/power.sh, using a stub `aws` that records its calls.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
source "$SCRIPT_DIR/_assert.sh"

SCRIPT="$PROJECT_ROOT/scripts/demo/power.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
mkdir "$WORK/bin"
CALLS="$WORK/calls.log"

# CLUSTER_STATUS controls what describe-clusters reports for every cluster.
cat > "$WORK/bin/aws" <<'STUB'
#!/usr/bin/env bash
echo "$*" >> "$CALLS"
if [[ "$1 $2" == "ecs describe-clusters" ]]; then
  echo "${CLUSTER_STATUS:-ACTIVE}"
fi
STUB
chmod +x "$WORK/bin/aws"

run() {
  : > "$CALLS"
  env -i PATH="$WORK/bin:$PATH" CALLS="$CALLS" "$@" bash "$SCRIPT" "${ARGS[@]}"
}

ARGS=(on)
run CLUSTER_STATUS=ACTIVE >/dev/null
assert_contains "$CALLS" "ecs update-service --cluster sebt-portal-demo-development-api --service sebt-portal-demo-development-api --desired-count 1"
assert_contains "$CALLS" "ecs update-service --cluster sebt-portal-demo-development-web --service sebt-portal-demo-development-web --desired-count 1"

ARGS=(off)
run CLUSTER_STATUS=ACTIVE >/dev/null
assert_contains "$CALLS" "--service sebt-portal-demo-development-api --desired-count 0"
assert_contains "$CALLS" "--service sebt-portal-demo-development-web --desired-count 0"

ARGS=(reset)
run CLUSTER_STATUS=ACTIVE >/dev/null
assert_contains "$CALLS" "ecs update-service --cluster sebt-portal-demo-development-api --service sebt-portal-demo-development-api --force-new-deployment"
if grep -q "development-web --" <(grep update-service "$CALLS"); then
  echo "ASSERT FAIL: reset must not touch the web service" >&2
  exit 1
fi

ARGS=(on)
run CLUSTER_STATUS=ACTIVE STATE=co PROJECT=other ENVIRONMENT=prod >/dev/null
assert_contains "$CALLS" "--cluster sebt-portal-demo-development-api --service sebt-portal-demo-development-api --desired-count 1"
if grep -q "co-\|other" "$CALLS"; then
  echo "ASSERT FAIL: exported STATE/PROJECT/ENVIRONMENT must not redirect the script" >&2
  exit 1
fi

# Inactive cluster: fail without scaling anything.
ARGS=(on)
if run CLUSTER_STATUS=INACTIVE 2>"$WORK/stderr" >/dev/null; then
  echo "ASSERT FAIL: expected failure when a cluster is not ACTIVE" >&2
  exit 1
fi
assert_contains "$WORK/stderr" "is not ACTIVE"
if grep -q "update-service" "$CALLS"; then
  echo "ASSERT FAIL: must not scale when a cluster is not ACTIVE" >&2
  exit 1
fi

# Bad arguments print usage and fail.
ARGS=(bogus)
if run 2>"$WORK/stderr" >/dev/null; then
  echo "ASSERT FAIL: expected failure for a bad action" >&2
  exit 1
fi
assert_contains "$WORK/stderr" "usage:"
ARGS=()
if run 2>"$WORK/stderr" >/dev/null; then
  echo "ASSERT FAIL: expected failure for no action" >&2
  exit 1
fi

echo "demo-power tests passed"
