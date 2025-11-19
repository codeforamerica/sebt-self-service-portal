#!/usr/bin/env node
/**
 * Convert Figma Tokens Studio JSON to USWDS SCSS Variables
 *
 * This script reads dc.json from Tokens Studio and extracts ONLY the 'theme' object,
 * converting it to USWDS-compatible SCSS variables.
 *
 * Features:
 * - Smart caching: Only regenerates if dc.json changed
 * - Fast execution: Skips transformation if output is up-to-date
 * - USWDS compatible: Generates proper theme variable format
 *
 * Workflow:
 * 1. Read design/states/dc.json
 * 2. Check if transformation needed (file changed)
 * 3. Extract 'theme' object (ignore 'system' object - USWDS has those built-in)
 * 4. Convert to SCSS variables with USWDS naming conventions
 * 5. Keep token references as-is for USWDS to resolve
 */

import { readFileSync, writeFileSync, mkdirSync, existsSync, statSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const projectRoot = join(__dirname, '..');

/**
 * Convert a token name to SCSS variable format
 * Example: "theme-primary-vivid" → "$theme-color-primary-vivid"
 */
function toScssVariableName(name) {
  // Token names should already be in the correct format
  // Just ensure they start with $ if they don't
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
      // Extract token name and quote it for USWDS
      const tokenName = value.slice(1, -1);
      return `'${tokenName}'`;
    }
    // Remove existing quotes and re-quote for consistency
    const cleaned = value.replace(/'/g, '');
    return `'${cleaned}'`;
  }
  // Booleans and numbers stay as-is
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
      // Convert: theme-primary-lightest → theme-color-primary-lightest
      // But keep: theme-font-type-sans, theme-link-color as-is
      if (tokenName.startsWith('theme-') &&
          !tokenName.includes('-color-') &&
          !tokenName.includes('-font-') &&
          !tokenName.includes('-link-') &&
          !tokenName.includes('-focus-') &&
          !tokenName.includes('-button-') &&
          !tokenName.includes('-global-') &&
          !tokenName.includes('-style-') &&
          !tokenName.includes('-text-') &&
          value.$type === 'color') {
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

/**
 * Check if transformation is needed
 */
function needsRegeneration(inputPath, outputPath) {
  if (!existsSync(outputPath)) {
    return true; // Output doesn't exist
  }

  const inputStats = statSync(inputPath);
  const outputStats = statSync(outputPath);

  // Regenerate if input is newer than output
  return inputStats.mtimeMs > outputStats.mtimeMs;
}

/**
 * Main function
 */
function main() {
  try {
    // Read dc.json
    const dcJsonPath = join(projectRoot, 'design/states/dc.json');
    const outputPath = join(projectRoot, 'sass/_uswds-theme-dc.scss');

    // Check if regeneration needed
    if (!needsRegeneration(dcJsonPath, outputPath)) {
      console.log('⚡ Tokens unchanged, using cached SCSS');
      console.log(`   Source: ${dcJsonPath}`);
      console.log(`   Output: ${outputPath}`);
      return;
    }

    console.log(`Reading: ${dcJsonPath}`);
    const dcJson = JSON.parse(readFileSync(dcJsonPath, 'utf8'));

    // Extract theme object
    if (!dcJson.theme) {
      console.error('❌ Error: No "theme" object found in dc.json');
      process.exit(1);
    }

    console.log('✅ Found theme object');

    // Process theme object to SCSS variables
    const variables = processThemeObject(dcJson.theme);
    console.log(`✅ Extracted ${variables.length} theme variables`);

    // Generate SCSS content
    const header = `// _uswds-theme-dc.scss
// Auto-generated USWDS theme variables from Figma Tokens Studio
// Source: design/states/dc.json (theme object only)
// DO NOT EDIT DIRECTLY - This file is regenerated from design tokens
//
// Usage: Import this file before importing USWDS to customize the theme
// @import 'uswds-theme-dc';
// @use 'uswds';

// Custom typeface tokens for fonts not included in USWDS by default
$theme-typeface-tokens: (
  urbanist: (
    display-name: "Urbanist",
    cap-height: 364px
  )
);

`;

    const scssVariables = variables
      .map(v => {
        // Clean up description - only use first line, remove newlines
        const cleanDesc = v.description
          ? v.description.split('\n')[0].trim()
          : '';
        const comment = cleanDesc ? `  // ${cleanDesc}` : '';

        // Convert font values to lowercase to match typeface token names
        let value = v.value;
        if ((v.name.includes('font-type') || v.name.includes('font-role')) && value === "'Urbanist'") {
          value = "'urbanist'";
        }

        return `${v.name}: ${value};${comment}`;
      })
      .join('\n');

    const scssContent = header + scssVariables + '\n';

    // Write SCSS file to sass directory (used by USWDS compile)
    const outputDir = join(projectRoot, 'sass');
    mkdirSync(outputDir, { recursive: true });

    writeFileSync(outputPath, scssContent, 'utf8');

    console.log(`✅ Generated: ${outputPath}`);
    console.log(`\n📊 Summary:`);
    console.log(`   - Theme variables: ${variables.length}`);
    console.log(`   - Output file: sass/_uswds-theme-dc.scss`);
    console.log(`\n✅ Done!`);

  } catch (error) {
    console.error('❌ Error:', error.message);
    process.exit(1);
  }
}

main();
