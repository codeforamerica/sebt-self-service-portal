#!/usr/bin/env node
/**
 * Generate i18n Locale Files from CSV
 *
 * Transforms Google Sheet CSV export into namespaced JSON files for react-i18next.
 * Supports multi-state deployments with state-specific copy overrides.
 *
 * Usage:
 *   node content/scripts/generate-locales.js           # Generate all locales
 *   node content/scripts/generate-locales.js --watch   # Watch mode (future)
 *
 * CSV File:
 *   content/[WORKING] DC CO Enrollment Checker & Self-Service Portal Content - 🟡 DC SUN Bucks [WORKING].csv
 *   (Downloaded from Google Sheets, committed to repository)
 *
 * CSV Format:
 *   🟡 Content,English,Español,
 *   "S1 - Landing Page - Title","Get a one-time...","Obtenga un pago...",
 *
 * Content Key Format (supports two formats):
 *   3-part: "{Section} - {Page} - {Key}"
 *     Example: "S1 - Landing Page - Title" -> landing.title
 *   2-part: "{Section} - {Key}"
 *     Example: "GLOBAL - Address Format" -> common.addressFormat
 *
 *   - Section: GLOBAL (shared), S1-S8 (screens), PROTO (prototypes)
 *   - Page: Landing Page, Disclaimer, Personal Information, etc.
 *   - Key: Descriptive key name (spaces converted to camelCase)
 *
 * Output Structure:
 *   content/locales/en/dc/common.json
 *   content/locales/en/dc/landing.json
 *   content/locales/es/dc/common.json
 *   ...
 *
 * Features:
 * - Smart caching: Only regenerates if CSV changed (SHA-256 hash)
 * - State-specific overrides: "all" state entries as base, specific state overrides
 * - Namespace splitting: Organizes by page/component for lazy loading
 * - Variable interpolation: Preserves {state}, {year} placeholders for runtime
 */

import { createHash } from 'crypto';
import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const contentDir = join(__dirname, '..');

// Configuration
const CONFIG = {
  // CSV file from Google Sheets
  csvPath: join(contentDir, '[WORKING] DC CO Enrollment Checker & Self-Service Portal Content - 🟡 DC SUN Bucks [WORKING].csv'),
  outputDir: join(contentDir, 'locales'),
  hashFile: join(contentDir, '.copy-hash'),
  locales: {
    en: 'English',
    es: 'Español',
  },
  states: ['dc', 'co'],
  // Map CSV sections to namespaces (fallback when page not mapped)
  sectionToNamespace: {
    All: 'common', // Shared across all pages
    GLOBAL: 'common', // Global shared content
    S1: 'landing', // Landing page (Screen 1)
    S2: 'disclaimer', // Disclaimer (Screen 2)
    S3: 'personalInfo', // Personal Information (Screen 3)
    S4: 'confirmInfo', // Confirm Personal Information (Screen 4)
    S5: 'optionalId', // Optional ID Information (Screen 5)
    S6: 'result', // Result page (Screen 6)
    S7: 'dashboard', // Portal Dashboard (Screen 7)
    S8: 'edit', // Edit screens (Screen 8)
    PROTO: 'proto', // Prototype/demo content
  },
  // Map page names to namespaces (for "S1 - Landing Page - Title" format)
  pageToNamespace: {
    // S1 - Landing
    'landing page': 'landing',
    // S2 - Disclaimer
    'disclaimer': 'disclaimer',
    // S3 - Personal Information
    'personal information': 'personalInfo',
    // S4 - Confirm Personal Information
    'confirm personal information': 'confirmInfo',
    // S5 - Optional ID Information
    'optional id information': 'optionalId',
    // S6 - Result
    'result': 'result',
    // S7 - Portal Dashboard
    'portal dashboard': 'dashboard',
    // S8 - Edit screens
    'contact preferences update': 'editContactPreferences',
    'mailing address edit': 'editMailingAddress',
    // Global/Common
    'header': 'common',
    'footer': 'common',
    'help': 'common',
    'global': 'common',
    // PROTO - Prototype screens
    'prototypes': 'proto',
  },
};

