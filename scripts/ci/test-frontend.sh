#!/bin/bash
# Frontend Test Script
# Environment-agnostic: Works in Docker, GitHub Actions, and local development
#
# Usage:
#   ./scripts/ci/test-frontend.sh [--skip-install]
#
# Options:
#   --skip-install    Skip dependency installation (useful if already installed)

set -e  # Exit on error
set -u  # Exit on undefined variable

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
FRONTEND_DIR="$PROJECT_ROOT/src/SEBT.Portal.Web"

# Parse arguments
SKIP_INSTALL=false

for arg in "$@"; do
  case $arg in
    --skip-install)
      SKIP_INSTALL=true
      shift
      ;;
  esac
done

# Logging functions
log_info() {
  echo -e "${BLUE}ℹ️  $1${NC}"
}

log_success() {
  echo -e "${GREEN}✅ $1${NC}"
}

log_warning() {
  echo -e "${YELLOW}⚠️  $1${NC}"
}

log_error() {
  echo -e "${RED}❌ $1${NC}"
}

# Check prerequisites
check_prerequisites() {
  log_info "Checking prerequisites..."

  if ! command -v node &> /dev/null; then
    log_error "Node.js is not installed"
    exit 1
  fi

  if ! command -v pnpm &> /dev/null; then
    log_error "pnpm is not installed"
    exit 1
  fi

  log_success "Prerequisites verified"
}

# Install dependencies
install_dependencies() {
  if [ "$SKIP_INSTALL" = true ]; then
    log_info "Skipping dependency installation (--skip-install)"
    return 0
  fi

  log_info "Installing dependencies..."
  cd "$PROJECT_ROOT"
  pnpm install --frozen-lockfile --prefer-offline
  log_success "Dependencies installed"
}

# Run linting
run_lint() {
  log_info "Running linter..."
  cd "$FRONTEND_DIR"

  # Check if lint script exists
  if grep -q '"lint"' package.json 2>/dev/null; then
    pnpm run lint
    log_success "Linting passed"
  else
    log_warning "No lint script found in package.json, skipping"
  fi
}

# Run tests
run_tests() {
  log_info "Running tests..."
  cd "$FRONTEND_DIR"

  # Check if test script exists
  if grep -q '"test"' package.json 2>/dev/null; then
    pnpm run test
    log_success "Tests passed"
  else
    log_warning "No test script found in package.json, skipping"
  fi
}

# Run type checking
run_type_check() {
  log_info "Running TypeScript type checking..."
  cd "$FRONTEND_DIR"

  pnpm exec tsc --noEmit
  log_success "Type checking passed"
}

# Main execution
main() {
  log_info "=== Frontend Test Script ==="
  log_info "Project Root: $PROJECT_ROOT"
  log_info "Frontend Dir: $FRONTEND_DIR"
  echo ""

  check_prerequisites
  install_dependencies
  run_type_check
  run_lint
  run_tests

  echo ""
  log_success "=== All frontend tests passed ==="
}

# Run main function
main "$@"
