#!/usr/bin/env node
/**
 * Generate i18n Locale Files from CSV
 *
 * Transforms Google Sheet CSV exports into namespaced JSON files for react-i18next.
 * Supports multi-state deployments with separate CSV files per state.
 *
 * Usage:
 *   node packages/design-system/content/scripts/generate-locales.js           # Generate all locales
 *   node packages/design-system/content/scripts/generate-locales.js --watch   # Watch mode (future)
 *   node packages/design-system/content/scripts/generate-locales.js --validate  # Check content only: writes nothing, exits 1 on a content error
 *
 * CSV Files:
 *   content/shared.csv     # Optional state-agnostic base (see merge-content.js)
 *   content/states/dc.csv  # DC-specific content (downloaded from DC tab)
 *   content/states/co.csv  # CO-specific content (downloaded from CO tab)
 *
 * States come from STATE_CODES (src/lib/state.ts). A state's CSV is optional when
 * shared.csv exists; a state row replaces the shared row for the same key entirely.
 * --states dc,co overrides the list (test fixtures).
 *
 * CSV Format:
 *   Variable Name/Key,🟢 CO English,⚪ SOURCE English,🟢 CO Español,⚪ SOURCE Español,Notes
 *   S1 - Landing Page - Action Español,Aplica ahora,¿Necesito aplicar?,Apply now,Do I need to apply?,
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
 *   content/locales/en/co/common.json
 *   content/locales/en/dc/common.json
 *   content/locales/en/dc/landing.json
 *   content/locales/es/dc/common.json
 *   ...
 *
 * Features:
 * - Multi-state CSVs: Each state may have its own CSV file (content/states/{state}.csv) over a shared base
 * - Smart caching: Only regenerates if any CSV changed (SHA-256 hash)
 *  -- to regenerate without caching, comment out any use of saveHash below
 * - Namespace splitting: Organizes by page/component for lazy loading
 * - Variable interpolation: Preserves {state}, {year} placeholders for runtime
 * - Content checks: every run reports content defects, cached or not (see
 *   validate-content.js); only --validate fails on them, so a sheet typo never
 *   blocks dev or a build
 */

// load-env.js is portal-specific and not needed in the shared package.
// The script does not use any process.env variables.
import { createHash } from 'crypto';
import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from 'fs';
import * as path from 'path';
import { dirname, join, relative } from 'path';
import { fileURLToPath } from 'url';
import {
  loadStateRows,
  locateContent,
  resolveStates,
} from './merge-content.js';
import { validateStateContent } from './validate-content.js';

// Parse CLI arguments
const cliArgs = process.argv.slice(2)
function getCliArg(name) {
  const idx = cliArgs.indexOf(name)
  return idx !== -1 ? cliArgs[idx + 1] : null
}
const csvDirOverride = getCliArg('--csv-dir')
const statesOverride = getCliArg('--states')  // comma-separated state codes; defaults to STATE_CODES
const outDirOverride = getCliArg('--out-dir')
const tsOutOverride  = getCliArg('--ts-out')
const appFilter      = getCliArg('--app')   // 'portal' | 'enrollment' | null (all)
const sectionsFilter = getCliArg('--sections')  // comma-separated, e.g., 'S1,GLOBAL'
const allowedSections = sectionsFilter ? sectionsFilter.split(',').map(s => s.trim()) : null
// Boolean flag, so it is not read with getCliArg (which expects a value after the name)
const validateOnly = cliArgs.includes('--validate')

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const contentDir = join(__dirname, '..');
const rootDir = join(contentDir, '..');
const rel = p => relative(rootDir, p);

const ignoredStringIndicator = "!N/A!"

