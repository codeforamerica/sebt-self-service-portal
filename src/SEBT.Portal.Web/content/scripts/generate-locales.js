#!/usr/bin/env node
/**
 * Generate i18n Locale Files from CSV
 *
 * Transforms Google Sheet CSV exports into namespaced JSON files for react-i18next.
 * Supports multi-state deployments with separate CSV files per state.
 *
 * Usage:
 *   node content/scripts/generate-locales.js           # Generate all locales
 *   node content/scripts/generate-locales.js --watch   # Watch mode (future)
 *
 * CSV Files:
 *   content/states/dc.csv  # DC-specific content (downloaded from DC tab)
 *   content/states/co.csv  # CO-specific content (downloaded from CO tab)
 *   content/states/ny.csv  # NY-specific content (future)
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
 * - Multi-state CSVs: Each state has its own CSV file (content/states/{state}.csv)
 * - Smart caching: Only regenerates if any CSV changed (SHA-256 hash)
 * - Namespace splitting: Organizes by page/component for lazy loading
 * - Variable interpolation: Preserves {state}, {year} placeholders for runtime
 */

import '../../design/scripts/load-env.js';
import { createHash } from 'crypto';
import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from 'fs';
import { dirname, join, relative } from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const contentDir = join(__dirname, '..');
const rootDir = join(contentDir, '..');
const rel = p => relative(rootDir, p);

