#!/usr/bin/env node
/**
 * Generate Design Tokens for Next.js
 *
 * Transforms state-specific design tokens from Figma into CSS custom properties.
 * Adapted from Vite's tokens-to-scss.js for Next.js architecture.
 *
 * Usage:
 *   node scripts/generate-tokens.js           # Defaults to DC
 *   STATE=co node scripts/generate-tokens.js  # Colorado state
 *
 * Features:
 * - Smart caching: Only regenerates if source JSON changed
 * - CSS custom properties: Modern, framework-agnostic approach
 * - Build integration: Auto-runs during predev and prebuild
 *
 * Workflow:
 * 1. Read the state's token file (see state-token-source.js)
 * 2. Check if transformation needed (timestamp and source based)
 * 3. Extract 'theme' object (USWDS has 'system' tokens built-in)
 * 4. Convert to CSS custom properties with semantic naming
 * 5. Output to design/tokens.css (imported in layout.tsx)
 */

import './load-env.js';
import { readFileSync, writeFileSync, existsSync, statSync } from 'fs';
import { join, dirname, relative } from 'path';
import { fileURLToPath } from 'url';
import { resolveStateTokens } from './state-token-source.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const rootDir = join(__dirname, '..', '..');
const rel = p => relative(rootDir, p);

const USWDS_PREFIXES = new Set([
  'color', 'font', 'link', 'focus', 'button',
  'global', 'style', 'text'
]);

function getStatePaths(state) {
  const source = resolveStateTokens(state);

  return {
    input: source.path,
    sourceLabel: source.label,
    output: join(rootDir, 'design', 'tokens.css')
  };
}

function shouldInjectColorPrefix(tokenName, tokenType) {
  if (!tokenName.startsWith('theme-') || tokenType !== 'color') {
    return false;
  }

  for (const prefix of USWDS_PREFIXES) {
    if (tokenName.includes(`-${prefix}-`)) {
      return false;
    }
  }

  return true;
}

function toCssVariableName(name) {
  // Convert to CSS custom property format: --theme-primary
  return name.startsWith('--') ? name : `--${name}`;
}

function toCssValue(value) {
  if (typeof value === 'string') {
    // Handle token references: {system.red-cool-50} -> var(--system-red-cool-50)
    if (value.startsWith('{') && value.endsWith('}')) {
      const tokenName = value.slice(1, -1).replace(/\./g, '-');
      return `var(--${tokenName})`;
    }
    // Return as-is for colors, remove quotes for strings
    return value;
  }
  return value;
}

function processThemeObject(obj, prefix = '') {
  const variables = [];

  for (const [key, value] of Object.entries(obj)) {
    if (value && typeof value === 'object' && '$value' in value) {
      let tokenName = prefix ? `${prefix}-${key}` : key;

      // Inject 'color' prefix for theme colors without specific USWDS prefix
      if (shouldInjectColorPrefix(tokenName, value.$type)) {
        tokenName = tokenName.replace('theme-', 'theme-color-');
      }

      const cssName = toCssVariableName(tokenName);
      const cssValue = toCssValue(value.$value);

      variables.push({
        name: cssName,
        value: cssValue,
        type: value.$type,
        description: value.$description || ''
      });
    } else if (value && typeof value === 'object' && !('$value' in value)) {
      // Recursively process nested objects
      const nestedPrefix = prefix ? `${prefix}-${key}` : key;
      variables.push(...processThemeObject(value, nestedPrefix));
    }
  }

  return variables;
}

function sourceLine(sourceLabel) {
  return ` * Source: ${sourceLabel} (theme object only)`;
}

/**
 * Timestamps alone can't tell when tokens.css came from another state or token
 * source, so the source recorded in its header must match too.
 */
export function needsRegeneration(inputPath, outputPath, sourceLabel) {
  if (!existsSync(outputPath)) {
    return true;
  }

  if (!readFileSync(outputPath, 'utf8').includes(sourceLine(sourceLabel))) {
    return true;
  }

  return statSync(inputPath).mtimeMs > statSync(outputPath).mtimeMs;
}

function formatCssVariable({ name, value, description }) {
  const comment = description
    ? `  /* ${description.split('\n')[0].trim()} */\n`
    : '';
  return `${comment}  ${name}: ${value};`;
}

function processState(state) {
  const paths = getStatePaths(state);

  // Check if state token file exists
  if (!existsSync(paths.input)) {
    console.log(`⚠️  No token file found for ${state.toUpperCase()} at:`);
    console.log(`   ${rel(paths.input)}`);
    console.log('   Skipping token generation.');
    return { state, skipped: true, success: true };
  }

  // Check if regeneration needed
  if (!needsRegeneration(paths.input, paths.output, paths.sourceLabel)) {
    console.log(`⚡ Tokens unchanged for ${state.toUpperCase()}`);
    console.log(`   ${rel(paths.input)} → ${rel(paths.output)}`);
    return { state, cached: true, success: true };
  }

  console.log(`  Reading: ${rel(paths.input)}`);
  const stateJson = JSON.parse(readFileSync(paths.input, 'utf8'));

  if (!stateJson.theme) {
    throw new Error(`No "theme" object found in ${state}.json`);
  }

  console.log(`✅ Found theme object for ${state.toUpperCase()}`);

  // Process theme tokens
  const variables = processThemeObject(stateJson.theme);
  console.log(`✅ Extracted ${variables.length} theme variables`);

  // Generate CSS content
  const cssContent = `/**
 * State-Specific Design Tokens - ${state.toUpperCase()}
 *
 * Auto-generated from Figma Tokens Studio
${sourceLine(paths.sourceLabel)}
 * DO NOT EDIT DIRECTLY - This file is regenerated from design tokens
 *
 * Generated: ${new Date().toISOString()}
 */

:root {
${variables.map(formatCssVariable).join('\n')}
}

/* Apply theme to USWDS components */
.usa-button--primary {
  background-color: var(--theme-primary, var(--theme-color-primary));
  border-radius: var(--theme-button-border-radius, var(--radius-pill));
}

.usa-link,
a {
  color: var(--theme-link-color);
}

.usa-focus,
*:focus {
  outline-color: var(--theme-focus-color);
}
`;

  writeFileSync(paths.output, cssContent, 'utf8');

  console.log(`✅ Generated ${variables.length} CSS custom properties for ${state.toUpperCase()}`);
  console.log(`   ${rel(paths.output)}`);

  return { state, cached: false, success: true, count: variables.length };
}

function main() {
  const state = (process.env.STATE || process.env.NEXT_PUBLIC_STATE || 'dc').toLowerCase();

  try {
    console.log(`🎨 Generating design tokens for ${state.toUpperCase()}...`);

    const result = processState(state);

    if (result.skipped) {
      console.log('⚠️  Token generation skipped - no design tokens available');
      process.exit(0);
    }

    if (result.success) {
      console.log(`✅ ${state.toUpperCase()} tokens generated successfully`);
      process.exit(0);
    }
  } catch (error) {
    console.error(`❌ Failed to generate tokens for ${state.toUpperCase()}:`);
    console.error(`   ${error.message}\n`);
    process.exit(1);
  }
}

// Only auto-run when invoked as a script, not when imported by tests.
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  main();
}
