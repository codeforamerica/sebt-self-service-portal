#!/usr/bin/env node
/**
 * Generate Design Tokens for All States
 *
 * This script generates SCSS/CSS variables for all configured states.
 * Run during build to include all state tokens in the build artifact.
 *
 * Usage:
 *   node scripts/generate-all-tokens.js
 *
 * Environment:
 *   None required - processes all states automatically
 */

import { fileURLToPath } from 'url';
import { dirname, join } from 'path';
import { existsSync, readdirSync } from 'fs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const rootDir = join(__dirname, '..', '..');

// Supported states configuration
const STATES = ['dc', 'co']; // Add new states here as they're added

console.log('🎨 Generating design tokens for all states...\n');

// Check if design tokens directory exists
const designDir = join(rootDir, 'design', 'states');
if (!existsSync(designDir)) {
  console.log('⚠️  No design/states/ directory found.');
  console.log('   Token generation will be skipped until design tokens are added.\n');
  console.log('   To add design tokens:');
  console.log('   1. Create design/states/ directory');
  console.log('   2. Add {state}.json files (e.g., dc.json, co.json)');
  console.log('   3. Run this script again\n');
  process.exit(0);
}

// Discover available token files
const availableTokens = readdirSync(designDir)
  .filter(file => file.endsWith('.json'))
  .map(file => file.replace('.json', ''));

if (availableTokens.length === 0) {
  console.log('⚠️  No token files found in design/states/');
  console.log('   Add {state}.json files and run again.\n');
  process.exit(0);
}

console.log(`📂 Found ${availableTokens.length} state(s): ${availableTokens.join(', ')}\n`);

// Generate tokens for each state
let successCount = 0;
let failureCount = 0;

for (const state of STATES) {
  if (!availableTokens.includes(state)) {
    console.log(`⏭️  Skipping ${state.toUpperCase()} - no token file found`);
    continue;
  }

  try {
    console.log(`🔨 Generating tokens for ${state.toUpperCase()}...`);

    // Run token generation for this state using child_process
    const { execSync } = await import('child_process');

    // Generate CSS custom properties (for runtime use)
    execSync(`STATE=${state} node design/scripts/generate-tokens.js`, {
      cwd: rootDir,
      stdio: 'inherit'
    });

    // Generate SASS variables (for USWDS compile-time theming)
    execSync(`STATE=${state} node design/scripts/generate-sass-tokens.js`, {
      cwd: rootDir,
      stdio: 'inherit'
    });

    // Generate fonts.ts (for Next.js font loading)
    execSync(`STATE=${state} node design/scripts/generate-fonts.js`, {
      cwd: rootDir,
      stdio: 'inherit'
    });

    successCount++;
  } catch (error) {
    console.error(`❌ Failed to generate tokens for ${state.toUpperCase()}:`);
    console.error(`   ${error.message}\n`);
    failureCount++;
  }
}

// Summary
console.log('─'.repeat(50));
console.log(`📊 Token Generation Summary:`);
console.log(`   ✅ Success: ${successCount}`);
console.log(`   ❌ Failed:  ${failureCount}`);
console.log(`   ⏭️  Skipped: ${STATES.length - successCount - failureCount}`);
console.log('─'.repeat(50));

if (failureCount > 0) {
  console.log('\n⚠️  Some tokens failed to generate. Check errors above.');
  process.exit(1);
}

console.log('\n✨ All configured state tokens generated successfully!');
console.log('   Build artifact will include tokens for all states.\n');
