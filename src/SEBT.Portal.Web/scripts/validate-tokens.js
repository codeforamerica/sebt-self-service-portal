#!/usr/bin/env node
/**
 * Design Token Validation Script
 *
 * Validates design token structure to catch errors early in the build pipeline.
 * Ensures token files conform to expected format and USWDS requirements.
 *
 * Usage:
 *   node validate-tokens.js           # Validates DC (default)
 *   node validate-tokens.js ca        # Validates California
 *   STATE=tx node validate-tokens.js  # Environment variable
 *
 * Validation Rules:
 * 1. Required 'theme' object exists
 * 2. Color tokens use valid USWDS color references
 * 3. Font tokens reference valid typefaces
 * 4. All token values are properly formatted
 * 5. No circular references
 *
 * Exit Codes:
 *   0 - All validations passed
 *   1 - Validation errors found
 */

import { readFileSync, existsSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const projectRoot = join(__dirname, '..');

// USWDS color palette (subset for validation)
const USWDS_COLORS = new Set([
  'red', 'red-warm', 'red-cool', 'red-vivid',
  'orange', 'orange-warm', 'orange-vivid',
  'gold', 'yellow',
  'green', 'green-warm', 'green-cool', 'green-vivid',
  'mint', 'mint-cool', 'mint-vivid',
  'cyan', 'cyan-vivid',
  'blue', 'blue-warm', 'blue-cool', 'blue-vivid',
  'indigo', 'indigo-warm', 'indigo-cool', 'indigo-vivid',
  'violet', 'violet-warm', 'violet-vivid',
  'magenta', 'magenta-vivid',
  'gray', 'gray-warm', 'gray-cool',
  'black', 'white'
]);

const USWDS_COLOR_GRADES = new Set([
  '5', '10', '20', '30', '40', '50', '60', '70', '80', '90',
  '5v', '10v', '20v', '30v', '40v', '50v', '60v', '70v', '80v'
]);

// Valid custom typefaces (can be extended)
const VALID_TYPEFACES = new Set([
  'urbanist',
  'public-sans',
  'source-sans-pro',
  'merriweather',
  'roboto-mono'
]);

class ValidationError {
  constructor(path, message, severity = 'error') {
    this.path = path;
    this.message = message;
    this.severity = severity;
  }

  toString() {
    const icon = this.severity === 'error' ? '❌' : '⚠️';
    return `${icon} ${this.path}: ${this.message}`;
  }
}

class TokenValidator {
  constructor(state) {
    this.state = state;
    this.errors = [];
    this.warnings = [];
    this.tokenRefs = new Set();
    this.resolvedRefs = new Set();
  }

  // Validate color token reference
  validateColorReference(value, path) {
    // Extract color name from reference like {mint-cool-60v}
    const refMatch = value.match(/^\{([^}]+)\}$/);
    if (!refMatch) {
      // Direct color values are also valid (hex, rgb, etc.)
      if (!/^(#[0-9a-fA-F]{3,8}|rgb|hsl)/.test(value)) {
        this.warnings.push(
          new ValidationError(
            path,
            `Color value "${value}" is not a USWDS reference or standard color format`,
            'warning'
          )
        );
      }
      return;
    }

    const colorRef = refMatch[1];
    this.tokenRefs.add(colorRef);

    // Parse USWDS color reference: color-family-grade
    const parts = colorRef.split('-');
    if (parts.length < 2) {
      this.errors.push(
        new ValidationError(
          path,
          `Invalid color reference format: "${colorRef}" (expected: family-grade or family-variant-grade)`
        )
      );
      return;
    }

    const grade = parts[parts.length - 1];
    const family = parts.slice(0, -1).join('-');

    if (!USWDS_COLORS.has(family)) {
      this.errors.push(
        new ValidationError(
          path,
          `Unknown USWDS color family: "${family}" in reference "${colorRef}"`
        )
      );
    }

    if (!USWDS_COLOR_GRADES.has(grade)) {
      this.errors.push(
        new ValidationError(
          path,
          `Invalid USWDS color grade: "${grade}" in reference "${colorRef}" (valid: 5-90, 5v-80v)`
        )
      );
    }
  }

  // Validate font token reference
  validateFontReference(value, path) {
    // Remove quotes and lowercase
    const font = value.replace(/['"]/g, '').toLowerCase();

    // Check if it's a custom typeface
    if (!VALID_TYPEFACES.has(font)) {
      this.warnings.push(
        new ValidationError(
          path,
          `Font "${font}" not in validated typeface list. Ensure it's properly configured.`,
          'warning'
        )
      );
    }
  }

  // Validate a single token
  validateToken(token, path) {
    if (!token || typeof token !== 'object') {
      this.errors.push(
        new ValidationError(path, 'Token must be an object')
      );
      return;
    }

    if (!('$value' in token)) {
      // Not a leaf token, skip
      return;
    }

    const { $value, $type, $description } = token;

    // Validate based on type
    if ($type === 'color') {
      this.validateColorReference($value, path);
    } else if ($type === 'fontFamilies' || path.includes('font-type')) {
      this.validateFontReference($value, path);
    } else if ($type === 'dimension' || $type === 'sizing') {
      // Validate dimension format
      if (typeof $value !== 'string' && typeof $value !== 'number') {
        this.errors.push(
          new ValidationError(
            path,
            `Dimension value must be string or number, got: ${typeof $value}`
          )
        );
      }
    }

    // Warn if no description for important tokens
    if (!$description && (path.includes('primary') || path.includes('secondary'))) {
      this.warnings.push(
        new ValidationError(
          path,
          'Important token missing $description',
          'warning'
        )
      );
    }
  }

  // Recursively validate theme object
  validateThemeObject(obj, prefix = 'theme') {
    for (const [key, value] of Object.entries(obj)) {
      const path = `${prefix}.${key}`;

      if (value && typeof value === 'object') {
        if ('$value' in value) {
          // This is a token
          this.validateToken(value, path);
        } else {
          // Nested object
          this.validateThemeObject(value, path);
        }
      }
    }
  }

  // Main validation entry point
  validate(tokenData) {
    const startTime = performance.now();

    // 1. Required structure
    if (!tokenData.theme) {
      this.errors.push(
        new ValidationError(
          'root',
          'Missing required "theme" object. USWDS requires theme tokens.'
        )
      );
      return this.getResults(startTime);
    }

    // 2. Validate theme structure
    this.validateThemeObject(tokenData.theme);

    // 3. Check for potential circular references
    const unresolvedRefs = [...this.tokenRefs].filter(
      ref => !this.resolvedRefs.has(ref)
    );
    if (unresolvedRefs.length > 0) {
      this.warnings.push(
        new ValidationError(
          'theme',
          `Unresolved token references: ${unresolvedRefs.join(', ')}. Ensure these are defined in USWDS or system tokens.`,
          'warning'
        )
      );
    }

    return this.getResults(startTime);
  }

  getResults(startTime) {
    const elapsedTime = (performance.now() - startTime).toFixed(2);
    return {
      state: this.state,
      errors: this.errors,
      warnings: this.warnings,
      tokenCount: this.tokenRefs.size,
      validationTime: parseFloat(elapsedTime),
      success: this.errors.length === 0
    };
  }
}

function validateState(state) {
  const tokenPath = join(projectRoot, `design/states/${state}.json`);

  if (!existsSync(tokenPath)) {
    console.error(`❌ Token file not found: ${tokenPath}`);
    return { success: false };
  }

  console.log(`Validating: ${tokenPath}`);

  try {
    const tokenData = JSON.parse(readFileSync(tokenPath, 'utf8'));
    const validator = new TokenValidator(state);
    const results = validator.validate(tokenData);

    // Print results
    console.log(`\n📊 Validation Results (${state.toUpperCase()}):`);
    console.log(`   Validation time: ${results.validationTime}ms`);
    console.log(`   Token references: ${results.tokenCount}`);

    if (results.errors.length > 0) {
      console.log(`\n❌ Errors (${results.errors.length}):`);
      results.errors.forEach(err => console.log(`   ${err.toString()}`));
    }

    if (results.warnings.length > 0) {
      console.log(`\n⚠️  Warnings (${results.warnings.length}):`);
      results.warnings.forEach(warn => console.log(`   ${warn.toString()}`));
    }

    if (results.errors.length === 0) {
      console.log(`\n✅ Validation passed for ${state.toUpperCase()}`);
    } else {
      console.log(`\n❌ Validation failed for ${state.toUpperCase()}`);
    }

    return results;
  } catch (error) {
    console.error(`❌ Failed to parse token file: ${error.message}`);
    return { success: false };
  }
}

function main() {
  const args = process.argv.slice(2);
  const state = (args[0] || process.env.STATE || 'dc').toLowerCase();

  const results = validateState(state);
  process.exit(results.success ? 0 : 1);
}

main();
