#!/bin/bash
# Backend Test Script
# Environment-agnostic: Works in Docker, GitHub Actions, and local development
#
# Usage:
#   ./scripts/ci/test-backend.sh [--skip-build] [--configuration <config>] [--coverage]
#
# Options:
#   --skip-build             Skip build step (useful if already built)
#   --configuration <config> Test configuration (Debug|Release, default: Release in CI)
#   --coverage               Collect code coverage (requires coverlet)

set -e  # Exit on error
set -u  # Exit on undefined variable

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory (POSIX-compatible)
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Parse arguments
SKIP_BUILD=false
CONFIGURATION="Debug"
COLLECT_COVERAGE=false

# Auto-detect CI environment
if [ "${CI:-false}" = "true" ] || [ -n "${GITHUB_ACTIONS:-}" ]; then
  CONFIGURATION="Release"
fi

while [[ $# -gt 0 ]]; do
  case $1 in
    --skip-build)
      SKIP_BUILD=true
      shift
      ;;
    --configuration)
      CONFIGURATION="$2"
      shift 2
      ;;
    --coverage)
      COLLECT_COVERAGE=true
      shift
      ;;
    *)
      echo "Unknown option: $1"
      exit 1
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

  if ! command -v dotnet &> /dev/null; then
    log_error ".NET SDK is not installed"
    exit 1
  fi

  log_success ".NET SDK $(dotnet --version) found"
}

# Build projects (if needed)
build_projects() {
  if [ "$SKIP_BUILD" = true ]; then
    log_info "Skipping build (--skip-build)"
    return 0
  fi

  log_info "Building projects for testing..."
  cd "$PROJECT_ROOT"

  dotnet build \
    --configuration "$CONFIGURATION" \
    --verbosity minimal

  log_success "Build complete"
}

# Run tests
run_tests() {
  log_info "Running .NET tests..."
  cd "$PROJECT_ROOT"

  local test_args=(
    "test"
    "--configuration" "$CONFIGURATION"
    "--no-build"
    "--verbosity" "normal"
  )

  # Add coverage collection if requested
  if [ "$COLLECT_COVERAGE" = true ]; then
    log_info "Code coverage collection enabled"
    test_args+=(
      "--collect:\"XPlat Code Coverage\""
      "--results-directory" "./TestResults"
    )
  fi

  dotnet "${test_args[@]}"

  log_success "All tests passed"
}

# Display coverage summary
show_coverage() {
  if [ "$COLLECT_COVERAGE" = false ]; then
    return 0
  fi

  log_info "Code coverage summary:"

  # Find coverage files
  if [ -d "./TestResults" ]; then
    local coverage_files=$(find ./TestResults -name "coverage.cobertura.xml" 2>/dev/null)
    if [ -n "$coverage_files" ]; then
      log_success "Coverage reports generated in ./TestResults"
      log_info "To view coverage, install reportgenerator:"
      log_info "  dotnet tool install -g dotnet-reportgenerator-globaltool"
      log_info "  reportgenerator -reports:./TestResults/**/coverage.cobertura.xml -targetdir:./CoverageReport"
    else
      log_warning "No coverage files found"
    fi
  fi
}

# Display test summary
show_test_summary() {
  log_info "Test summary:"

  # Count test projects
  local test_projects=$(find "$PROJECT_ROOT/test" -name "*.csproj" 2>/dev/null | wc -l)
  log_info "  Test projects: $test_projects"

  # Show test results directory if exists
  if [ -d "./TestResults" ]; then
    local result_files=$(find ./TestResults -name "*.trx" 2>/dev/null | wc -l)
    log_info "  Test result files: $result_files"
  fi
}

# Main execution
main() {
  log_info "=== Backend Test Script ==="
  log_info "Project Root: $PROJECT_ROOT"
  log_info "Configuration: $CONFIGURATION"
  log_info "Coverage: $COLLECT_COVERAGE"
  echo ""

  check_prerequisites
  build_projects
  run_tests

  echo ""
  show_coverage
  show_test_summary
  echo ""
  log_success "=== All backend tests completed successfully ==="
}

# Run main function
main
