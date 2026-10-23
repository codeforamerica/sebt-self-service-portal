#!/usr/bin/env node
/**
 * Tests for rewrite-static-export-uri.js.
 * Run: node --test tofu/modules/sebt_enrollment_checker/functions/rewrite-static-export-uri.test.mjs
 *
 * The function file declares a bare `function handler(event)` because that is
 * what the CloudFront Functions runtime loads — it has no export, and adding
 * one risks the runtime rejecting the file. So the source is read and evaluated
 * here rather than imported, leaving the deployed artifact untouched.
 */
import { strict as assert } from 'node:assert'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { test } from 'node:test'
import { fileURLToPath } from 'node:url'

const source = readFileSync(
  join(dirname(fileURLToPath(import.meta.url)), 'rewrite-static-export-uri.js'),
  'utf8'
)

// eslint-disable-next-line no-new-func -- evaluates the CloudFront function in isolation
const handler = new Function(`${source}; return handler`)()

function rewrite(uri) {
  return handler({ request: { uri } }).uri
}

// Every route the static export emits as a flat .html file. If a route is added
// to the checker without appearing here, the rewrite is untested for it.
test('maps extensionless routes to their exported .html object', () => {
  assert.equal(rewrite('/check'), '/check.html')
  assert.equal(rewrite('/results'), '/results.html')
  assert.equal(rewrite('/disclaimer'), '/disclaimer.html')
  assert.equal(rewrite('/closed'), '/closed.html')
  assert.equal(rewrite('/outage'), '/outage.html')
})

test('treats a trailing slash as the same route', () => {
  assert.equal(rewrite('/check/'), '/check.html')
})

// default_root_object serves this; rewriting it to /.html would break the
// landing page.
test('leaves the root alone', () => {
  assert.equal(rewrite('/'), '/')
})

test('leaves assets untouched', () => {
  assert.equal(rewrite('/_next/static/chunks/main-abc.js'), '/_next/static/chunks/main-abc.js')
  assert.equal(rewrite('/favicon.ico'), '/favicon.ico')
  assert.equal(rewrite('/config.js'), '/config.js')
  assert.equal(rewrite('/img/usa-icons/close.svg'), '/img/usa-icons/close.svg')
})

// A miss has to reach the origin as a miss, so the custom_error_response blocks
// in main.tf can answer with 404.html — this is the whole point of the rewrite.
test('rewrites an unknown route so it misses and falls through to the 404 response', () => {
  assert.equal(rewrite('/foo'), '/foo.html')
  assert.equal(rewrite('/foo/bar'), '/foo/bar.html')
})

test('leaves an already-explicit .html path alone', () => {
  assert.equal(rewrite('/404.html'), '/404.html')
  assert.equal(rewrite('/index.html'), '/index.html')
})
