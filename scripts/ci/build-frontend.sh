#!/bin/bash
# Frontend Build Script
# Environment-agnostic: Works in Docker, GitHub Actions, and local development
#
# Usage:
#   ./scripts/ci/build-frontend.sh [--skip-install] [--production]
#
# Options:
#   --skip-install    Skip dependency installation (useful if already installed)
#   --production      Build for production (default: true in CI, false locally)

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
PRODUCTION=false

# Auto-detect CI environment
if [ "${CI:-false}" = "true" ] || [ -n "${GITHUB_ACTIONS:-}" ]; then
  PRODUCTION=true
fi

for arg in "$@"; do
  case $arg in
    --skip-install)
      SKIP_INSTALL=true
      shift
      ;;
    --production)
      PRODUCTION=true
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

  # Check Node.js
  if ! command -v node &> /dev/null; then
    log_error "Node.js is not installed"
    exit 1
  fi
  log_success "Node.js $(node --version) found"

  # Check pnpm
  if ! command -v pnpm &> /dev/null; then
    log_error "pnpm is not installed"
    log_info "Install with: npm install -g pnpm"
    exit 1
  fi
  log_success "pnpm $(pnpm --version) found"
}

# Install dependencies
install_dependencies() {
  if [ "$SKIP_INSTALL" = true ]; then
    log_info "Skipping dependency installation (--skip-install)"
    return 0
  fi

  log_info "Installing dependencies..."
  cd "$PROJECT_ROOT"

  if [ "$PRODUCTION" = true ]; then
    pnpm install --frozen-lockfile --prefer-offline
  else
    pnpm install
  fi

  log_success "Dependencies installed"
}

# Build frontend
build_frontend() {
  log_info "Building frontend..."
  cd "$FRONTEND_DIR"

  # Run prebuild script (tokens, fonts, validation)
  log_info "Running prebuild steps..."
  node scripts/validate-tokens.js
  node scripts/tokens-to-scss.js
  node scripts/inject-fonts.js
  node scripts/generate-token-types.js

  # TypeScript compilation
  log_info "Compiling TypeScript..."
  pnpm exec tsc --noEmit

  # Vite build
  log_info "Building with Vite..."
  if [ "$PRODUCTION" = true ]; then
    pnpm exec vite build --mode production
  else
    pnpm exec vite build
  fi

  log_success "Frontend build complete"
}

# Main execution
main() {
  log_info "=== Frontend Build Script ==="
  log_info "Project Root: $PROJECT_ROOT"
  log_info "Frontend Dir: $FRONTEND_DIR"
  log_info "Production: $PRODUCTION"
  echo ""

  detect_environment
  check_prerequisites
  install_dependencies
  build_frontend

  echo ""
  log_success "=== Frontend build completed successfully ==="

  # Display output info
  if [ -d "$FRONTEND_DIR/dist" ]; then
    log_info "Build output: $FRONTEND_DIR/dist"
    log_info "Build size: $(du -sh "$FRONTEND_DIR/dist" | cut -f1)"
  fi
}

# Run main function
main "$@"
