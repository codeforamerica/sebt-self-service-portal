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

  # State connector (contract) and the CO plugin are both in-repo (apps/connectors/*).
  # Building the monorepo builds them and runs CO's CopyPlugins target, which stages
  # the CO plugin DLLs into apps/portal/src/SEBT.Portal.Api/plugins-co.
  cd "$PROJECT_ROOT"
  dotnet build
}

# Run main function
main