// Configuration
const CONFIG = {
  // State CSV files directory (content/states/{state}.csv)
  statesDir: join(contentDir, 'states'),
  outputDir: join(contentDir, 'locales'),
  hashFile: join(contentDir, '.copy-hash'),
  locales: {
    en: 'English',
    es: 'Español',
  },
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
    // S8 - Login/OTP flow
    'otp enter email': 'login',
    'otp confirm': 'login',
    'otp email message': 'login',
    // S2 - Log In Disclaimer (maps to login namespace)
    'log in disclaimer': 'login',
    // S8 - Identity proofing
    'id proofing optional id info': 'idProofing',
    // S8 - Opt-in preferences
    'opt-in': 'optIn',
    // S8 - Off-boarding screens
    'off-boarding': 'offBoarding',
    'co-loaded off-boarding': 'offBoarding',
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
  // Key prefixes for pages that share a namespace but need distinct keys
  // This prevents key collisions (e.g., both OTP Enter Email and OTP Confirm have "title")
  pageKeyPrefix: {
    'otp confirm': 'verify',
    'otp email message': 'email',
    'co-loaded off-boarding': 'coLoaded',
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
  let key = toCamelCase(keyParts.join(' '));
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

  // Apply key prefix for pages that share a namespace but need distinct keys
  const keyPrefix = CONFIG.pageKeyPrefix[pageLower];
  if (keyPrefix) {
    // Capitalize first letter of key and prepend prefix
    key = keyPrefix + key.charAt(0).toUpperCase() + key.slice(1);
  }

  return {
    section,
    page: pageLower,
    namespace,
    key,
  };
}

/**
 * Build locale data structure from CSV rows for a single state
 * Returns: { en: { common: { key: value }, landing: { key: value } }, es: { ... } }
 */
function buildStateLocaleData(rows, state) {
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

  if (contentIdx === -1 || englishIdx === -1) {
    throw new Error(`CSV for ${state} must have "Content" and "English" columns`);
  }

  // Initialize data structure for this state
  const data = {};
  for (const locale of Object.keys(CONFIG.locales)) {
    data[locale] = {};
  }

  // Process each row
  for (const row of dataRows) {
    const contentKey = row[contentIdx];
    const englishValue = row[englishIdx] || '';
    const spanishValue = spanishIdx !== -1 ? row[spanishIdx] || '' : '';

    // Skip empty rows or rows without content keys
    if (!contentKey || !contentKey.trim()) continue;

    const parsed = parseContentKey(contentKey);
    if (!parsed) continue;

    const { namespace, key } = parsed;

    // English — don't overwrite a non-empty value with an empty one
    // (handles key collisions where multiple CSV rows map to the same namespace+key)
    if (!data.en[namespace]) {
      data.en[namespace] = {};
    }
    if (englishValue || !data.en[namespace][key]) {
      data.en[namespace][key] = englishValue;
    }

    // Spanish — same collision protection
    if (spanishIdx !== -1) {
      if (!data.es[namespace]) {
        data.es[namespace] = {};
      }
      if (spanishValue || !data.es[namespace][key]) {
        data.es[namespace][key] = spanishValue;
      }
    }
  }

  return data;
}

/**
 * Discover state CSV files in the states directory
 * Returns array of { state: string, csvPath: string }
 */
function discoverStateCsvFiles() {
  if (!existsSync(CONFIG.statesDir)) {
    return [];
  }

  const files = readdirSync(CONFIG.statesDir);
  return files
    .filter((f) => f.endsWith('.csv'))
    .map((f) => ({
      state: f.replace('.csv', '').toLowerCase(),
      csvPath: join(CONFIG.statesDir, f),
    }));
}

/**
 * Calculate combined SHA-256 hash of all state CSV files and this script
 */
function calculateCombinedHash(stateFiles) {
  const hash = createHash('sha256');

  // Include the script itself so logic changes invalidate the cache
  hash.update(readFileSync(fileURLToPath(import.meta.url), 'utf8'));

  for (const { state, csvPath } of stateFiles) {
    if (existsSync(csvPath)) {
      const content = readFileSync(csvPath, 'utf8');
      hash.update(`${state}:${content}`);
    }
  }

  return hash.digest('hex');
}

/**
 * Check if expected locale files exist for discovered states
 */
function localeFilesExist(states) {
  for (const locale of Object.keys(CONFIG.locales)) {
    for (const state of states) {
      // Check for landing.json as a representative file
      const landingPath = join(CONFIG.outputDir, locale, state, 'landing.json');
      if (!existsSync(landingPath)) {
        return false;
      }
    }
  }
  return true;
}

/**
 * Check if regeneration is needed based on hash
 */
function needsRegeneration(stateFiles) {
  if (stateFiles.length === 0) return false;

  const currentHash = calculateCombinedHash(stateFiles);
  if (!existsSync(CONFIG.hashFile)) return true;

  const states = stateFiles.map((f) => f.state);
  if (!localeFilesExist(states)) return true;

  const storedHash = readFileSync(CONFIG.hashFile, 'utf8').trim();
  return currentHash !== storedHash;
}

/**
 * Save current hash for cache invalidation
 */
function saveHash(stateFiles) {
  const hash = calculateCombinedHash(stateFiles);
  writeFileSync(CONFIG.hashFile, hash, 'utf8');
}

/**
 * Clean output directory
 */
function cleanOutputDir() {
  if (existsSync(CONFIG.outputDir)) {
    rmSync(CONFIG.outputDir, { recursive: true });
  }
}

/**
 * Write locale files for a single state
 */
function writeStateLocaleFiles(stateData, state) {
  let fileCount = 0;

  for (const [locale, namespaces] of Object.entries(stateData)) {
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

  return fileCount;
}

/**
 * Validate that English and Spanish have matching keys for a state
 */
function validateStateCompleteness(stateData, state) {
  const warnings = [];

  // Collect all keys from English
  const englishKeys = new Map();
  for (const [namespace, translations] of Object.entries(stateData.en || {})) {
    for (const key of Object.keys(translations)) {
      englishKeys.set(`${namespace}.${key}`, true);
    }
  }

  // Check Spanish for missing keys
  for (const [fullKey] of englishKeys) {
    const [namespace, key] = fullKey.split('.');
    if (!stateData.es?.[namespace]?.[key]) {
      warnings.push(`Missing Spanish translation in ${state}: ${fullKey}`);
    }
  }

  return warnings;
}

/**
 * Generate TypeScript resource file from locale directory
 *
 * Scans content/locales/ for all JSON files and generates a TypeScript file
 * with static imports and a typed resource map. This eliminates the need
 * to manually maintain 100+ imports in i18n.ts and translations.ts.
 *
 * Output: src/lib/generated-locale-resources.ts
 */
function generateResourceFile() {
  const outputPath = join(rootDir, 'src', 'lib', 'generated-locale-resources.ts');

  if (!existsSync(CONFIG.outputDir)) {
    console.log(
      '⚠️  No locales directory found, skipping resource file generation'
    );
    return;
  }

  // Discover all locale JSON files: {lang}/{state}/{namespace}.json
  const imports = [];
  const stateMap = {}; // { state: { lang: { namespace: varName } } }
  const allNamespaces = new Set();

  const languages = readdirSync(CONFIG.outputDir)
    .filter(
      (f) =>
        !f.startsWith('.') &&
        existsSync(join(CONFIG.outputDir, f)) &&
        readdirSync(join(CONFIG.outputDir, f)).length > 0
    )
    .sort();

  for (const lang of languages) {
    const langDir = join(CONFIG.outputDir, lang);
    const states = readdirSync(langDir)
      .filter(
        (f) =>
          !f.startsWith('.') &&
          existsSync(join(langDir, f)) &&
          readdirSync(join(langDir, f)).length > 0
      )
      .sort();

    for (const state of states) {
      const stateDir = join(CONFIG.outputDir, lang, state);
      const files = readdirSync(stateDir)
        .filter((f) => f.endsWith('.json'))
        .sort();

      for (const file of files) {
        const namespace = file.replace('.json', '');
        const varName = `${lang}${state.toUpperCase()}${namespace.charAt(0).toUpperCase()}${namespace.slice(1)}`;
        const importPath = `@/content/locales/${lang}/${state}/${namespace}.json`;

        imports.push({ varName, importPath });
        allNamespaces.add(namespace);

        if (!stateMap[state]) stateMap[state] = {};
        if (!stateMap[state][lang]) stateMap[state][lang] = {};
        stateMap[state][lang][namespace] = varName;
      }
    }
  }

  // Generate TypeScript file content
  const lines = [
    '// Auto-generated by content/scripts/generate-locales.js — DO NOT EDIT',
    '// Run `pnpm copy:generate` to regenerate this file',
    '',
  ];

  // Static imports
  for (const { varName, importPath } of imports) {
    lines.push(`import ${varName} from '${importPath}'`);
  }
  lines.push('');

  // stateResources export
  lines.push('export const stateResources = {');
  const sortedStates = Object.keys(stateMap).sort();
  for (const state of sortedStates) {
    lines.push(`  ${state}: {`);
    const sortedLangs = Object.keys(stateMap[state]).sort();
    for (const lang of sortedLangs) {
      lines.push(`    ${lang}: {`);
      const sortedNs = Object.keys(stateMap[state][lang]).sort();
      for (const ns of sortedNs) {
        lines.push(`      ${ns}: ${stateMap[state][lang][ns]},`);
      }
      lines.push('    },');
    }
    lines.push('  },');
  }
  lines.push('}');
  lines.push('');

  // namespaces export with Namespace type
  const sortedNamespaces = [...allNamespaces].sort();
  lines.push(
    `export const namespaces = [${sortedNamespaces.map((n) => `'${n}'`).join(', ')}] as const`
  );
  lines.push('export type Namespace = (typeof namespaces)[number]');
  lines.push('');

  writeFileSync(outputPath, lines.join('\n'), 'utf8');
  console.log(`📝 Generated resource file: ${rel(outputPath)}`);
  console.log(
    `   ${imports.length} imports, ${sortedStates.length} states, ${sortedNamespaces.length} namespaces`
  );
}

/**
 * Main entry point
 */
function main() {
  console.log('🌐 Generating i18n locale files...');

  // Discover state CSV files
  const stateFiles = discoverStateCsvFiles();

  if (stateFiles.length === 0) {
    console.log('⚠️  No state CSV files found in content/states/');
    console.log('   Expected: content/states/dc.csv, content/states/co.csv, etc.');
    console.log('   Download from Google Sheets (each tab as separate CSV).');
    // Still generate resource file from any existing JSON files
    generateResourceFile();
    process.exit(0);
  }

  console.log(`📁 Found ${stateFiles.length} state CSV file(s):`);
  for (const { state, csvPath } of stateFiles) {
    console.log(`   - ${state}: ${rel(csvPath)}`);
  }
  console.log();

  // Check if CSV regeneration needed
  if (needsRegeneration(stateFiles)) {
    try {
      // Clean output directory
      cleanOutputDir();

      let totalFileCount = 0;
      const allWarnings = [];

      // Process each state CSV
      for (const { state, csvPath } of stateFiles) {
        console.log(`📖 Processing ${state.toUpperCase()}:`);
        console.log(`   Reading: ${rel(csvPath)}`);

        const csvContent = readFileSync(csvPath, 'utf8');
        const rows = parseCSV(csvContent);
        console.log(`   Found ${rows.length - 1} content entries`);

        // Build locale data for this state
        const stateData = buildStateLocaleData(rows, state);

        // Validate completeness
        const warnings = validateStateCompleteness(stateData, state);
        allWarnings.push(...warnings);

        // Write locale files
        const fileCount = writeStateLocaleFiles(stateData, state);
        totalFileCount += fileCount;
        console.log(`   Generated ${fileCount} files\n`);
      }

      // Show validation warnings
      if (allWarnings.length > 0) {
        console.log('⚠️  Validation warnings:');
        allWarnings.forEach((w) => console.log(`   - ${w}`));
        console.log();
      }

      // Save hash for caching
      saveHash(stateFiles);

      // Summary
      console.log(`✅ Generated ${totalFileCount} total locale files:`);
      for (const locale of Object.keys(CONFIG.locales)) {
        for (const { state } of stateFiles) {
          const dir = join(CONFIG.outputDir, locale, state);
          if (existsSync(dir)) {
            const files = readdirSync(dir);
            console.log(`   ${locale}/${state}/: ${files.join(', ')}`);
          }
        }
      }
      console.log();
    } catch (error) {
      console.error('❌ Failed to generate locales:');
      console.error(`   ${error.message}\n`);
      process.exit(1);
    }
  } else {
    console.log('⚡ Locales unchanged (cached)');

    // List existing locale files
    if (existsSync(CONFIG.outputDir)) {
      const locales = readdirSync(CONFIG.outputDir);
      console.log(`   Existing: ${locales.join(', ')}`);
    }
    console.log();
  }

  // Always generate the TypeScript resource file from whatever JSON files exist on disk
  generateResourceFile();

  process.exit(0);
}

main();
