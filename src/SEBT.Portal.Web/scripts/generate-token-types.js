#!/usr/bin/env node
/**
 * TypeScript Type Definitions Generator
 *
 * Auto-generates TypeScript types from design tokens for IDE autocomplete and type safety.
 *
 * Usage:
 *   node generate-token-types.js           # Generates types for DC (default)
 *   node generate-token-types.js ca        # Generates types for California
 *   STATE=tx node generate-token-types.js  # Environment variable
 *
 * Output:
 *   src/types/design-tokens.d.ts - TypeScript type definitions
 *
 * Features:
 * - Type-safe access to design tokens in TypeScript
 * - IDE autocomplete for token values
 * - Compile-time validation of token usage
 */

import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const projectRoot = join(__dirname, '..');

/**
 * Convert token name to TypeScript-friendly property name
 * theme-color-primary-vivid → colorPrimaryVivid
 */
function toCamelCase(str) {
  return str
    .replace(/^theme-/, '')
    .replace(/-([a-z])/g, (_, letter) => letter.toUpperCase());
}

/**
 * Determine TypeScript type from token $type
 */
function getTypeScriptType(tokenType) {
  switch (tokenType) {
    case 'color':
      return 'string'; // USWDS color reference or hex
    case 'fontFamilies':
      return 'string';
    case 'dimension':
    case 'sizing':
      return 'string | number';
    case 'fontWeights':
      return 'number | string';
    case 'lineHeights':
      return 'number | string';
    case 'number':
      return 'number';
    case 'boolean':
      return 'boolean';
    default:
      return 'string';
  }
}

/**
 * Process theme object and generate TypeScript interface properties
 */
function processThemeObject(obj, prefix = '') {
  const properties = [];

  for (const [key, value] of Object.entries(obj)) {
    if (value && typeof value === 'object' && '$value' in value) {
      // This is a token with a value
      const tokenName = prefix ? `${prefix}-${key}` : key;
      const propName = toCamelCase(tokenName);
      const tsType = getTypeScriptType(value.$type);
      const description = value.$description || `Token: ${tokenName}`;

      properties.push({
        name: propName,
        type: tsType,
        description,
        originalName: tokenName
      });
    } else if (value && typeof value === 'object' && !('$value' in value)) {
      // Nested object - recurse
      const nestedPrefix = prefix ? `${prefix}-${key}` : key;
      properties.push(...processThemeObject(value, nestedPrefix));
    }
  }

  return properties;
}

/**
 * Generate TypeScript interface from properties
 */
function generateInterface(state, properties) {
  const interfaceName = `${state.toUpperCase()}Theme`;
  const lines = [];

  lines.push('/**');
  lines.push(` * Design tokens for ${state.toUpperCase()} state`);
  lines.push(` * Auto-generated from design/states/${state}.json`);
  lines.push(' * DO NOT EDIT DIRECTLY - Regenerated from design tokens');
  lines.push(' */');
  lines.push(`export interface ${interfaceName} {`);

  // Sort properties alphabetically for better readability
  const sortedProps = [...properties].sort((a, b) => a.name.localeCompare(b.name));

  sortedProps.forEach(({ name, type, description }) => {
    lines.push(`  /** ${description} */`);
    lines.push(`  ${name}: ${type};`);
    lines.push('');
  });

  lines.push('}');

  return lines.join('\n');
}

/**
 * Generate token value mapping (for runtime access)
 */
function generateTokenMap(state, properties) {
  const constName = `${state.toUpperCase()}_TOKENS`;
  const lines = [];

  lines.push('/**');
  lines.push(` * Runtime token map for ${state.toUpperCase()} state`);
  lines.push(` * Maps property names to original SCSS variable names`);
  lines.push(' */');
  lines.push(`export const ${constName}: Record<keyof ${state.toUpperCase()}Theme, string> = {`);

  // Sort properties alphabetically
  const sortedProps = [...properties].sort((a, b) => a.name.localeCompare(b.name));

  sortedProps.forEach(({ name, originalName }, index) => {
    const comma = index < sortedProps.length - 1 ? ',' : '';
    lines.push(`  ${name}: '$${originalName}'${comma}`);
  });

  lines.push('};');

  return lines.join('\n');
}

/**
 * Main generation function
 */
function generateTypes(state) {
  const startTime = performance.now();
  const tokenPath = join(projectRoot, `design/states/${state}.json`);
  const outputDir = join(projectRoot, 'src/types');
  const outputPath = join(outputDir, 'design-tokens.d.ts');

  if (!existsSync(tokenPath)) {
    throw new Error(`Token file not found: ${tokenPath}`);
  }

  console.log(`Reading tokens: ${tokenPath}`);
  const tokenData = JSON.parse(readFileSync(tokenPath, 'utf8'));

  if (!tokenData.theme) {
    throw new Error(`No "theme" object found in ${state}.json`);
  }

  console.log(`✅ Processing theme tokens for ${state.toUpperCase()}`);

  // Extract properties
  const properties = processThemeObject(tokenData.theme);
  console.log(`✅ Extracted ${properties.length} token properties`);

  // Generate TypeScript content
  const content = `// design-tokens.d.ts
// Auto-generated TypeScript definitions for design tokens
// Source: design/states/${state}.json
// DO NOT EDIT DIRECTLY - Regenerated from design tokens on build

${generateInterface(state, properties)}

${generateTokenMap(state, properties)}

/**
 * Example usage:
 *
 * import type { ${state.toUpperCase()}Theme } from './types/design-tokens';
 * import { ${state.toUpperCase()}_TOKENS } from './types/design-tokens';
 *
 * // Type-safe access
 * const primaryColor: ${state.toUpperCase()}Theme['colorPrimaryVivid'] = '#0f6460';
 *
 * // Runtime SCSS variable name
 * const scssVar = ${state.toUpperCase()}_TOKENS.colorPrimaryVivid; // '$theme-color-primary-vivid'
 */
`;

  // Write to file
  mkdirSync(outputDir, { recursive: true });
  writeFileSync(outputPath, content, 'utf8');

  const elapsedTime = (performance.now() - startTime).toFixed(2);

  console.log(`✅ Generated TypeScript definitions`);
  console.log(`   ${outputPath}`);
  console.log(`\n📊 Type Generation Metrics:`);
  console.log(`   Generation time: ${elapsedTime}ms`);
  console.log(`   Properties: ${properties.length}`);
  console.log(`   Interface: ${state.toUpperCase()}Theme`);
  console.log(`   Token map: ${state.toUpperCase()}_TOKENS\n`);

  return { success: true, properties: properties.length, time: parseFloat(elapsedTime) };
}

function main() {
  try {
    const args = process.argv.slice(2);
    const state = (args[0] || process.env.STATE || 'dc').toLowerCase();

    generateTypes(state);
  } catch (error) {
    console.error(`❌ Error: ${error.message}`);
    process.exit(1);
  }
}

main();
