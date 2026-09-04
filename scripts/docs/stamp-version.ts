/**
 * Writes `docs/docfx/version.json`, the build provenance the site footer shows.
 *
 * docfx 2.78 has no versioning support of its own: there is no `versions` key
 * in its schema and no version flag on the CLI. So the site states which commit
 * it was built from, and the template reads this file at runtime rather than
 * baking the value into every page.
 *
 * A published snapshot lists itself in `versions.json` alongside this file. When
 * that file names more than one release, the template shows a version picker;
 * with one or none it stays hidden. See docs/docfx/README.md.
 */
import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export interface BuildVersion {
  /** Product version, from the root package.json. */
  version: string;
  /** Short SHA of the commit the docs describe. */
  commit: string;
  branch: string;
  /** When that commit landed, not when the site was built. */
  commitDate: string;
  builtAt: string;
  /**
   * Whether the working tree had uncommitted changes at build time. A published
   * site should never show this, and it is recorded so that it is obvious when
   * one does.
   */
  dirty: boolean;
}

function git(repoRoot: string, args: string[]): string {
  return execFileSync('git', args, { cwd: repoRoot, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
}

export function readVersion(repoRoot: string, now: Date): BuildVersion {
  const pkg = JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8'));

  return {
    version: pkg.version ?? '0.0.0',
    commit: git(repoRoot, ['rev-parse', '--short', 'HEAD']),
    branch: git(repoRoot, ['rev-parse', '--abbrev-ref', 'HEAD']),
    commitDate: git(repoRoot, ['log', '-1', '--format=%cI']),
    builtAt: now.toISOString(),
    dirty: git(repoRoot, ['status', '--porcelain']).length > 0,
  };
}

/**
 * The version list the picker reads, seeded with this build as the only entry.
 *
 * Writing it even when there is nothing to choose between keeps the template
 * from requesting a file that does not exist, which would log a 404 on every
 * page load. Publishing a release snapshot means adding its entry here.
 */
export function seedVersionList(version: BuildVersion): { label: string; url: string; current: boolean }[] {
  return [{ label: `v${version.version} (current)`, url: './', current: true }];
}

function main(): void {
  const repoRoot = resolve(fileURLToPath(import.meta.url), '../../..');
  const version = readVersion(repoRoot, new Date());

  const write = (name: string, value: unknown) =>
    writeFileSync(join(repoRoot, 'docs/docfx', name), `${JSON.stringify(value, null, 2)}\n`);

  write('version.json', version);
  write('versions.json', seedVersionList(version));

  console.log(`Stamped docs v${version.version} at ${version.commit}${version.dirty ? ' (working tree dirty)' : ''}.`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main();
}
