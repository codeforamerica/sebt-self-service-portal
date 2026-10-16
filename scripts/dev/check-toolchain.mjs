// Fails an install when Node or pnpm is the wrong major version.
//
// Why a preinstall script rather than `engines` + `engine-strict`:
//
//   - pnpm 12 ignores `engine-strict` from both .npmrc and pnpm-workspace.yaml,
//     so it does not warn the one audience that needs warning. This script runs
//     on every pnpm major, because pnpm always runs the root project's
//     lifecycle scripts.
//   - `engine-strict` also enforces every *dependency's* engines range, so an
//     unrelated upgrade can break installs. (That is how we learned jsdom@30
//     excludes Node 25.) This checks only our own pins.
//   - `engines.pnpm` does nothing at all: pnpm does not police its own version
//     through it. Verified against pnpm 12.6.0.
//
// The pins live in .nvmrc (Node) and package.json engines.pnpm (pnpm) so there
// is one source of truth per tool and this file holds no version numbers.
//
// Escape hatch: pnpm install --ignore-scripts skips this.

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

const read = (relativePath) =>
    readFileSync(join(repoRoot, relativePath), 'utf8');

const majorOf = (version) => {
    const match = /(\d+)/.exec(String(version ?? ''));
    return match ? Number(match[1]) : null;
};

/** pnpm reports itself in npm_config_user_agent, e.g. "pnpm/12.6.0 npm/? node/? darwin arm64". */
const runningPnpmVersion = () => {
    const match = /pnpm\/(\d+\.\d+\.\d+)/.exec(process.env.npm_config_user_agent ?? '');
    return match ? match[1] : null;
};

const problems = [];

const wantNode = majorOf(read('.nvmrc'));
const haveNode = majorOf(process.versions.node);
if (wantNode !== null && haveNode !== wantNode) {
    problems.push({
        tool: 'Node',
        want: `${wantNode}.x`,
        have: `v${process.versions.node}`,
        pinnedIn: '.nvmrc',
        // Homebrew ships only the newest major and has no node@24 formula, so
        // name the two routes that actually produce the right one.
        fix: 'Run ./scripts/dev/init-workspace.sh, or use a version manager (nvm/fnm) with `nvm install`.',
    });
}

const wantPnpm = majorOf(JSON.parse(read('package.json')).engines?.pnpm);
const havePnpm = runningPnpmVersion();
if (wantPnpm !== null && havePnpm !== null && majorOf(havePnpm) !== wantPnpm) {
    problems.push({
        tool: 'pnpm',
        want: `${wantPnpm}.x`,
        have: havePnpm,
        pinnedIn: 'package.json engines.pnpm (and CI: .github/config/states/state-config.yaml)',
        fix: `Run \`npm install -g pnpm@${wantPnpm}\`, or ./scripts/dev/init-workspace.sh.`,
    });
}

if (problems.length > 0) {
    const lines = [
        '',
        'This repository needs an exact toolchain major version, not a minimum.',
        '',
    ];
    for (const p of problems) {
        lines.push(
            `  ${p.tool}: need ${p.want}, found ${p.have}`,
            `    pinned in: ${p.pinnedIn}`,
            `    fix:       ${p.fix}`,
            '',
        );
    }
    lines.push(
        'Newer majors fail in ways that name no version: Node 26 makes puppeteer',
        'extract browser archives into empty directories and still exit 0, and pnpm 12',
        'turns ignored dependency build scripts into a hard error that no',
        'configuration can override once Aspire passes --ignore-workspace.',
        '',
    );
    console.error(lines.join('\n'));
    process.exit(1);
}
