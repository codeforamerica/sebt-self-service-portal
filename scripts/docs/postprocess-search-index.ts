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
import { existsSync, readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
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
const SEP = String.raw`\s*(?:·|&middot;)\s*`;
const DOC_META = new RegExp(String.raw`\s*Last updated [A-Z][a-z]+ \d{1,2}, \d{4}${SEP}View source(?:${SEP}View changelog)?\s*`, 'g');

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

/** The subset of an OpenAPI document this step reads. */
export interface OpenApiSpec {
  paths?: Record<string, Record<string, unknown>>;
}

interface OpenApiOperation {
  summary?: string;
  description?: string;
  tags?: string[];
}

/**
 * Keys that may sit beside operations in a path item without being one. Anything
 * not on this list is treated as a verb, so a method added to a later OpenAPI
 * revision is indexed rather than silently dropped.
 */
const NON_OPERATION_KEYS = new Set(['summary', 'description', 'servers', 'parameters', '$ref']);

/**
 * One search entry per operation in the OpenAPI document.
 *
 * The REST reference is a single page rendered client-side by RapiDoc, so the
 * build-time extractor sees an empty article and indexes nothing but the prose
 * above it. Without this the endpoints are unsearchable: a query for
 * "replace card" or "/api/household/address" finds no page at all.
 *
 * Each entry points at the id RapiDoc gives the operation's `<section>`,
 * `{method}-{path}`. Those ids live inside the component's shadow DOM, where
 * native fragment navigation cannot reach them, but RapiDoc reads the hash
 * itself on load and scrolls to the operation and expands it.
 */
export function endpointEntries(spec: OpenApiSpec, pageHref: string): Record<string, SearchEntry> {
  const entries: Record<string, SearchEntry> = {};

  for (const [path, pathItem] of Object.entries(spec.paths ?? {})) {
    for (const [key, value] of Object.entries(pathItem)) {
      if (NON_OPERATION_KEYS.has(key)) {
        continue;
      }

      const operation = value as OpenApiOperation;
      const href = `${pageHref}#${key.toLowerCase()}-${path}`;
      entries[href] = {
        href,
        title: `${key.toUpperCase()} ${path}`,
        summary: [operation.summary, operation.description, ...(operation.tags ?? [])]
          .filter(Boolean)
          .join(' ')
          .replace(/\s{2,}/g, ' ')
          .trim(),
      };
    }
  }

  return entries;
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

  // The REST page renders client-side, so its endpoints reach the index only from the spec.
  // Absent when someone renders the site without running `pnpm docs:spec` first.
  const specPath = join(siteDir, 'rest/portal.openapi.json');
  let endpoints = 0;
  if (existsSync(specPath)) {
    const entries = endpointEntries(JSON.parse(readFileSync(specPath, 'utf8')), 'rest/index.html');
    Object.assign(index, entries);
    endpoints = Object.keys(entries).length;
  }

  writeFileSync(indexPath, JSON.stringify(index));
  console.log(
    `Search index: cleaned ${retitled} title(s), removed the date line from ${destamped} summary(s), ` +
      `added keywords to ${enriched} page(s), indexed ${endpoints} REST endpoint(s).`,
  );
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main();
}
