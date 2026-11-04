// Posts a sticky PR comment when a PR adds aislop findings, and writes every finding
// to the job summary. Used by aislop.yaml via actions/github-script.
const COMMENT_MARKER = '<!-- aislop-findings -->';
const MAX_ROWS = 50;

function escapeCell(text) {
  return String(text).replace(/\\/g, '\\\\').replace(/\|/g, '\\|').replace(/\r?\n/g, ' ');
}

function location(d) {
  return d.line > 0 ? `${d.filePath}:${d.line}` : d.filePath;
}

function plural(count, word) {
  return `${count} ${word}${count === 1 ? '' : 's'}`;
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

// The baseline is a snapshot, so findings main gained after it was written also
// show as unbaselined; restricting to the PR's files keeps those off this PR.
function newFindings(report, changedFiles) {
  return report.diagnostics.filter((d) => !d.baselined && changedFiles.has(d.filePath));
}

function buildCommentBody(report, { runUrl, codeScanningUrl, changedFiles }) {
  const fresh = newFindings(report, changedFiles);
  const links = `[Every finding, by rule](${runUrl}) (job summary) · [Code scanning alerts](${codeScanningUrl})\n`;

  if (fresh.length === 0) {
    return `${COMMENT_MARKER}\n## 🧹 aislop: no new findings\n\n✅ Every finding flagged earlier on this PR is resolved.\n\n${links}`;
  }

  const errors = report.diagnostics.filter((d) => d.severity === 'error' && changedFiles.has(d.filePath));

  let body = `${COMMENT_MARKER}\n## 🧹 aislop: ${plural(fresh.length, 'new finding')}\n\n`;
  body += `> [!WARNING]\n`;
  body += `> **Advisory: not blocking merges yet.** Score ${report.score}/100 (${report.label}).\n\n`;

  body += `### New in this PR (${fresh.length})\n\n${findingsTable(fresh)}`;

  if (errors.length > 0) {
    body += `\n### Errors in files this PR changes (${errors.length})\n\n${findingsTable(errors)}`;
  }

  body += `\n${links}`;
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
      summary += `- \`${location(d)}\`${d.baselined ? '' : ' **(not in baseline)**'}: ${escapeCell(d.message)}\n`;
    }
    summary += `\n</details>\n`;
  }
  return summary;
}

async function listChangedFiles(github, context) {
  const files = await github.paginate(github.rest.pulls.listFiles, {
    owner: context.repo.owner,
    repo: context.repo.repo,
    pull_number: context.issue.number,
  });
  return new Set(files.map((f) => f.filename));
}

// Only starts a comment when there is something new, but keeps an existing one
// current so it can't go on listing findings the author has since fixed.
async function upsertComment(github, context, body, hasNewFindings) {
  const comments = await github.paginate(github.rest.issues.listComments, {
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
  } else if (hasNewFindings) {
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
    const changedFiles = await listChangedFiles(github, context);
    const body = buildCommentBody(report, { runUrl, codeScanningUrl, changedFiles });
    await upsertComment(github, context, body, newFindings(report, changedFiles).length > 0);
  }
};

// aislop-ignore-file knip/exports -- these exports are for post-aislop-comment.test.js, which knip doesn't see
module.exports.buildCommentBody = buildCommentBody;
module.exports.buildJobSummary = buildJobSummary;
module.exports.COMMENT_MARKER = COMMENT_MARKER;
