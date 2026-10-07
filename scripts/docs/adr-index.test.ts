import assert from 'node:assert/strict';
import { resolve } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  firstCommitAuthor,
  groupByStatus,
  normalizeStatus,
  parseAdr,
  renderAdrCards,
  renderInline,
  type AdrRecord,
} from './adr-index.ts';

const repoRoot = resolve(fileURLToPath(import.meta.url), '../../..');

const FIXTURE = `# 15. PII encryption at rest (AES-256-GCM + key rotation)

Date: 2026-05-04

## Status

Accepted

## Context

The portal persists personally identifiable information.
`;

/** A record with every field set, so a test can vary one thing at a time. */
function record(over: Partial<AdrRecord> = {}): AdrRecord {
  return {
    file: '0015-pii-encryption-at-rest.md',
    number: 15,
    title: 'PII encryption at rest',
    date: '2026-05-04',
    status: 'Accepted',
    author: 'Test Author',
    ...over,
  };
}

test('parseAdr reads the number, title, date, and status', () => {
  const adr = parseAdr('0015-pii-encryption-at-rest.md', FIXTURE);

  assert.equal(adr.number, 15);
  assert.equal(adr.title, 'PII encryption at rest (AES-256-GCM + key rotation)');
  assert.equal(adr.date, '2026-05-04');
  assert.equal(adr.status, 'Accepted');
  assert.equal(adr.file, '0015-pii-encryption-at-rest.md');
});

test('parseAdr names the file when the H1 is not a numbered ADR heading', () => {
  assert.throws(() => parseAdr('0031-new.md', '# A decision\n\nDate: 2026-01-01\n'), /0031-new\.md/);
});

test('parseAdr rejects a record with no Date line, rather than inventing one', () => {
  const noDate = '# 31. A decision\n\n## Status\n\nAccepted\n';

  assert.throws(() => parseAdr('0031-new.md', noDate), /Date/);
});

test('parseAdr rejects a record with no Status section', () => {
  assert.throws(() => parseAdr('0031-new.md', '# 31. A decision\n\nDate: 2026-01-01\n'), /Status/);
});

test('parseAdr rejects a record whose H1 number does not match its filename', () => {
  const mismatched = '# 23. A decision\n\nDate: 2026-01-01\n\n## Status\n\nAccepted\n';

  assert.throws(() => parseAdr('0022-a-decision.md', mismatched), /0022-a-decision\.md/);
});

test('normalizeStatus takes the leading word, so a prose status still yields a badge', () => {
  // ADR 0021 reads: `Accepted. Amends the "Contract references" decision in ...`
  assert.equal(normalizeStatus('Accepted. Amends the "Contract references" decision in [ADR 0017](x.md).'), 'Accepted');
});

test('normalizeStatus accepts any casing', () => {
  assert.equal(normalizeStatus('accepted'), 'Accepted');
  assert.equal(normalizeStatus('PROPOSED'), 'Proposed');
});

test('normalizeStatus throws on a value it does not know', () => {
  // A typo must fail the build. Falling back to a blank badge would publish a
  // card that silently claims nothing about the decision's standing.
  assert.throws(() => normalizeStatus('Acccepted'), /Acccepted/);
});

test('groupByStatus orders groups by standing, not alphabetically', () => {
  const groups = groupByStatus([
    record({ number: 1, status: 'Accepted' }),
    record({ number: 2, status: 'Superseded' }),
    record({ number: 3, status: 'Proposed' }),
  ]);

  assert.deepEqual(
    groups.map((g) => g.status),
    ['Proposed', 'Accepted', 'Superseded'],
    'open decisions come first, so the page opens with what still needs a call',
  );
});

test('groupByStatus omits a status no record uses', () => {
  const groups = groupByStatus([record({ status: 'Accepted' })]);

  assert.deepEqual(
    groups.map((g) => g.status),
    ['Accepted'],
  );
});

test('groupByStatus sorts newest decision first inside a group', () => {
  const groups = groupByStatus([
    record({ number: 17, date: '2026-07-01' }),
    record({ number: 21, date: '2026-08-26' }),
    record({ number: 20, date: '2026-08-14' }),
  ]);

  assert.deepEqual(
    groups[0].records.map((r) => r.number),
    [21, 20, 17],
  );
});

