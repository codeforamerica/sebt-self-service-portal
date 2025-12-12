#!/bin/bash
# ACT Test Runner for SEBT Portal
# Test GitHub Actions workflows locally before pushing
#
# Quick Start:
#   ./act-test.sh list        # List workflows
#   ./act-test.sh job build   # Run CI build
#
# Full Guide: docs/development/local-ci-testing.md
# Usage: ./act-test.sh [options]

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_help() {
  cat <<EOF
ACT Test Runner for SEBT Portal

Usage: ./act-test.sh [command] [options]

Commands:
  list              List all workflows and jobs
  run               Run all workflows (default)
  push              Simulate push event
  pr                Simulate pull request event
  job <job-name>    Run specific job
  dry-run           Dry run (show what would execute)
  graph             Show workflow dependency graph

Options:
  -v, --verbose     Verbose output
  -h, --help        Show this help message

Examples:
  ./act-test.sh                    # Run all workflows (push event)
  ./act-test.sh list               # List available workflows
  ./act-test.sh job build          # Run only the 'build' job
  ./act-test.sh pr                 # Test pull request workflow
  ./act-test.sh dry-run            # Show what would run
  ./act-test.sh graph              # Show workflow graph

EOF
}

# Parse arguments
COMMAND="${1:-run}"
VERBOSE=""

case "$COMMAND" in
  -h|--help|help)
    print_help
    exit 0
    ;;
  -v|--verbose)
    VERBOSE="--verbose"
    COMMAND="run"
    ;;
esac

# Ensure Docker is running
if ! docker info > /dev/null 2>&1; then
  echo -e "${RED}❌ Docker is not running. Please start Docker Desktop.${NC}"
  exit 1
fi

echo -e "${GREEN}🐳 Docker is running${NC}"

# Execute ACT command
case "$COMMAND" in
  list)
    echo -e "${YELLOW}📋 Available workflows and jobs:${NC}"
    act -l
    ;;

  run)
    echo -e "${YELLOW}🚀 Running all workflows (push event)...${NC}"
    act push $VERBOSE
    ;;

  push)
    echo -e "${YELLOW}🚀 Simulating push event...${NC}"
    act push $VERBOSE
    ;;

  pr)
    echo -e "${YELLOW}🔀 Simulating pull request event...${NC}"
    act pull_request --eventpath .github/workflows/events/pull_request.json $VERBOSE
    ;;

  job)
    JOB_NAME="$2"
    if [ -z "$JOB_NAME" ]; then
      echo -e "${RED}❌ Job name required. Usage: ./act-test.sh job <job-name>${NC}"
      echo -e "${YELLOW}Run './act-test.sh list' to see available jobs${NC}"
      exit 1
    fi
    echo -e "${YELLOW}🎯 Running job: $JOB_NAME${NC}"
    act -j "$JOB_NAME" $VERBOSE
    ;;

  dry-run)
    echo -e "${YELLOW}🔍 Dry run (showing what would execute)...${NC}"
    act -n
    ;;

  graph)
    echo -e "${YELLOW}📊 Workflow dependency graph:${NC}"
    act -g
    ;;

  *)
    echo -e "${RED}❌ Unknown command: $COMMAND${NC}"
    print_help
    exit 1
    ;;
esac

echo -e "${GREEN}✅ ACT execution complete${NC}"
