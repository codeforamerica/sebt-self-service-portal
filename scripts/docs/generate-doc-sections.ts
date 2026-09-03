/**
 * Assembles the docs site's copied sections into `docs/docfx/<section>/`: a copy
 * of every source Markdown file, plus a `toc.yml` listing them.
 *
 * Nothing here reads file contents. TOC entries are written with an `href` and
 * no `name`, and docfx fills the name in from the target file's H1, rendering
 * any Markdown in that heading. The script exists only because docfx TOC files
 * have no glob support, so the file list has to come from somewhere.
 *
 * Two things depend on the copy, and both fail without it. docfx can map an
 * outside directory in with a `src`/`dest` content rule, but a TOC pointing at
 * mapped files resolves neither the H1 (every entry renders unnamed) nor the
 * output path (hrefs stay `.md`), and links between the files break the same
 * way. Keeping real files in one directory avoids all of it.
 *
 * The source directories remain authoritative. Copies are never edited, and
 * each section's `index.md` is authored, not generated.
 */
import { copyFileSync, mkdirSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { stringify } from 'yaml';

/** The one file in an output directory that is authored rather than copied. */
const AUTHORED_PAGE = 'index.md';

export interface DocSection {
  /** Directory to copy from, relative to the repo root. */
  source: string;
  /** Directory to copy into, relative to the repo root. */
  output: string;
  /** Whether the section has an authored `index.md` to list first and preserve. */
  hasIndex: boolean;
  /**
   * Explicit page order for sections that read in sequence rather than
   * alphabetically. Files named here come first, in this order. Any file not
   * named follows in filename order, so a new page still reaches the
   * navigation without an edit here.
   */
  order?: string[];
  /**
   * Groups this section under a shared parent TOC, so one sidebar covers every
   * section in the group. The value is the parent directory, relative to the
   * repo root. Without it, the section gets a sidebar of its own.
   */
  parent?: string;
  /**
   * Where the section sits in the parent TOC, outermost heading first, ending
   * with the section's own label. Sections that share a leading heading share
   * one node, so `['Content', 'Customizing', ...]` files under a `Customizing`
   * node inside `Content`. Every name is explicit because docfx derives a name
   * from a target file's H1, and a heading node has no target of its own.
   */
  nav?: string[];
}

export const SECTIONS: DocSection[] = [
  { source: 'docs/adr', output: 'docs/docfx/adr', hasIndex: true },
  {
    source: 'docs/guides/state-connector',
    output: 'docs/docfx/guides/state-connector',
    hasIndex: false,
    order: ['index.md', 'quickstart.md', 'contract.md', 'data-mapping.md', 'troubleshooting.md'],
    parent: 'docs/docfx/guides',
    nav: ['Get started', 'Build a state connector'],
  },
  {
    source: 'docs/guides/content',
    output: 'docs/docfx/guides/content',
    hasIndex: false,
    order: ['index.md', 'add-a-key.md', 'troubleshooting.md'],
    parent: 'docs/docfx/guides',
    nav: ['Content', 'Customizing', 'Change user-facing text'],
  },
];

/** Sorts `files` so that any named in `order` come first, in that order. */
export function applyOrder(files: string[], order: string[] = []): string[] {
  const ranked = order.filter((name) => files.includes(name));
  const rest = files.filter((name) => !ranked.includes(name));
  return [...ranked, ...rest];
}

/** A grouped section, with the nav path it occupies in the parent TOC. */
export interface DocGroup {
  nav: string[];
  dir: string;
  files: string[];
}

/** A docfx TOC entry. Only leaves carry an `href`; headings carry only a name. */
interface TocEntry {
  name?: string;
  href?: string;
  items?: TocEntry[];
}

/** Walking a nav path leaves an empty child list on each leaf. Drop those. */
function pruneEmptyItems(entries: TocEntry[]): void {
  for (const entry of entries) {
    if (entry.items?.length === 0) {
      delete entry.items;
    } else if (entry.items) {
      pruneEmptyItems(entry.items);
    }
  }
}

/**
 * Builds a parent TOC that nests each grouped section under its nav path.
 *
 * Sections sharing a leading nav name share one heading node, which is how two
 * guides end up under the same section without repeating it. The node at the
 * end of the path points at the section's `index.md` and the remaining pages
 * become its children. Without that, `index.md` would appear twice: once as the
 * section label and again as its own first child.
 */
export function buildParentToc(groups: DocGroup[]): string {
  const roots: TocEntry[] = [];

  for (const { nav, dir, files } of groups) {
    if (nav.length === 0) {
      throw new Error(`${dir} has no nav path, so it has nowhere to sit in the parent TOC`);
    }

    let siblings = roots;
    let section!: TocEntry;

    for (const name of nav) {
      section = siblings.find((entry) => entry.name === name) ?? { name };
      if (!siblings.includes(section)) {
        siblings.push(section);
      }
      siblings = section.items ??= [];
    }

    if (files.includes(AUTHORED_PAGE)) {
      section.href = `${dir}/${AUTHORED_PAGE}`;
    }
    siblings.push(...files.filter((f) => f !== AUTHORED_PAGE).map((f) => ({ href: `${dir}/${f}` })));
  }

  pruneEmptyItems(roots);

  return `# Generated by scripts/docs/generate-doc-sections.ts. Do not edit.\n${stringify(roots)}`;
}

/** Markdown filenames in `dir`, in filename order. */
export function listMarkdown(dir: string): string[] {
  return readdirSync(dir)
    .filter((name) => name.endsWith('.md'))
    .sort();
}

export function buildToc(files: string[], hasIndex: boolean): string {
  const entries = hasIndex ? [{ href: AUTHORED_PAGE }, ...files.map((f) => ({ href: f }))] : files.map((f) => ({ href: f }));

  return `# Generated by scripts/docs/generate-doc-sections.ts. Do not edit.\n${stringify(entries)}`;
}

function assemble(repoRoot: string, section: DocSection): string[] {
  const sourceDir = join(repoRoot, section.source);
  const outDir = join(repoRoot, section.output);

  const files = applyOrder(listMarkdown(sourceDir), section.order);
  if (files.length === 0) {
    throw new Error(`No Markdown files found in ${sourceDir}`);
  }

  mkdirSync(outDir, { recursive: true });

  // Drop previous copies so files that have since been renamed or removed
  // don't linger and get published. Any authored page is left alone.
  for (const name of readdirSync(outDir)) {
    if (!(section.hasIndex && name === AUTHORED_PAGE)) {
      rmSync(join(outDir, name), { recursive: true, force: true });
    }
  }

  for (const file of files) {
    copyFileSync(join(sourceDir, file), join(outDir, file));
  }

  // A grouped section takes its navigation from the parent TOC, so it needs no
  // toc.yml of its own. One there would override the parent for its pages.
  if (!section.parent) {
    writeFileSync(join(outDir, 'toc.yml'), buildToc(files, section.hasIndex));
  }

  return files;
}

function main(): void {
  const repoRoot = resolve(fileURLToPath(import.meta.url), '../../..');

  const grouped = new Map<string, DocGroup[]>();

  for (const section of SECTIONS) {
    const files = assemble(repoRoot, section);
    console.log(`Assembled ${section.source}: ${files.length} file(s).`);

    if (section.parent) {
      const dir = section.output.slice(`${section.parent}/`.length);
      const entries = grouped.get(section.parent) ?? [];
      entries.push({ nav: section.nav ?? [dir], dir, files });
      grouped.set(section.parent, entries);
    }
  }

  for (const [parent, entries] of grouped) {
    const parentDir = join(repoRoot, parent);
    mkdirSync(parentDir, { recursive: true });
    writeFileSync(join(parentDir, 'toc.yml'), buildParentToc(entries));
    console.log(`Wrote ${parent}/toc.yml nesting ${entries.length} section(s).`);
  }
}

// Only run when invoked directly, so the helpers can be unit tested.
if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main();
}