test('groupByStatus breaks a date tie on the higher number, so the order is stable', () => {
  // ADRs 0014 and 0015 share the date 2026-05-04.
  const groups = groupByStatus([record({ number: 14, date: '2026-05-04' }), record({ number: 15, date: '2026-05-04' })]);

  assert.deepEqual(
    groups[0].records.map((r) => r.number),
    [15, 14],
  );
});

test('renderAdrCards links each card at the record it summarizes', () => {
  const html = renderAdrCards(groupByStatus([record()]));

  assert.match(html, /<a class="adr-card" href="0015-pii-encryption-at-rest\.md">/);
  assert.match(html, /PII encryption at rest/);
  assert.match(html, /ADR 0015/, 'the number is zero-padded, matching the filenames');
});

test('renderAdrCards states the date in words and names the author', () => {
  const html = renderAdrCards(groupByStatus([record()]));

  assert.match(html, /May 4, 2026/);
  assert.match(html, /Test Author/);
});

test('renderAdrCards leaves the separator entity intact', () => {
  // Escaping the joined meta string turns `&middot;` into `&amp;middot;`, which
  // renders the entity as literal text on the page.
  const html = renderAdrCards(groupByStatus([record()]));

  assert.match(html, /May 4, 2026 &middot; Test Author/);
  assert.doesNotMatch(html, /&amp;middot;/);
});

test('renderAdrCards escapes a title that would otherwise be read as markup', () => {
  const html = renderAdrCards(groupByStatus([record({ title: 'Use <script> & tags' })]));

  assert.match(html, /Use &lt;script&gt; &amp; tags/);
});

test('renderInline renders the bold and code spans that titles actually use', () => {
  // ADR 0007's title bolds a library name; ADR 0017's names a path in code.
  // Inside a raw HTML card these would otherwise print their own markers.
  assert.equal(renderInline('Adopt **Bogus** and factory pattern'), 'Adopt <strong>Bogus</strong> and factory pattern');
  assert.equal(renderInline('Consolidate into an `/apps` monorepo'), 'Consolidate into an <code>/apps</code> monorepo');
});

test('renderInline escapes before converting, so a tag in a title stays inert', () => {
  assert.equal(renderInline('A `<T>` parameter'), 'A <code>&lt;T&gt;</code> parameter');
  assert.equal(renderInline('<script>alert(1)</script>'), '&lt;script&gt;alert(1)&lt;/script&gt;');
});

test('renderInline leaves underscores alone, because identifiers use them', () => {
  assert.equal(renderInline('Rename state_code to stateCode'), 'Rename state_code to stateCode');
});

test('renderInline does not convert markers inside a code span', () => {
  assert.equal(renderInline('Use `a**b` verbatim'), 'Use <code>a**b</code> verbatim');
});

test('renderAdrCards carries the status as a class, so the badge can be styled', () => {
  const html = renderAdrCards(groupByStatus([record({ status: 'Proposed' })]));

  assert.match(html, /adr-status adr-status-proposed/);
});

test('renderAdrCards omits the author line for a record git knows nothing about', () => {
  const html = renderAdrCards(groupByStatus([record({ author: null })]));

  assert.match(html, /May 4, 2026/);
  assert.doesNotMatch(html, /&middot;\s*<\/span>/, 'no dangling separator with nothing after it');
});

test('renderAdrCards heads each group with its status and count', () => {
  const html = renderAdrCards(groupByStatus([record({ number: 1 }), record({ number: 2 })]));

  assert.match(html, /<h2 class="adr-group" data-count="2">Accepted<\/h2>/);
});

test('the group count is an attribute, so it stays out of the page outline', () => {
  // docfx builds "In this article" from heading text. With the count inside the
  // heading, the outline read "Accepted28" with no separator.
  const html = renderAdrCards(groupByStatus([record()]));

  assert.doesNotMatch(html, /<h2[^>]*>[^<]*\d/, 'no digit in the rendered heading text');
});

test('firstCommitAuthor returns null for a path git does not track', () => {
  assert.equal(firstCommitAuthor(repoRoot, 'docs/adr/0000-not-a-real-record.md'), null);
});
