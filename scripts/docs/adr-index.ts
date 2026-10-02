/**
 * Builds the card list for the Architecture Decisions landing page.
 *
 * The records in `docs/adr/` are the only source. Nothing here is authored by
 * hand, so adding an ADR puts it on the page at the next docs build, and
 * renumbering one cannot leave a stale row behind. It replaces a Notion
 * database that had already drifted from the repository.
 *
 * Output goes to `docs/docfx/adr/_cards.md`, which the authored `index.md`
 * pulls in with a docfx INCLUDE. Splitting it that way keeps the page's prose
 * in Markdown where it can be edited, instead of inside a template string here.
 */
import { execFileSync } from 'node:child_process';
import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { formatStampDate } from './generate-doc-sections.ts';

/** Directory holding the records, relative to the repo root. */
export const ADR_SOURCE = 'docs/adr';

/** Generated partial, relative to the repo root. Git-ignored by `adr/*.md`. */
export const CARDS_FILE = 'docs/docfx/adr/_cards.md';

/**
 * Statuses a record may declare, in the order they appear on the page.
 *
 * Open decisions lead, so the page opens with what still needs a call rather
 * than with the longest group. Only `Proposed` and `Accepted` are in use today;
 * the rest are here so the first record to need one lands in a defined place
 * instead of failing the build.
 */
export const STATUS_ORDER = ['Proposed', 'Accepted', 'Superseded', 'Deprecated', 'Rejected'] as const;

export type AdrStatus = (typeof STATUS_ORDER)[number];

export interface AdrRecord {
  /** Filename, which is also the link target once docfx rewrites the extension. */
  file: string;
  number: number;
  title: string;
  /** Decision date, `YYYY-MM-DD`, from the record's own `Date:` line. */
  date: string;
  status: AdrStatus;
  /** First commit author, or null when git does not track the file yet. */
  author: string | null;
}

export interface AdrGroup {
  status: AdrStatus;
  records: AdrRecord[];
}

/** Markdown filenames in `dir`, excluding any authored page, in filename order. */
export function listAdrs(dir: string): string[] {
  return readdirSync(dir)
    .filter((name) => name.endsWith('.md') && name !== 'index.md')
    .sort();
}

/**
 * Reduces a `## Status` body to one of the known statuses.
 *
 * Most records hold a single bare word, but ADR 0021 states its standing as a
 * sentence with a link, so only the leading word is read. An unrecognized value
 * throws: a card whose badge is blank or invented says nothing true about where
 * the decision stands, and a typo should stop the build instead of shipping.
 */
export function normalizeStatus(raw: string): AdrStatus {
  const word = raw.trim().split(/[^A-Za-z]/)[0].toLowerCase();
  const match = STATUS_ORDER.find((status) => status.toLowerCase() === word);

  if (!match) {
    throw new Error(`Unknown ADR status ${JSON.stringify(raw.trim())}. Expected one of: ${STATUS_ORDER.join(', ')}`);
  }

  return match;
}

/**
 * Reads the card fields out of one record.
 *
 * Every field is required. A record missing one is a malformed record, and
 * failing here names the file, which is faster to act on than a card that
 * renders half-empty.
 */
