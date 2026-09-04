import assert from 'node:assert/strict';
import { test } from 'node:test';
import { applyKeywords, hrefFor, readKeywords, stripDocMeta, stripSiteTitle, type SearchEntry } from './postprocess-search-index.ts';

const SITE = 'Summer EBT Self-Service Portal: Engineering Documentation';

test('strips the site title docfx appends to every page title', () => {
  assert.equal(stripSiteTitle(`Change user-facing text | ${SITE}`, SITE), 'Change user-facing text');
});

test('leaves a title that does not carry the suffix alone', () => {
  assert.equal(stripSiteTitle('Change user-facing text', SITE), 'Change user-facing text');
});

test('only strips the suffix at the end, so a mid-title mention survives', () => {
  assert.equal(stripSiteTitle(`${SITE} in depth`, SITE), `${SITE} in depth`);
});

test('reads space-separated keywords from front matter', () => {
  assert.deepEqual(readKeywords('---\nkeywords: i18n translation locale\n---\n\n# Title\n'), ['i18n', 'translation', 'locale']);
});

test('reads a YAML list of keywords', () => {
  assert.deepEqual(readKeywords('---\nkeywords:\n  - i18n\n  - locale\n---\n\n# Title\n'), ['i18n', 'locale']);
});

test('returns nothing for a page with no front matter', () => {
  assert.deepEqual(readKeywords('# Title\n\nBody.\n'), []);
});

test('returns nothing rather than throwing on malformed front matter', () => {
  assert.deepEqual(readKeywords('---\nkeywords: [unclosed\n---\n\n# Title\n'), []);
});

test('maps a source markdown path to the href docfx produces', () => {
  assert.equal(hrefFor('guides/content/add-a-key.md'), 'guides/content/add-a-key.html');
});

test('keywords are appended, so they are indexed without showing in the blurb', () => {
  const index: Record<string, SearchEntry> = {
    'guides/content/index.html': { href: 'guides/content/index.html', title: 'Change user-facing text', summary: 'Every word' },
  };

  assert.equal(applyKeywords(index, 'guides/content/index.html', ['i18n', 'locale']), true);
  assert.equal(index['guides/content/index.html'].summary, 'Every word i18n locale');
});

test('a page with no index entry is skipped rather than creating one', () => {
  const index: Record<string, SearchEntry> = {};

  assert.equal(applyKeywords(index, 'missing.html', ['i18n']), false);
  assert.deepEqual(index, {});
});

test('running twice does not stack duplicate keywords', () => {
  const index: Record<string, SearchEntry> = {
    'a.html': { href: 'a.html', title: 'A', summary: 'Body' },
  };

  assert.equal(applyKeywords(index, 'a.html', ['i18n', 'locale']), true);
  assert.equal(applyKeywords(index, 'a.html', ['i18n', 'locale']), false, 'second run should be a no-op');
  assert.equal(index['a.html'].summary, 'Body i18n locale');
});

test('the provenance line is removed from an indexed summary', () => {
  const summary = 'Change user-facing text Last updated September 3, 2026 · View source Every word that a family reads';

  assert.equal(stripDocMeta(summary), 'Change user-facing text Every word that a family reads');
});

test('a summary with no provenance line is left alone', () => {
  assert.equal(stripDocMeta('Every word that a family reads'), 'Every word that a family reads');
});

test('the provenance line is removed whichever separator survived extraction', () => {
  for (const sep of ['·', '&middot;']) {
    assert.equal(stripDocMeta(`Title Last updated January 9, 2026 ${sep} View source Body`), 'Title Body');
  }
});
