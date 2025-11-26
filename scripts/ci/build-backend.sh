#!/bin/bash
# Backend Build Script
# Environment-agnostic: Works in Docker, GitHub Actions, and local development
#
# Usage:
#   ./scripts/ci/build-backend.sh [--skip-restore] [--configuration <config>]
#
# Options:
#   --skip-restore           Skip dotnet restore (useful if already restored)
#   --configuration <config> Build configuration (Debug|Release, default: Release in CI)

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

# Parse arguments
SKIP_RESTORE=false
CONFIGURATION="Debug"

# Auto-detect CI environment
if [ "${CI:-false}" = "true" ] || [ -n "${GITHUB_ACTIONS:-}" ]; then
  CONFIGURATION="Release"
fi

while [[ $# -gt 0 ]]; do
  case $1 in
    --skip-restore)
      SKIP_RESTORE=true
      shift
      ;;
    --configuration)
      CONFIGURATION="$2"
      shift 2
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

# Environment detection
detect_environment() {
  if [ -f /.dockerenv ]; then
    log_info "Running in Docker container"
    return 0
  elif [ -n "${DOCKER_HOST:-}" ]; then
    log_info "Docker environment detected"
    return 0
  elif [ -n "${GITHUB_ACTIONS:-}" ]; then
    log_info "Running in GitHub Actions"
    return 0
  else
    log_info "Running in local/native environment"
    return 0
  fi
}

# Check prerequisites
check_prerequisites() {
  log_info "Checking prerequisites..."

  # Check .NET SDK
  if ! command -v dotnet &> /dev/null; then
    log_error ".NET SDK is not installed"
    log_info "Install from: https://dotnet.microsoft.com/download"
    exit 1
  fi

  local dotnet_version=$(dotnet --version)
  log_success ".NET SDK $dotnet_version found"

  # Check for .NET 10
  if ! dotnet --list-sdks | grep -q "^10\."; then
    log_warning ".NET 10 SDK not found, but continuing..."
  else
    log_success ".NET 10 SDK available"
  fi
}

# Restore dependencies
restore_dependencies() {
  if [ "$SKIP_RESTORE" = true ]; then
    log_info "Skipping dependency restoration (--skip-restore)"
    return 0
  fi

  log_info "Restoring .NET dependencies..."
  cd "$PROJECT_ROOT"

  dotnet restore --verbosity minimal

  log_success "Dependencies restored"
}

# Build backend
build_backend() {
  log_info "Building backend (Configuration: $CONFIGURATION)..."
  cd "$PROJECT_ROOT"

  # Build with no restore since we already did that
  dotnet build \
    --configuration "$CONFIGURATION" \
    --no-restore \
    --verbosity minimal

  log_success "Backend build complete"
}

# Display build artifacts
show_artifacts() {
  log_info "Build artifacts:"

  # Find all bin directories
  find "$PROJECT_ROOT/src" -type d -name "bin" | while read -r bin_dir; do
    if [ -d "$bin_dir/$CONFIGURATION" ]; then
      local project_name=$(basename "$(dirname "$bin_dir")")
      local artifact_size=$(du -sh "$bin_dir/$CONFIGURATION" 2>/dev/null | cut -f1 || echo "N/A")
      log_info "  $project_name: $artifact_size"
    fi
  done
}

# Main execution
main() {
  log_info "=== Backend Build Script ==="
  log_info "Project Root: $PROJECT_ROOT"
  log_info "Configuration: $CONFIGURATION"
  echo ""

  detect_environment
  check_prerequisites
  restore_dependencies
  build_backend

  echo ""
  show_artifacts
  echo ""
  log_success "=== Backend build completed successfully ==="
}

# Run main function
main
