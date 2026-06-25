#!/usr/bin/env node
// Fetches merged PRs from the past week (or --days=N) and writes a
// friendly markdown summary bucketed by state to docs/release-notes/YYYY-MM-DD.md.
//
// Requires the `gh` CLI to be installed and authenticated (`gh auth login`).
// Run via: pnpm release-notes:generate

import { execSync } from 'node:child_process'
import { mkdirSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

interface PullRequest {
  number: number
  title: string
  labels: { name: string }[]
  mergedAt: string
}

function parseDaysArg(argv: string[]): number {
  const flag = argv.find((a) => a.startsWith('--days='))
  if (!flag) return 7
  const n = parseInt(flag.split('=')[1], 10)
  if (isNaN(n) || n < 1) throw new Error(`Invalid --days value: ${flag.split('=')[1]}`)
  return n
}

function isColorado(pr: PullRequest): boolean {
  const text = (pr.title + ' ' + pr.labels.map((l) => l.name).join(' ')).toLowerCase()
  return text.includes('co') || text.includes('colorado')
}

function isDC(pr: PullRequest): boolean {
  const text = (pr.title + ' ' + pr.labels.map((l) => l.name).join(' ')).toLowerCase()
  return text.includes(' dc') || text.includes('district of columbia') || text.includes('[dc]')
}

function friendlyTitle(title: string): string {
  return title
    .replace(/^\[?(fix|feat|chore|refactor|hotfix|bug|update|wip)\]?:?\s*/i, '')
    .replace(/\(#\d+\)/g, '')
    .replace(/#\d+/g, '')
    .replace(/\[CO\]|\[DC\]|\[co\]|\[dc\]/gi, '')
    .trim()
}

function buildMarkdown(mergedPRs: PullRequest[], weekStart: string, today: string): string {
  const colorado: string[] = []
  const dc: string[] = []
  const both: string[] = []

  for (const pr of mergedPRs) {
    const co = isColorado(pr)
    const d = isDC(pr)
    const entry = `- ${friendlyTitle(pr.title)}`
    if (co && !d) colorado.push(entry)
    else if (d && !co) dc.push(entry)
    else both.push(entry)
  }

  let md = `# SEBT Self-Service Portal — Weekly Update\n`
  md += `**${weekStart} through ${today}**\n\n`
  md += `Here's a summary of what changed in the portal this week.\n\n`

  if (both.length > 0) {
    md += `## 🌐 Updates for All States\n\n`
    md += both.join('\n') + '\n\n'
  }

  if (colorado.length > 0) {
    md += `## 🏔️ Colorado-Specific Updates\n\n`
    md += colorado.join('\n') + '\n\n'
  }

  if (dc.length > 0) {
    md += `## 🏛️ Washington DC-Specific Updates\n\n`
    md += dc.join('\n') + '\n\n'
  }

  if (mergedPRs.length === 0) {
    md += `_No updates were released this week._\n\n`
  }

  md += `---\n_This update was generated automatically. Questions? Reach out to the engineering team._\n`

  return md
}

async function main(): Promise<void> {
  const days = parseDaysArg(process.argv.slice(2))

  const since = new Date()
  since.setDate(since.getDate() - days)

  const today = new Date().toISOString().split('T')[0]
  const weekStart = since.toISOString().split('T')[0]

  // gh resolves owner/repo from the current git remote and handles auth automatically.
  const raw = execSync(
    `gh pr list --state merged --json number,title,labels,mergedAt --limit 100`,
    { encoding: 'utf8' },
  )

  const allPRs: PullRequest[] = JSON.parse(raw)
  const mergedPRs = allPRs.filter((pr) => pr.mergedAt && new Date(pr.mergedAt) >= since)

  const md = buildMarkdown(mergedPRs, weekStart, today)

  // scripts/release-notes/generate.ts → up 3 levels → repo root
  const repoRoot = resolve(fileURLToPath(import.meta.url), '../../..')
  const outDir = resolve(repoRoot, 'scripts/release-notes/output')
  mkdirSync(outDir, { recursive: true })

  const outPath = resolve(outDir, `${today}.md`)
  writeFileSync(outPath, md, 'utf8')

  console.log(`Written to: scripts/release-notes/output/${today}.md`)
  console.log(`Covered ${mergedPRs.length} merged PR(s) from ${weekStart} through ${today}.`)
}

main().catch((err) => {
  console.error(err)
  process.exit(1)
})
