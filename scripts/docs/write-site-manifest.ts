/**
 * Writes `_site/.docs-manifest.json`: a sha256 per published file, plus one rollup
 * hash over the sorted list.
 *
 * The manifest ships with the site, so CI can diff a candidate build against the
 * manifest served by the live site rather than against a retained artifact. The
 * comparison then describes what is actually deployed, and nothing expires.
 *
 * The excluded files are rewritten on every build whatever the content, and would
 * report the whole site as changed every time.
 */
import { createHash } from 'node:crypto';
import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const MANIFEST = '.docs-manifest.json';
const EXCLUDE = new Set(['index.json', 'manifest.json', 'xrefmap.yml', MANIFEST]);

const siteRoot = resolve(fileURLToPath(import.meta.url), '../../..', 'docs/docfx/_site');

function walk(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const path = join(dir, entry);
    return statSync(path).isDirectory() ? walk(path) : [path];
  });
}

const sha256 = (data: Buffer | string) => createHash('sha256').update(data).digest('hex');

const files = Object.fromEntries(
  walk(siteRoot)
    .map((path) => [relative(siteRoot, path).split('\\').join('/'), path] as const)
    .filter(([name]) => !EXCLUDE.has(name))
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([name, path]) => [name, sha256(readFileSync(path))]),
);

const site = sha256(
  Object.entries(files)
    .map(([name, hash]) => `${name} ${hash}`)
    .join('\n'),
);

writeFileSync(join(siteRoot, MANIFEST), `${JSON.stringify({ site, files }, null, 2)}\n`);
console.log(`Site manifest: ${Object.keys(files).length} file(s), site hash ${site.slice(0, 12)}.`);
