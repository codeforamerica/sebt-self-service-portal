#!/usr/bin/env node
/**
 * Inject State-Specific Fonts into HTML
 *
 * Auto-detects custom fonts from design tokens and injects Google Fonts links into index.html.
 * Runs during prebuild to ensure fonts match the current state's design tokens.
 *
 * Usage:
 *   node inject-fonts.js           # Defaults to DC
 *   node inject-fonts.js co        # Colorado state
 *   STATE=tx node inject-fonts.js  # Environment variable
 *
 * Workflow:
 * 1. Read design/states/{state}.json
 * 2. Extract font families from theme tokens
 * 3. Generate Google Fonts URLs with appropriate weights
 * 4. Inject <link> tags into index.html at STATE_FONTS_PLACEHOLDER
 */

import { readFileSync, writeFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const projectRoot = join(__dirname, '..');

const GOOGLE_FONTS_BASE = 'https://fonts.googleapis.com/css2';
const DEFAULT_WEIGHTS = [300, 400, 700];
const DEFAULT_STYLES = ['normal', 'italic'];

const GOOGLE_FONTS = new Set([
  'urbanist',
  'public sans',
  'roboto',
  'open sans',
  'lato',
  'montserrat',
  'raleway',
  'poppins',
  'inter',
  'work sans',
  'nunito'
]);

function extractFonts(tokensJson, state) {
  const fonts = new Set();

  if (!tokensJson.theme) {
    console.warn(`⚠️  No theme object found in ${state}.json`);
    return fonts;
  }

  const theme = tokensJson.theme;

  if (theme['theme-font-type-sans']?.$value) {
    const fontName = theme['theme-font-type-sans'].$value.replace(/'/g, '').toLowerCase();
    fonts.add(fontName);
  }

  if (theme['theme-font-type-serif']?.$value) {
    const fontName = theme['theme-font-type-serif'].$value.replace(/'/g, '').toLowerCase();
    fonts.add(fontName);
  }

  return fonts;
}

function isGoogleFont(fontName) {
  return GOOGLE_FONTS.has(fontName.toLowerCase());
}

function generateGoogleFontsUrl(fontName) {
  const capitalized = fontName
    .split(' ')
    .map(word => word.charAt(0).toUpperCase() + word.slice(1))
    .join('+');

  const weightSpecs = [];
  DEFAULT_STYLES.forEach((style, styleIdx) => {
    DEFAULT_WEIGHTS.forEach(weight => {
      weightSpecs.push(`${styleIdx},${weight}`);
    });
  });

  return `${GOOGLE_FONTS_BASE}?family=${capitalized}:ital,wght@${weightSpecs.join(';')}&display=swap`;
}

function generateFontLinks(fonts) {
  if (fonts.size === 0) {
    return '<!-- No custom fonts required -->';
  }

  const googleFonts = Array.from(fonts).filter(isGoogleFont);

  if (googleFonts.length === 0) {
    return `<!-- Custom fonts: ${Array.from(fonts).join(', ')} (not on Google Fonts) -->`;
  }

  const lines = [
    '<!-- Google Fonts: Auto-injected from design tokens -->',
    '<link rel="preconnect" href="https://fonts.googleapis.com" />',
    '<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />'
  ];

  googleFonts.forEach(font => {
    const url = generateGoogleFontsUrl(font);
    const capitalizedName = font
      .split(' ')
      .map(word => word.charAt(0).toUpperCase() + word.slice(1))
      .join(' ');

    lines.push(`<link href="${url}" rel="stylesheet" />`);
    lines.push(`<!-- ${capitalizedName}: Light (300), Regular (400), Bold (700) with italics -->`);
  });

  return lines.map(line => `    ${line}`).join('\n');
}

function injectFonts(state) {
  const tokensPath = join(projectRoot, 'design/states', `${state}.json`);
  const htmlPath = join(projectRoot, 'index.html');

  console.log(`Reading tokens: ${tokensPath}`);
  const tokensJson = JSON.parse(readFileSync(tokensPath, 'utf8'));

  const fonts = extractFonts(tokensJson, state);
  console.log(`✅ Found ${fonts.size} custom font(s): ${Array.from(fonts).join(', ')}`);

  const fontLinks = generateFontLinks(fonts);

  let html = readFileSync(htmlPath, 'utf8');

  const placeholder = '<!-- STATE_FONTS_PLACEHOLDER: Auto-injected from design tokens during build -->';
  const startMarker = '<!-- INJECTED_FONTS_START -->';
  const endMarker = '<!-- INJECTED_FONTS_END -->';

  if (html.includes(placeholder)) {
    html = html.replace(placeholder, `${startMarker}\n${fontLinks}\n    ${endMarker}`);
  } else if (html.includes(startMarker) && html.includes(endMarker)) {
    const regex = new RegExp(`${startMarker}[\\s\\S]*?${endMarker}`, 'g');
    html = html.replace(regex, `${startMarker}\n${fontLinks}\n    ${endMarker}`);
  } else {
    throw new Error(`Neither placeholder nor markers found in index.html. Expected: ${placeholder} or ${startMarker}...${endMarker}`);
  }

  writeFileSync(htmlPath, html, 'utf8');
  console.log(`✅ Injected fonts for ${state.toUpperCase()} into index.html\n`);
}

function main() {
  try {
    const args = process.argv.slice(2);
    const state = (args[0] || process.env.STATE || 'dc').toLowerCase();

    injectFonts(state);
  } catch (error) {
    console.error('❌ Font injection failed:', error.message);
    process.exit(1);
  }
}

main();
