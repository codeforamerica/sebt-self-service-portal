// Posts (or updates) a sticky PR comment summarizing every aislop finding, and writes
// the full list to the job summary. Used by aislop.yaml via actions/github-script.
//
// Code scanning only annotates findings a PR introduces, so baselined findings would
// otherwise be invisible on the PR. This keeps all of them in view while the check
// stays advisory.
const COMMENT_MARKER = '<!-- aislop-findings -->';
const MAX_ROWS = 50;
const SEVERITIES = ['error', 'warning', 'info'];

function escapeCell(text) {
  return String(text).replace(/\\/g, '\\\\').replace(/\|/g, '\\|').replace(/\r?\n/g, ' ');
}

function location(d) {
  return d.line > 0 ? `${d.filePath}:${d.line}` : d.filePath;
}

function plural(count, word) {
  return `${count} ${word}${count === 1 ? '' : 's'}`;
}

function countBy(items, key) {
  const counts = new Map();
  for (const item of items) {
    counts.set(item[key], (counts.get(item[key]) ?? 0) + 1);
  }
  return counts;
}

function findingsTable(diagnostics) {
  const shown = diagnostics.slice(0, MAX_ROWS);
  let table = '| Location | Rule | Message |\n|---|---|---|\n';
  for (const d of shown) {
    table += `| \`${escapeCell(location(d))}\` | \`${d.rule}\` | ${escapeCell(d.message)} |\n`;
  }
  if (diagnostics.length > shown.length) {
    table += `\n…and ${diagnostics.length - shown.length} more.\n`;
  }
  return table;
}

function rulesTable(diagnostics) {
  const severityByRule = new Map(diagnostics.map((d) => [d.rule, d.severity]));
  const sorted = [...countBy(diagnostics, 'rule')].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));
  let table = '| Rule | Findings | Severity |\n|---|---|---|\n';
  for (const [rule, count] of sorted) {
    table += `| \`${rule}\` | ${count} | ${severityByRule.get(rule)} |\n`;
  }
  return table;
}

function buildCommentBody(report, { runUrl, codeScanningUrl }) {
  const diagnostics = report.diagnostics;
  const fresh = diagnostics.filter((d) => !d.baselined);
  const errors = diagnostics.filter((d) => d.severity === 'error');
  const severityCounts = countBy(diagnostics, 'severity');
  const [errorCount, warningCount, infoCount] = SEVERITIES.map((s) => severityCounts.get(s) ?? 0);

  let body = `${COMMENT_MARKER}\n## 🧹 aislop: ${plural(diagnostics.length, 'finding')} (${fresh.length} new)\n\n`;
  body += `> [!WARNING]\n`;
  body += `> **Advisory: not blocking merges yet.** ${plural(errorCount, 'error')} · ${plural(warningCount, 'warning')} · ${infoCount} info. `;
  body += `Score ${report.score}/100 (${report.label}).\n\n`;

  body += `### New in this PR (${fresh.length})\n\n`;
  body += fresh.length > 0
    ? findingsTable(fresh)
    : '_None. Every finding is already recorded in `.aislop/ci-baseline.json`._\n';

  body += `\n### Errors (${errors.length})\n\n`;
  body += errors.length > 0 ? findingsTable(errors) : '_None._\n';

  body += `\n### By rule\n\n${rulesTable(diagnostics)}`;
  body += `\n[Every finding, by rule](${runUrl}) (job summary) · [Code scanning alerts](${codeScanningUrl})\n`;
  return body;
}

function buildJobSummary(report) {
  let summary = `## aislop: ${plural(report.diagnostics.length, 'finding')}\n\n`;
  const byRule = new Map();
  for (const d of report.diagnostics) {
    byRule.set(d.rule, [...(byRule.get(d.rule) ?? []), d]);
  }
  const sorted = [...byRule].sort((a, b) => b[1].length - a[1].length || a[0].localeCompare(b[0]));
  for (const [rule, findings] of sorted) {
    summary += `<details><summary><code>${rule}</code> (${findings.length})</summary>\n\n`;
    for (const d of findings) {
      summary += `- \`${location(d)}\`${d.baselined ? '' : ' **(new)**'}: ${escapeCell(d.message)}\n`;
    }
    summary += `\n</details>\n`;
  }
  return summary;
}

async function upsertComment(github, context, body) {
  const { data: comments } = await github.rest.issues.listComments({
    owner: context.repo.owner,
    repo: context.repo.repo,
    issue_number: context.issue.number,
  });
  const existing = comments.find((c) => c.user.type === 'Bot' && c.body.includes(COMMENT_MARKER));

  if (existing) {
    await github.rest.issues.updateComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      comment_id: existing.id,
      body,
    });
  } else {
    await github.rest.issues.createComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: context.issue.number,
      body,
    });
  }
}

module.exports = async ({ github, context, core, report, runUrl, codeScanningUrl }) => {
  await core.summary.addRaw(buildJobSummary(report)).write();

  if (context.payload.pull_request) {
    await upsertComment(github, context, buildCommentBody(report, { runUrl, codeScanningUrl }));
  }
};

// aislop-ignore-file knip/exports -- these exports are for post-aislop-comment.test.js, which knip doesn't see
module.exports.buildCommentBody = buildCommentBody;
module.exports.buildJobSummary = buildJobSummary;
module.exports.COMMENT_MARKER = COMMENT_MARKER;
