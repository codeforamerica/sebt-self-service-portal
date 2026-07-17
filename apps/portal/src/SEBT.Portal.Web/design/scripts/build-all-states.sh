#!/bin/bash

##
# Build All States - Multi-State Deployment Script
#
# This script builds the Next.js application for each state (DC and CO)
# with state-specific design tokens, creating separate deployments.
#
# Usage:
#   ./scripts/build-all-states.sh
#
# Output:
#   .next-dc/  - DC state build
#   .next-co/  - CO state build
##

set -e  # Exit on error

echo "🏗️  Building SEBT Portal for all states..."
echo ""

states=("dc" "co")
failed_builds=()

for state in "${states[@]}"; do
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  echo "📦 Building for ${state^^} state..."
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  echo ""

  # Set state environment variable
  export STATE=$state

  # Clean previous build for this state
  if [ -d ".next-$state" ]; then
    echo "🧹 Cleaning previous $state build..."
    rm -rf ".next-$state"
  fi

  # Run prebuild (token generation, validation, type generation)
  echo "🎨 Generating $state tokens..."
  pnpm prebuild || {
    echo "❌ Failed to generate tokens for $state"
    failed_builds+=("$state")
    continue
  }

  # Build Next.js app
  echo "⚡ Building Next.js app for $state..."
  next build || {
    echo "❌ Failed to build Next.js app for $state"
    failed_builds+=("$state")
    continue
  }

  # Move build output to state-specific directory
  mv .next ".next-$state"

  echo ""
  echo "✅ ${state^^} build complete!"
  echo "   Output: .next-$state/"
  echo ""
done

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📊 Build Summary"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

if [ ${#failed_builds[@]} -eq 0 ]; then
  echo "✅ All state builds completed successfully!"
  echo ""
  echo "Deployment artifacts:"
  for state in "${states[@]}"; do
    echo "  • .next-$state/ → ${state}.sebt-portal.gov"
  done
  echo ""
  exit 0
else
  echo "❌ Some builds failed:"
  for state in "${failed_builds[@]}"; do
    echo "  • $state"
  done
  echo ""
  exit 1
fi
