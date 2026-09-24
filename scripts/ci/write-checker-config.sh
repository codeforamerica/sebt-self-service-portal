#!/usr/bin/env bash
# Enrollment Checker Runtime Config Writer
#
# The enrollment checker is a static export, so there is no server to hand the
# browser its config at request time. Instead the deployed bucket carries a
# config.js that assigns window.__CHECKER_CONFIG__, loaded before the app bundle.
# Writing it at deploy, rather than inlining values at build, lets one export be
# promoted between environments by rewriting this single file.
#
# Usage:
#   ./scripts/ci/write-checker-config.sh <output-path>
#
# Every input is read from the environment and is optional. An unset or blank
# value is left out of config.js, so the checker keeps its build-time default and
# that integration stays off wherever the value is not set.
#
#   API_BASE_URL, PORTAL_URL, APPLICATION_URL        http(s) URLs
#   AMPLITUDE_API_KEY, MIXPANEL_TOKEN, SITEIMPROVE_ID
#   META_PIXEL, META_PIXEL_ACTION
#   ADENTIFI_PIXEL_LANDING, ADENTIFI_PIXEL_APPLY_NOW
#   SHOW_SCHOOL_FIELD, CHECKER_ENABLED,
#   BOT_PROTECTION_ENABLED                           "true" or "false"
#
# A malformed URL or boolean fails the step and writes nothing, so a bad value is
# caught at deploy instead of breaking the checker in visitors' browsers.

set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 <output-path>" >&2
  exit 2
fi

CONFIG_OUTPUT="$1" node - <<'NODE'
const { writeFileSync } = require('node:fs')

// config.js key -> environment variable, grouped by how each value is checked.
const URLS = {
  apiBaseUrl: 'API_BASE_URL',
  portalUrl: 'PORTAL_URL',
  applicationUrl: 'APPLICATION_URL'
}
const STRINGS = {
  amplitudeApiKey: 'AMPLITUDE_API_KEY',
  mixpanelToken: 'MIXPANEL_TOKEN',
  siteImproveId: 'SITEIMPROVE_ID',
  metaPixel: 'META_PIXEL',
  metaPixelAction: 'META_PIXEL_ACTION',
  adentifiPixelLanding: 'ADENTIFI_PIXEL_LANDING',
  adentifiPixelApplyNow: 'ADENTIFI_PIXEL_APPLY_NOW'
}
const BOOLEANS = {
  showSchoolField: 'SHOW_SCHOOL_FIELD',
  checkerEnabled: 'CHECKER_ENABLED',
  botProtectionEnabled: 'BOT_PROTECTION_ENABLED'
}

const read = (name) => (process.env[name] ?? '').trim()
const config = {}
const problems = []

for (const [key, name] of Object.entries(URLS)) {
  const value = read(name)
  if (!value) continue
  const protocol = URL.canParse(value) ? new URL(value).protocol : null
  if (protocol === 'https:' || protocol === 'http:') {
    config[key] = value
  } else {
    problems.push(`${name} must be an http(s) URL, got: ${value}`)
  }
}

for (const [key, name] of Object.entries(STRINGS)) {
  const value = read(name)
  if (value) config[key] = value
}

for (const [key, name] of Object.entries(BOOLEANS)) {
  const value = read(name)
  if (!value) continue
  if (value === 'true' || value === 'false') {
    config[key] = value === 'true'
  } else {
    problems.push(`${name} must be "true" or "false", got: ${value}`)
  }
}

if (problems.length > 0) {
  for (const problem of problems) console.error(`❌ ${problem}`)
  process.exit(1)
}

// JSON.stringify rather than interpolating into a template: a value containing a
// quote or a line break stays inside its string instead of breaking out into code.
const output = process.env.CONFIG_OUTPUT
writeFileSync(output, `window.__CHECKER_CONFIG__ = ${JSON.stringify(config, null, 2)};\n`)
console.log(`✅ Wrote ${Object.keys(config).length} value(s) to ${output}`)
NODE
