#!/usr/bin/env bash
# The frontend CI script must run copy:validate for both web apps and fail when
# it fails. pnpm is replaced by a stub that records each call, so this test runs
# no real install, lint, or test suite.
source "$(dirname "$0")/_assert.sh"

REPO_ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
SCRIPT="$REPO_ROOT/.github/workflows/scripts/test-frontend.sh"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

CALLS="$WORK_DIR/pnpm-calls.log"
mkdir -p "$WORK_DIR/bin"
cat > "$WORK_DIR/bin/pnpm" <<'STUB'
#!/usr/bin/env bash
echo "$(basename "$PWD") :: $*" >> "$PNPM_CALLS"
if [ "${FAIL_COPY_VALIDATE:-}" = "1" ] && [[ "$*" == *copy:validate* ]]; then
  exit 1
fi
exit 0
STUB
chmod +x "$WORK_DIR/bin/pnpm"

run_script() {
  : > "$CALLS"
  PATH="$WORK_DIR/bin:$PATH" PNPM_CALLS="$CALLS" bash "$SCRIPT" --skip-install > "$WORK_DIR/output.log" 2>&1
}

echo "validates content for the portal and the enrollment checker"
run_script
assert_contains "$CALLS" "SEBT.Portal.Web :: run copy:validate"
assert_contains "$CALLS" "SEBT.EnrollmentChecker.Web :: run copy:validate"

echo "fails when content validation fails, after checking both apps"
status=0
FAIL_COPY_VALIDATE=1 run_script || status=$?
assert_eq "$status" "1"
assert_contains "$CALLS" "SEBT.Portal.Web :: run copy:validate"
assert_contains "$CALLS" "SEBT.EnrollmentChecker.Web :: run copy:validate"
