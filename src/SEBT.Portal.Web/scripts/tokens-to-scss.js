#!/usr/bin/env node
/**
 * Convert Figma Tokens Studio JSON to USWDS SCSS Variables
 *
 * Transforms state-specific design tokens from Figma into USWDS theme variables.
 * Auto-runs during build via 'prebuild' script - generates only the needed state.
 *
 * Usage:
 *   node tokens-to-scss.js           # Defaults to DC
 *   node tokens-to-scss.js ca        # California state
 *   STATE=tx node tokens-to-scss.js  # Environment variable
 *
 * Features:
 * - Smart caching: Only regenerates if source JSON changed
 * - USWDS compatible: Generates proper theme variable format
 * - Build integration: Auto-runs during pnpm build
 *
 * Workflow:
 * 1. Read design/states/{state}.json (source of truth)
 * 2. Check if transformation needed (timestamp-based)
 * 3. Extract 'theme' object (USWDS has 'system' tokens built-in)
 * 4. Convert to SCSS variables with USWDS naming conventions
 * 5. Output to sass/_uswds-theme-{state}.scss (gitignored)
 */

import { readFileSync, writeFileSync, mkdirSync, existsSync, statSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const projectRoot = join(__dirname, '..');

// Configuration
const USWDS_PREFIXES = new Set([
  'color', 'font', 'link', 'focus', 'button',
  'global', 'style', 'text'
]);

const CUSTOM_TYPEFACES = {
  urbanist: {
    displayName: 'Urbanist',
    capHeight: 364
  }
};

// Get state file paths
function getStatePaths(state) {
  const statesDir = join(projectRoot, 'design/states');
  const sassDir = join(projectRoot, 'sass');

  return {
    input: join(statesDir, `${state}.json`),
    output: join(sassDir, `_uswds-theme-${state}.scss`),
    sassDir
  };
}

// Check if token should get -color- infix
function shouldInjectColorPrefix(tokenName, tokenType) {
  if (!tokenName.startsWith('theme-') || tokenType !== 'color') {
    return false;
  }

  // Check if any known prefix is already present
  for (const prefix of USWDS_PREFIXES) {
    if (tokenName.includes(`-${prefix}-`)) {
      return false;
    }
  }

  return true;
}

// Normalize font value (lowercase for custom typefaces)
function normalizeFontValue(value, varName) {
  if (!varName.includes('font-type') && !varName.includes('font-role')) {
    return value;
  }

  // Auto-lowercase font names that are in CUSTOM_TYPEFACES
  for (const typeface of Object.keys(CUSTOM_TYPEFACES)) {
    const capitalized = `'${typeface.charAt(0).toUpperCase() + typeface.slice(1)}'`;
    if (value === capitalized) {
      return `'${typeface}'`;
    }
  }

  return value;
}

// Generate typeface tokens SCSS
function generateTypefaceTokens() {
  if (Object.keys(CUSTOM_TYPEFACES).length === 0) {
    return '';
  }

  const tokens = Object.entries(CUSTOM_TYPEFACES)
    .map(([key, { displayName, capHeight }]) =>
      `  ${key}: (\n    display-name: "${displayName}",\n    cap-height: ${capHeight}px\n  )`
    )
    .join(',\n');

  return `\n// Custom typeface tokens for fonts not included in USWDS by default\n$theme-typeface-tokens: (\n${tokens}\n);\n`;
}

/**
 * Convert a token name to SCSS variable format
 * Example: "theme-primary-vivid" → "$theme-color-primary-vivid"
 */
function toScssVariableName(name) {
  return name.startsWith('$') ? name : `$${name}`;
}

/**
 * Convert a token value to SCSS format
 * - Token references like {mint-cool-60v} → 'mint-cool-60v' (for USWDS)
 * - Strings → quoted
 * - Booleans/numbers → as-is
 */
function toScssValue(value) {
  if (typeof value === 'string') {
    // Handle token references like {mint-cool-60v}
    if (value.startsWith('{') && value.endsWith('}')) {
      const tokenName = value.slice(1, -1);
      return `'${tokenName}'`;
    }
    const cleaned = value.replace(/'/g, '');
    return `'${cleaned}'`;
  }
  return value;
}

/**
 * Process theme object recursively and collect SCSS variables
 */
function processThemeObject(obj, prefix = '') {
  const variables = [];

  for (const [key, value] of Object.entries(obj)) {
    if (value && typeof value === 'object' && '$value' in value) {
      // This is a token with a value
      let tokenName = prefix ? `${prefix}-${key}` : key;

      // USWDS expects color tokens to have "-color-" infix after "theme"
      if (shouldInjectColorPrefix(tokenName, value.$type)) {
        tokenName = tokenName.replace('theme-', 'theme-color-');
      }

      const scssName = toScssVariableName(tokenName);
      const scssValue = toScssValue(value.$value);

      variables.push({
        name: scssName,
        value: scssValue,
        description: value.$description || ''
      });
    } else if (value && typeof value === 'object' && !('$value' in value)) {
      // This is a nested object - recurse
      const nestedPrefix = prefix ? `${prefix}-${key}` : key;
      variables.push(...processThemeObject(value, nestedPrefix));
    }
  }

  return variables;
}

// Check if transformation is needed
function needsRegeneration(inputPath, outputPath) {
  if (!existsSync(outputPath)) {
    return true; // Output doesn't exist
  }

  const inputStats = statSync(inputPath);
  const outputStats = statSync(outputPath);

  // Regenerate if input is newer than output
  return inputStats.mtimeMs > outputStats.mtimeMs;
}

// Format a single SCSS variable
function formatScssVariable({ name, value, description }) {
  value = normalizeFontValue(value, name);
  const comment = description
    ? `  // ${description.split('\n')[0].trim()}`
    : '';
  return `${name}: ${value};${comment}`;
}

// Process a single state
function processState(state) {
  const paths = getStatePaths(state);

  if (!existsSync(paths.input)) {
    throw new Error(`State token file not found: ${paths.input}`);
  }

  if (!needsRegeneration(paths.input, paths.output)) {
    console.log(`⚡ Tokens unchanged for ${state.toUpperCase()}\n   ${paths.input} → ${paths.output}`);
    return { state, cached: true, success: true };
  }

  console.log(`Reading: ${paths.input}`);
  const stateJson = JSON.parse(readFileSync(paths.input, 'utf8'));

  if (!stateJson.theme) {
    throw new Error(`No "theme" object found in ${state}.json`);
  }

  console.log(`✅ Found theme object for ${state.toUpperCase()}`);

  const variables = processThemeObject(stateJson.theme);
  console.log(`✅ Extracted ${variables.length} theme variables`);

  const scssContent = `// _uswds-theme-${state}.scss
// Auto-generated USWDS theme variables from Figma Tokens Studio
// Source: design/states/${state}.json (theme object only)
// DO NOT EDIT DIRECTLY - This file is regenerated from design tokens
//
// Usage: Import this file before importing USWDS to customize the theme
// @import 'uswds-theme-${state}';
// @use 'uswds';
${generateTypefaceTokens()}
` + variables.map(formatScssVariable).join('\n') + '\n';

  mkdirSync(paths.sassDir, { recursive: true });
  writeFileSync(paths.output, scssContent, 'utf8');

  console.log(`✅ Generated ${variables.length} variables for ${state.toUpperCase()}`);
  console.log(`   ${paths.output}\n`);

  return { state, cached: false, success: true, count: variables.length };
}

function main() {
  try {
    const args = process.argv.slice(2);
    const state = (args[0] || process.env.STATE || 'dc').toLowerCase();

    try {
      processState(state);
    } catch (error) {
      console.error(`❌ Error: ${error.message}`);
      process.exit(1);
    }

  } catch (error) {
    console.error('❌ Error:', error.message);
    process.exit(1);
  }
}

main();