/**
 * Parse CSV content into rows
 * Handles quoted fields with commas and newlines
 */
function parseCSV(content) {
  const rows = [];
  let currentRow = [];
  let currentField = '';
  let inQuotes = false;

  for (let i = 0; i < content.length; i++) {
    const char = content[i];
    const nextChar = content[i + 1];

    if (inQuotes) {
      if (char === '"' && nextChar === '"') {
        // Escaped quote
        currentField += '"';
        i++;
      } else if (char === '"') {
        // End of quoted field
        inQuotes = false;
      } else {
        currentField += char;
      }
    } else {
      if (char === '"') {
        // Start of quoted field
        inQuotes = true;
      } else if (char === ',') {
        // Field separator
        currentRow.push(currentField.trim());
        currentField = '';
      } else if (char === '\n' || (char === '\r' && nextChar === '\n')) {
        // Row separator
        currentRow.push(currentField.trim());
        if (currentRow.some((f) => f)) {
          // Skip empty rows
          rows.push(currentRow);
        }
        currentRow = [];
        currentField = '';
        if (char === '\r') i++; // Skip \n in \r\n
      } else if (char !== '\r') {
        currentField += char;
      }
    }
  }

  // Handle last field/row
  if (currentField || currentRow.length > 0) {
    currentRow.push(currentField.trim());
    if (currentRow.some((f) => f)) {
      rows.push(currentRow);
    }
  }

  return rows;
}

/**
 * Convert space-separated key to camelCase
 * "Logo Alt" -> "logoAlt"
 */
function toCamelCase(str) {
  return str
    .split(/\s+/)
    .map((word, index) =>
      index === 0
        ? word.toLowerCase()
        : word.charAt(0).toUpperCase() + word.slice(1).toLowerCase()
    )
    .join('');
}

/**
 * Parse content key into structured parts
 * Supports both formats:
 *   3-part: "S1 - Landing Page - Title" -> { namespace: "landing", key: "title" }
 *   2-part: "GLOBAL - Address Format" -> { namespace: "common", key: "addressFormat" }
 */
function parseContentKey(contentKey) {
  const parts = contentKey.split(' - ').map((p) => p.trim());

  if (parts.length < 2) {
    console.warn(`⚠️  Invalid content key format: "${contentKey}"`);
    return null;
  }

  // Handle 2-part format: "SECTION - Key Name"
  if (parts.length === 2) {
    const [section, keyName] = parts;
    const key = toCamelCase(keyName);

    // Determine namespace from section
    const namespace = CONFIG.sectionToNamespace[section] || toCamelCase(section);

    return {
      section,
      page: section.toLowerCase(),
      namespace,
      key,
    };
  }

  // Handle 3+ part format: "SECTION - Page - Key Name"
  const [section, page, ...keyParts] = parts;
  const key = toCamelCase(keyParts.join(' '));
  const pageLower = page.toLowerCase();

  // Determine namespace from page name or section
  let namespace;
  if (CONFIG.pageToNamespace[pageLower]) {
    namespace = CONFIG.pageToNamespace[pageLower];
  } else if (CONFIG.sectionToNamespace[section]) {
    namespace = CONFIG.sectionToNamespace[section];
  } else {
    // Fallback: use sanitized page name
    namespace = toCamelCase(page);
  }

  return {
    section,
    page: pageLower,
    namespace,
    key,
  };
}

/**
 * Build locale data structure from CSV rows
 * Returns: { en: { dc: { common: { key: value }, landing: { key: value } } } }
 */
