#!/usr/bin/env sh
set -e

# USWDS Asset Copy Script
# Copies static assets from USWDS node_modules to public directory
# Runs during postinstall to ensure assets are available for Next.js

USWDS_DIST="node_modules/@uswds/uswds/dist"
PUBLIC_DIR="public"

# Check if assets already exist (skip if already copied)
if [ -d "$PUBLIC_DIR/fonts" ] && [ "$(ls -A $PUBLIC_DIR/fonts)" ]; then
  echo "✓ USWDS assets already exist, skipping copy"
  exit 0
fi

echo "Copying USWDS assets to public directory..."

# Create directories
mkdir -p "$PUBLIC_DIR/js" "$PUBLIC_DIR/css" "$PUBLIC_DIR/fonts" "$PUBLIC_DIR/img"

# Copy assets
echo "  → Copying JavaScript..."
cp "$USWDS_DIST/js/uswds-init.min.js" "$PUBLIC_DIR/js/"

echo "  → Copying CSS..."
cp "$USWDS_DIST/css/uswds.min.css" "$PUBLIC_DIR/css/"

echo "  → Copying fonts..."
cp -r "$USWDS_DIST/fonts/"* "$PUBLIC_DIR/fonts/"

echo "  → Copying images..."
cp -r "$USWDS_DIST/img" "$PUBLIC_DIR/"

echo "✓ USWDS assets copied successfully"