// Configuration
const CONFIG = {
  // State CSV files directory (content/states/{state}.csv)
  statesDir: csvDirOverride
    ? path.resolve(process.cwd(), csvDirOverride)
    : join(contentDir, 'states'),
  outputDir: outDirOverride
    ? path.resolve(process.cwd(), outDirOverride)
    : join(contentDir, 'locales'),
  locales: {
    am: 'Amharic',    
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
    // S8 - OIDC/MyColorado callback
    'callback': 'login',
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
    // S5 - CO Bulk Order New Cards flow (initial + who screens share optionalId via S5 fallback,
    // but the "bulk order new cards" intro screen is used by components in confirmInfo namespace)
    'bulk order new cards': 'confirmInfo',
    // S9 - Outage page
    'outage page': 'outage',
    // S10 - Step-up verification screens
    'step-up disclaimer': 'stepUpDisclaimer',
    'step-up failure': 'stepUpFailure',
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
    // "Co-loaded Address Update" shares the `dashboard` namespace with "Pin Card".
    // Prefix isolates the co-loaded mailing-address page so its keys don't
    // collide with (and overwrite) `title` / `body` / `action` from Pin Card.
    'co-loaded address update': 'coLoadedAddressUpdate',
    'address not found': 'notFound',
    'suggested address': 'suggested',
    'abbreviated address': 'abbreviated',
    'callback': 'callback',
    'bulk order new cards': 'replacementCards',
  },
};

// The hash lives inside each output directory so every consumer (portal,
// enrollment checker, package default) caches independently — a shared hash
// file would let the first app's regeneration silently skip the second's.
CONFIG.hashFile = join(CONFIG.outputDir, '.copy-hash');

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

// Rows whose keys parse to the same locale key are the same content, however the CSV spells them.
function localeKey(row) {
  const parsed = row.key.includes(' - ') ? parseContentKey(row.key) : null;
  return parsed ? `${parsed.namespace}.${parsed.key}` : row.key;
}

/**
 * Build locale data structure from normalized content rows ({ key, en, es, am }) for a single state
 * Returns: { en: { common: { key: value }, landing: { key: value } }, es: { ... } }
 */
function buildStateLocaleData(rows) {
  // Initialize data structure for this state
  const data = {};
  for (const locale of Object.keys(CONFIG.locales)) {
    data[locale] = {};
  }

  const sourceLocales = ['en', 'es', 'am'];

  for (const row of rows) {
    // Skip rows the state marked as not applicable
    if (row.en === ignoredStringIndicator) continue;

    const parsed = parseContentKey(row.key);
    if (!parsed) continue;

    // Section-level filter: skip rows from sections not in the allowed list
    if (allowedSections && !allowedSections.includes(parsed.section)) continue;

    const { namespace, key } = parsed;

    for (const locale of sourceLocales) {
      // eslint-disable-next-line security/detect-object-injection -- locale comes from a fixed list
      const value = row[locale];
      // A language is only emitted when the source CSV has a column for it
      if (value === undefined) continue;

      // Don't overwrite a non-empty value with an empty one
      // (handles key collisions where multiple CSV rows map to the same namespace+key)
      // eslint-disable-next-line security/detect-object-injection -- locale comes from a fixed list
      const namespaces = data[locale];
      if (!namespaces[namespace]) {
        namespaces[namespace] = {};
      }
      if (value || !namespaces[namespace][key]) {
        namespaces[namespace][key] = value;
      }
    }
  }

  return data;
}

/**
 * Calculate combined SHA-256 hash of shared.csv, all state CSV files and the scripts
 */