function buildLocaleData(rows) {
  const [headerRow, ...dataRows] = rows;

  // Find column indices (handle emoji prefixes like "🟡 Content")
  const contentIdx = headerRow.findIndex((h) =>
    h.toLowerCase().includes('content')
  );
  const englishIdx = headerRow.findIndex((h) =>
    h.toLowerCase().includes('english')
  );
  const spanishIdx = headerRow.findIndex((h) =>
    h.toLowerCase().includes('español')
  );
  const stateIdx = headerRow.findIndex((h) => h.toLowerCase() === 'state');

  if (contentIdx === -1 || englishIdx === -1) {
    throw new Error('CSV must have "Content" and "English" columns');
  }

  // Log detected format
  const hasStateColumn = stateIdx !== -1;
  console.log(
    `   Format: ${hasStateColumn ? 'Multi-state (with State column)' : 'Single-state (no State column)'}`
  );

  // Initialize data structure
  const data = {};
  for (const locale of Object.keys(CONFIG.locales)) {
    data[locale] = {};
    for (const state of CONFIG.states) {
      data[locale][state] = {};
    }
  }

  // Process each row
  for (const row of dataRows) {
    const contentKey = row[contentIdx];
    const englishValue = row[englishIdx] || '';
    const spanishValue = spanishIdx !== -1 ? row[spanishIdx] || '' : '';

    // Skip empty rows or rows without content keys
    if (!contentKey || !contentKey.trim()) continue;

    // Determine state: from column, or 'all' if no State column
    const stateValue = hasStateColumn
      ? row[stateIdx]?.toLowerCase() || 'all'
      : 'all';

    const parsed = parseContentKey(contentKey);
    if (!parsed) continue;

    const { namespace, key } = parsed;

    // Determine which states this entry applies to
    const targetStates =
      stateValue === 'all' ? CONFIG.states : [stateValue];

    // Add to each target state/locale
    for (const state of targetStates) {
      if (!CONFIG.states.includes(state)) continue;

      // English
      if (!data.en[state][namespace]) {
        data.en[state][namespace] = {};
      }
      // Only set if not already set by state-specific entry
      if (
        stateValue === 'all' &&
        data.en[state][namespace][key] !== undefined
      ) {
        continue; // State-specific already set, skip "all"
      }
      data.en[state][namespace][key] = englishValue;

      // Spanish
      if (spanishIdx !== -1 && spanishValue) {
        if (!data.es[state][namespace]) {
          data.es[state][namespace] = {};
        }
        if (
          stateValue === 'all' &&
          data.es[state][namespace][key] !== undefined
        ) {
          continue;
        }
        data.es[state][namespace][key] = spanishValue;
      }
    }
  }

  return data;
}

/**
 * Calculate SHA-256 hash of file contents
 */
function calculateHash(filePath) {
  if (!existsSync(filePath)) return null;
  const content = readFileSync(filePath, 'utf8');
  return createHash('sha256').update(content).digest('hex');
}

/**
 * Check if regeneration is needed based on hash
 */
function needsRegeneration() {
  const currentHash = calculateHash(CONFIG.csvPath);
  if (!currentHash) return true;

  if (!existsSync(CONFIG.hashFile)) return true;

  const storedHash = readFileSync(CONFIG.hashFile, 'utf8').trim();
  return currentHash !== storedHash;
}

/**
 * Save current hash for cache invalidation
 */
function saveHash() {
  const hash = calculateHash(CONFIG.csvPath);
  if (hash) {
    writeFileSync(CONFIG.hashFile, hash, 'utf8');
  }
}

/**
 * Clean output directory (preserves common.json which is manually maintained)
 */
function cleanOutputDir() {
  if (!existsSync(CONFIG.outputDir)) return;

  // Collect common.json files to preserve
  const commonFiles = [];
  for (const locale of Object.keys(CONFIG.locales)) {
    for (const state of CONFIG.states) {
      const commonPath = join(CONFIG.outputDir, locale, state, 'common.json');
      if (existsSync(commonPath)) {
        commonFiles.push({
          path: commonPath,
          content: readFileSync(commonPath, 'utf8'),
        });
      }
    }
  }

  // Clean directory
  rmSync(CONFIG.outputDir, { recursive: true });

  // Restore common.json files
  for (const file of commonFiles) {
    const dir = dirname(file.path);
    mkdirSync(dir, { recursive: true });
    writeFileSync(file.path, file.content, 'utf8');
  }
}

/**
 * Write locale files to disk
 */
