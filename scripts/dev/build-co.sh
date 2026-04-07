#!/bin/bash

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
MULTI_PROJECT_ROOT="$(cd "$PROJECT_ROOT/.." && pwd)"

# Parse arguments
CONFIGURATION="Debug"

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

# Main execution
main() {
  log_info "=== Backend Build - CO Local Dev ==="
  log_info "Project Root: $PROJECT_ROOT"
  log_info "Configuration: $CONFIGURATION"
  echo ""

  check_prerequisites

  # Build state connector interfaces
  cd "$MULTI_PROJECT_ROOT/sebt-self-service-portal-state-connector"
  dotnet build

  # Build CO plugin
  cd "$MULTI_PROJECT_ROOT/sebt-self-service-portal-co-connector"
  dotnet build

  # Build main app
  cd "$MULTI_PROJECT_ROOT/sebt-self-service-portal"
  dotnet build
}

# Run main function
main