export function parseAdr(file: string, markdown: string): Omit<AdrRecord, 'author'> {
  const heading = markdown.match(/^#\s+(\d+)\.\s+(.+?)\s*$/m);
  if (!heading) {
    throw new Error(`${file}: no "# N. Title" heading. The H1 is what gives the card its number and title.`);
  }

  const date = markdown.match(/^Date:\s*(\d{4}-\d{2}-\d{2})\s*$/m);
  if (!date) {
    throw new Error(`${file}: no "Date: YYYY-MM-DD" line. That line is the decision date the card shows.`);
  }

  const status = markdown.match(/^##\s+Status\s*$\n+(.+?)\s*$/m);
  if (!status) {
    throw new Error(`${file}: no "## Status" section with a value under it.`);
  }

  return {
    file,
    number: Number(heading[1]),
    title: heading[2],
    date: date[1],
    status: normalizeStatus(status[1]),
  };
}

/**
 * Buckets records by status and sorts each bucket newest decision first.
 *
 * The tie-break on number matters: several records share a date, and without it
 * their order would depend on the filesystem. It is descending to agree with
 * the date sort rather than fight it.
 */
export function groupByStatus(records: AdrRecord[]): AdrGroup[] {
  return STATUS_ORDER.map((status) => ({
    status,
    records: records
      .filter((record) => record.status === status)
      .sort((a, b) => (a.date === b.date ? b.number - a.number : b.date.localeCompare(a.date))),
  })).filter((group) => group.records.length > 0);
}

/** Escapes the characters that would otherwise be read as markup. */
function escapeHtml(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

/**
 * Renders the inline Markdown that ADR titles use, for a card that is raw HTML.
 *
 * A title is Markdown, and docfx renders it as such in the navigation and on
 * the record's own page. Inside an HTML block nothing processes it, so without
 * this the cards print the markers: ADR 0007 reads `**Bogus**` and ADR 0017
 * reads a path wrapped in backticks.
 *
 * Bold and code spans only, because those are what titles contain. Underscores
 * are deliberately not emphasis: identifiers are full of them, so treating
 * `state_code` as markup would corrupt more titles than it helped. Code spans
 * convert first, so a marker quoted inside one stays literal.
 *
 * Escaping runs before any of it, so the tags below are the only markup that
 * survives and a title containing a real tag is inert.
 */
export function renderInline(text: string): string {
  return escapeHtml(text)
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
}

/**
 * Renders the groups as full-width cards, one per row.
 *
 * Written as raw HTML because a card is one link wrapping several lines, which
 * Markdown link syntax cannot express. There are no blank lines inside the
 * container on purpose: a blank line closes an HTML block in CommonMark, and
 * the fragments after it get re-parsed as Markdown.
 *
 * The `href` keeps the `.md` extension. docfx rewrites links to the built page,
 * including inside raw HTML anchors, and hardcoding `.html` here would bypass
 * the check that reports a link to a record that no longer exists.
 */
export function renderAdrCards(groups: AdrGroup[]): string {
  const lines = ['<!-- Generated by scripts/docs/adr-index.ts. Do not edit. -->', '<div class="adr-index">'];

  for (const { status, records } of groups) {
    // The count is an attribute, not heading text, and the stylesheet draws it.
    // docfx builds its "In this article" outline from a heading's text, so a
    // count inside the heading was concatenated onto the status and the outline
    // read "Proposed2". It is also redundant next to the cards it counts, which
    // is why losing it to a pseudo-element costs nothing.
    lines.push(`  <h2 class="adr-group" data-count="${records.length}">${status}</h2>`, '  <div class="adr-list">');

    for (const record of records) {
      // Escaped before joining, never after: the separator is an entity, and
      // escaping the joined string would turn its ampersand into `&amp;`.
      //
      // A missing author drops out rather than leaving a blank after the
      // separator, which would read as absent data instead of an unknown.
      const meta = [formatStampDate(record.date), record.author]
        .filter((part): part is string => Boolean(part))
        .map(escapeHtml)
        .join(' &middot; ');

      lines.push(
        `    <a class="adr-card" href="${record.file}">`,
        `      <span class="adr-id">ADR ${String(record.number).padStart(4, '0')}</span>`,
        `      <span class="adr-title">${renderInline(record.title)}</span>`,
        `      <span class="adr-meta">${meta}</span>`,
        `      <span class="adr-status adr-status-${status.toLowerCase()}">${status}</span>`,
        '    </a>',
      );
    }

    lines.push('  </div>');
  }

  lines.push('</div>', '');
  return lines.join('\n');
}

/**
 * Author of the first commit to touch `file`, or null when git has no history
 * for it.
 *
 * `--follow` is load-bearing. The commit that renumbered nine records renamed
 * the files, so without it git reports that commit's author as the creator of
 * all nine and credits the wrong person for the decision. `--reverse` is not
 * used because it does not combine reliably with `--follow`; the oldest commit
 * is taken from the end of the default newest-first list instead.
 */
export function firstCommitAuthor(repoRoot: string, file: string): string | null {
  try {
    const stdout = execFileSync('git', ['log', '--follow', '--format=%an', '--', file], {
      cwd: repoRoot,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore'],
    });
    const authors = stdout.trim().split('\n').filter(Boolean);
    return authors.at(-1) ?? null;
  } catch {
    return null;
  }
}

/** Writes the card list and returns how many records it covers. */
export function writeAdrCards(repoRoot: string): number {
  const sourceDir = join(repoRoot, ADR_SOURCE);

  const records = listAdrs(sourceDir).map((file) => ({
    ...parseAdr(file, readFileSync(join(sourceDir, file), 'utf8')),
    author: firstCommitAuthor(repoRoot, `${ADR_SOURCE}/${file}`),
  }));

  if (records.length === 0) {
    throw new Error(`No ADRs found in ${sourceDir}`);
  }

  writeFileSync(join(repoRoot, CARDS_FILE), renderAdrCards(groupByStatus(records)));
  return records.length;
}
