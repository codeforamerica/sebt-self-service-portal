/**
 * Repairs `_site/index.json`, the lunr index docfx's ExtractSearchIndex writes.
 *
 * Two problems are fixed, both caused by docfx rather than by our content.
 *
 * First, every indexed title ends with the site title, because the extractor
 * reads the `<title>` tag verbatim. With 554 entries carrying "Summer EBT
 * Self-Service Portal: Engineering Documentation", a search for "portal" or
 * "EBT" matches every document on the title field, which is the field lunr
 * weighs most. Stripping the suffix is what makes those searches discriminate.
 *
 * Second, docfx drops unknown front matter keys, so a `keywords:` list never
 * reaches the page or the index. Pages can still declare one, and the terms are
 * appended to the indexed summary here. That is what lets a page be found by a
 * word it does not literally contain, such as "i18n" or "translation" for the
 * content guide.
 */
import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parse } from 'yaml';

export interface SearchEntry {
  href: string;
  title: string;
  summary: string;
}

/**
 * Removes the trailing site title docfx appends to every `<title>`.
 *
 * Matching is anchored to the separator and the exact site title so that a page
 * legitimately ending in those words keeps them.
 */
export function stripSiteTitle(title: string, siteTitle: string): string {
  const suffix = ` | ${siteTitle}`;
  return title.endsWith(suffix) ? title.slice(0, -suffix.length).trim() : title.trim();
}

/**
 * Matches the provenance line `generate-doc-sections.ts` writes below each H1.
 *
 * The extractor reads the whole article body, so that line lands in every
 * summary. Left alone it shows up in each result blurb, and makes "updated" and
 * "source" match every page, which is the same problem the site title caused in
 * the title field.
 */
const DOC_META = /\s*Last updated [A-Z][a-z]+ \d{1,2}, \d{4}\s*(?:·|&middot;)\s*View source\s*/g;

export function stripDocMeta(summary: string): string {
  return summary.replace(DOC_META, ' ').replace(/\s{2,}/g, ' ').trim();
}

/** Reads a `keywords` front matter value as a list of terms. */
export function readKeywords(markdown: string): string[] {
  if (!markdown.startsWith('---\n')) {
    return [];
  }

  const end = markdown.indexOf('\n---', 4);
  if (end === -1) {
    return [];
  }

  let front: unknown;
  try {
    front = parse(markdown.slice(4, end));
  } catch {
    return [];
  }

  const keywords = (front as { keywords?: unknown } | null)?.keywords;
  if (Array.isArray(keywords)) {
    return keywords.map(String);
  }
  return typeof keywords === 'string' ? keywords.split(/[,\s]+/).filter(Boolean) : [];
}

/** Every Markdown file under `dir`, as paths relative to it. */
export function listMarkdownTree(dir: string, base = dir): string[] {
  return readdirSync(dir).flatMap((name) => {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      return name === '_site' || name === 'template' ? [] : listMarkdownTree(full, base);
    }
    return name.endsWith('.md') ? [relative(base, full)] : [];
  });
}

/** The site href docfx produces for a source Markdown path. */
export function hrefFor(markdownPath: string): string {
  return markdownPath.replace(/\\/g, '/').replace(/\.md$/, '.html');
}

/**
 * Appends keywords to a page's indexed summary.
 *
 * lunr scores a term the same wherever it sits in the field, but the search UI
 * shows the front of the summary as the result blurb. So the terms go at the
 * end, where they are indexed but never displayed.
 *
 * Appending is skipped when the terms are already there, because this step runs
 * against a file it has possibly already rewritten. Running it twice on one
 * index must not stack duplicate terms.
 */
export function applyKeywords(index: Record<string, SearchEntry>, href: string, keywords: string[]): boolean {
  const entry = index[href];
  if (!entry || keywords.length === 0) {
    return false;
  }

  const suffix = keywords.join(' ');
  if (entry.summary.endsWith(suffix)) {
    return false;
  }

  entry.summary = `${entry.summary} ${suffix}`;
  return true;
}

function main(): void {
  const repoRoot = resolve(fileURLToPath(import.meta.url), '../../..');
  const siteDir = join(repoRoot, 'docs/docfx/_site');
  const sourceDir = join(repoRoot, 'docs/docfx');
  const indexPath = join(siteDir, 'index.json');

  const config = JSON.parse(readFileSync(join(sourceDir, 'docfx.json'), 'utf8'));
  const siteTitle: string = config.build.globalMetadata._appTitle;

  const index: Record<string, SearchEntry> = JSON.parse(readFileSync(indexPath, 'utf8'));

  let retitled = 0;
  let destamped = 0;
  for (const entry of Object.values(index)) {
    const stripped = stripSiteTitle(entry.title, siteTitle);
    if (stripped !== entry.title) {
      entry.title = stripped;
      retitled += 1;
    }

    const summary = stripDocMeta(entry.summary);
    if (summary !== entry.summary) {
      entry.summary = summary;
      destamped += 1;
    }
  }

  let enriched = 0;
  for (const markdownPath of listMarkdownTree(sourceDir)) {
    const keywords = readKeywords(readFileSync(join(sourceDir, markdownPath), 'utf8'));
    if (applyKeywords(index, hrefFor(markdownPath), keywords)) {
      enriched += 1;
    }
  }

  writeFileSync(indexPath, JSON.stringify(index));
  console.log(
    `Search index: cleaned ${retitled} title(s), removed the date line from ${destamped} summary(s), added keywords to ${enriched} page(s).`,
  );
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main();
}