function writeLocaleFiles(data) {
  let fileCount = 0;

  for (const [locale, states] of Object.entries(data)) {
    for (const [state, namespaces] of Object.entries(states)) {
      for (const [namespace, translations] of Object.entries(namespaces)) {
        if (Object.keys(translations).length === 0) continue;

        const dir = join(CONFIG.outputDir, locale, state);
        const filePath = join(dir, `${namespace}.json`);

        mkdirSync(dir, { recursive: true });
        writeFileSync(
          filePath,
          JSON.stringify(translations, null, 2) + '\n',
          'utf8'
        );
        fileCount++;
      }
    }
  }

  return fileCount;
}

/**
 * Validate that all keys exist across all locales/states
 */
function validateCompleteness(data) {
  const warnings = [];
  const baseLocale = 'en';
  const baseState = CONFIG.states[0];

  // Collect all keys from base locale/state
  const baseKeys = new Map();
  for (const [namespace, translations] of Object.entries(
    data[baseLocale][baseState]
  )) {
    for (const key of Object.keys(translations)) {
      baseKeys.set(`${namespace}.${key}`, true);
    }
  }

  // Check other locales/states for missing keys
  for (const [locale, states] of Object.entries(data)) {
    for (const [state, namespaces] of Object.entries(states)) {
      for (const [namespace, translations] of Object.entries(namespaces)) {
        for (const key of Object.keys(translations)) {
          const fullKey = `${namespace}.${key}`;
          if (!baseKeys.has(fullKey)) {
            warnings.push(
              `Extra key in ${locale}/${state}: ${fullKey}`
            );
          }
        }
      }

      // Check for missing keys
      for (const [fullKey] of baseKeys) {
        const [namespace, key] = fullKey.split('.');
        if (!data[locale][state][namespace]?.[key]) {
          warnings.push(
            `Missing key in ${locale}/${state}: ${fullKey}`
          );
        }
      }
    }
  }

  return warnings;
}

/**
 * Main entry point
 */
function main() {
  console.log('🌐 Generating i18n locale files...\n');

  // Check if CSV exists
  if (!existsSync(CONFIG.csvPath)) {
    console.log('⚠️  No CSV file found at content/copy.csv');
    console.log('   Download from Google Sheets when ready.\n');
    process.exit(0);
  }

  // Check if regeneration needed
  if (!needsRegeneration()) {
    console.log('⚡ Locales unchanged (cached)');
    console.log(`   ${CONFIG.csvPath}\n`);

    // List existing locale files
    if (existsSync(CONFIG.outputDir)) {
      const locales = readdirSync(CONFIG.outputDir);
      console.log(`   Existing: ${locales.join(', ')}\n`);
    }
    process.exit(0);
  }

  try {
    // Read and parse CSV
    console.log(`📖 Reading: ${CONFIG.csvPath}`);
    const csvContent = readFileSync(CONFIG.csvPath, 'utf8');
    const rows = parseCSV(csvContent);
    console.log(`   Found ${rows.length - 1} content entries\n`);

    // Build locale data
    const data = buildLocaleData(rows);

    // Validate completeness
    const warnings = validateCompleteness(data);
    if (warnings.length > 0) {
      console.log('⚠️  Validation warnings:');
      warnings.forEach((w) => console.log(`   - ${w}`));
      console.log();
    }

    // Clean and write
    cleanOutputDir();
    const fileCount = writeLocaleFiles(data);

    // Save hash for caching
    saveHash();

    // Summary
    console.log(`✅ Generated ${fileCount} locale files:`);
    for (const locale of Object.keys(CONFIG.locales)) {
      for (const state of CONFIG.states) {
        const dir = join(CONFIG.outputDir, locale, state);
        if (existsSync(dir)) {
          const files = readdirSync(dir);
          console.log(`   ${locale}/${state}/: ${files.join(', ')}`);
        }
      }
    }
    console.log();

    process.exit(0);
  } catch (error) {
    console.error('❌ Failed to generate locales:');
    console.error(`   ${error.message}\n`);
    process.exit(1);
  }
}

main();