function calculateCombinedHash(content) {
  const hash = createHash('sha256');

  // Include the script itself so logic changes invalidate the cache
  hash.update(readFileSync(fileURLToPath(import.meta.url), 'utf8'));
  hash.update(readFileSync(join(__dirname, 'merge-content.js'), 'utf8'));

  // Include output-shaping CLI args so e.g. a --sections change regenerates
  hash.update(JSON.stringify({ app: appFilter, sections: allowedSections }));

  if (content.sharedPath) {
    hash.update(`shared:${readFileSync(content.sharedPath, 'utf8')}`);
  }

  for (const { state, csvPath } of content.states) {
    hash.update(`state:${state}`);
    if (csvPath) {
      hash.update(`${state}:${readFileSync(csvPath, 'utf8')}`);
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
function needsRegeneration(content) {
  const currentHash = calculateCombinedHash(content);
  if (!existsSync(CONFIG.hashFile)) return true;

  const states = content.states.map((f) => f.state);
  if (!localeFilesExist(states)) return true;

  const storedHash = readFileSync(CONFIG.hashFile, 'utf8').trim();
  return currentHash !== storedHash;
}

/**
 * Save current hash for cache invalidation
 */
function saveHash(content) {
  const hash = calculateCombinedHash(content);
  mkdirSync(CONFIG.outputDir, { recursive: true });
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

  // Check other locales for missing keys
  for (const [fullKey] of englishKeys) {
    const [namespace, key] = fullKey.split('.');
    if (!stateData.es?.[namespace]?.[key]) {
      warnings.push(`Missing Spanish translation in ${state}: ${fullKey}`);
    }
    if (!stateData.am?.[namespace]?.[key]) {
      warnings.push(`Missing Amharic translation in ${state}: ${fullKey}`);
    }
  }
  

  return warnings;
}

// Which namespaces belong to which app.
// 'all' = shared, included in every app's barrel file.
const NAMESPACE_APP = {
  common:                 'all',
  landing:                'all',
  disclaimer:             'all',
  personalInfo:           'all',
  confirmInfo:            'all',
  result:                 'all',
  // Portal-specific
  login:                  'portal',
  idProofing:             'portal',
  optIn:                  'portal',
  offBoarding:            'portal',
  outage:                 'all',
  // Portal-only pages sharing CSV section S9 with the outage page (notification opt-in flow).
  email:                  'portal',
  enterEmail:             'portal',
  language:               'portal',
  signIn:                 'portal',
  text:                   'portal',
  dashboard:              'portal',
  edit:                   'portal',
  editContactPreferences: 'portal',
  editMailingAddress:     'portal',
  stepUpDisclaimer:       'portal',
  stepUpFailure:          'portal',
  proto:                  'portal',
  // S11 error/maintenance pages — namespaces derive from the CSV page names.
  // No code consumes these yet; classification keeps each app's bundle free of
  // the other app's page copy.
  '404Portal':                  'portal',
  maintenancePortal:            'portal',
  '404EnrollmentChecker':       'enrollment',
  maintenanceEnrollmentChecker: 'enrollment',
}

function isNamespaceForApp(namespace, app) {
  if (!app) return true
  // eslint-disable-next-line security/detect-object-injection -- namespace comes from JSON filenames, not user input
  const mapped = NAMESPACE_APP[namespace] ?? 'all'
  return mapped === 'all' || mapped === app
}

/**
 * Drop namespaces that belong to another app before validation and JSON emission.
 * A CSV section can hold pages for multiple apps (e.g. S9 has the shared outage page
 * alongside portal-only opt-in pages), so section filtering alone would emit orphaned
 * JSON files the app never imports.
 */
function filterStateDataForApp(stateData, app) {
  if (!app) return stateData
  const filtered = {}
  for (const [locale, namespaces] of Object.entries(stateData)) {
    // eslint-disable-next-line security/detect-object-injection -- locale comes from CONFIG.locales keys
    filtered[locale] = Object.fromEntries(
      Object.entries(namespaces).filter(([namespace]) => isNamespaceForApp(namespace, app))
    )
  }
  return filtered
}

/**
 * Read one state's merged content (shared base plus its own CSV) into this app's
 * locale data and check it.
 * Returns: { entryCount, stateData, errors }
 */
function readStateContent(stateFile, sharedPath) {
  const rows = loadStateRows(stateFile, sharedPath, localeKey);
  const stateData = filterStateDataForApp(buildStateLocaleData(rows), appFilter);
  const { errors } = validateStateContent(stateData, stateFile.state);

  return { entryCount: rows.length, stateData, errors };
}

/**
 * One line per finding: which rule, where, and why; then the offending copy.
 * Completeness warnings are already plain strings and pass through.
 */
function formatFinding(finding) {
  if (typeof finding === 'string') return finding;
  const { rule, state, locale, key, detail, value } = finding;
  const line = `[${rule}] ${state}/${locale} ${key}: ${detail}`;
  return value ? `${line}\n       copy: ${JSON.stringify(value)}` : line;
}

function reportFindings(errors, warnings) {
  if (warnings.length > 0) {
    console.log('⚠️  Validation warnings:');
    warnings.forEach((w) => console.log(`   - ${formatFinding(w)}`));
    console.log();
  }
  if (errors.length > 0) {
    console.error(`❌ Content errors (${errors.length}). Fix the cell in the content sheet and the state CSV:`);
    errors.forEach((e) => console.error(`   - ${formatFinding(e)}`));
    console.error();
  }
}

/**
 * --validate: check every state's content without writing anything. Always reads
 * the CSVs, so a cached output directory cannot hide a defect. A missing
 * translation is reported as a warning; a content error fails the run.
 * Returns the process exit code.
 */
function validateContentOnly(content) {
  console.log('🔎 Validating locale content (no files written)...');

  const allErrors = [];
  const allWarnings = [];
  for (const stateFile of content.states) {
    const { stateData, errors } = readStateContent(stateFile, content.sharedPath);
    allErrors.push(...errors);
    allWarnings.push(...validateStateCompleteness(stateData, stateFile.state));
  }

  reportFindings(allErrors, allWarnings);

  if (allErrors.length > 0) {
    return 1;
  }
  console.log(`✅ Content checks passed for ${content.states.map((f) => f.state).join(', ')}\n`);
  return 0;
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
  const outputPath = tsOutOverride
    ? path.resolve(process.cwd(), tsOutOverride)
    : join(rootDir, 'src', 'lib', 'generated-locale-resources.ts');

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
        const namespace = file.replace('.json', '')
        if (!isNamespaceForApp(namespace, appFilter)) continue
        // Sanitize namespace for use as a JS identifier (handle hyphens → camelCase)
        const safeNs = namespace.replace(/-([a-z])/g, (_, c) => c.toUpperCase());
        const varName = `${lang}${state.toUpperCase()}${safeNs.charAt(0).toUpperCase()}${safeNs.slice(1)}`;
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
        // Quote keys that aren't valid JS identifiers (e.g., contain hyphens)
        const key = /^[a-zA-Z_$][a-zA-Z0-9_$]*$/.test(ns) ? ns : `'${ns}'`;
        lines.push(`      ${key}: ${stateMap[state][lang][ns]},`);
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
  let content;
  try {
    content = locateContent(CONFIG.statesDir, resolveStates(statesOverride));
  } catch (error) {
    console.error(`❌ ${error.message}`);
    process.exit(1);
  }
  const stateFiles = content.states;

  if (validateOnly) {
    process.exit(validateContentOnly(content));
    return;
  }

  console.log('🌐 Generating i18n locale files...');

  if (content.sharedPath) {
    console.log(`📁 Shared content: ${rel(content.sharedPath)}`);
  }
  console.log(`📁 Found ${stateFiles.length} state(s):`);
  for (const { state, csvPath } of stateFiles) {
    console.log(`   - ${state}: ${csvPath ? rel(csvPath) : 'shared content only'}`);
  }
  console.log();

  // Check if regeneration needed
  if (needsRegeneration(content)) {
    try {
      // Clean output directory
      cleanOutputDir();

      let totalFileCount = 0;
      const allErrors = [];
      const allWarnings = [];

      // Process each state
      for (const stateFile of stateFiles) {
        const { state, csvPath } = stateFile;
        console.log(`📖 Processing ${state.toUpperCase()}:`);
        if (csvPath) {
          console.log(`   Reading: ${rel(csvPath)}`);
        }

        // Build locale data for this state, keeping only this app's namespaces,
        // and check its content
        const { entryCount, stateData, errors } = readStateContent(stateFile, content.sharedPath);
        console.log(`   Found ${entryCount} content entries`);
        allErrors.push(...errors);
        allWarnings.push(...validateStateCompleteness(stateData, state));

        // Write locale files
        const fileCount = writeStateLocaleFiles(stateData, state);
        totalFileCount += fileCount;
        console.log(`   Generated ${fileCount} files\n`);
      }

      // Report content findings. They never fail a generate run; --validate does.
      reportFindings(allErrors, allWarnings);

      // Save hash for caching
      saveHash(content);

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

    // Nothing is rewritten, but a content defect is still in the CSV: keep reporting it.
    reportFindings(
      stateFiles.flatMap((stateFile) => readStateContent(stateFile, content.sharedPath).errors),
      []
    );
  }

  // Always generate the TypeScript resource file from whatever JSON files exist on disk
  generateResourceFile();

  process.exit(0);
}

main();