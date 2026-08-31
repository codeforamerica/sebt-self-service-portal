// Posts (or updates) a single PR comment summarizing backend and frontend test
// coverage. Used by state-ci.yaml's coverage-comment job via actions/github-script's
// require() support.
//
// dc and co run the identical test suite against the identical solution today (see
// DC-735), so their uploaded coverage reports are duplicates of each other. Rather
// than special-case which matrix leg's report to trust, this sums raw covered/total
// counts across every report found (including both legs' copies) -- doubling both
// the numerator and denominator by the same factor doesn't change the ratio, so the
// duplication is harmless to the math. If dc/co coverage ever genuinely diverges,
// this would need to change to report per-state numbers instead.
const fs = require('fs');
const path = require('path');

const COMMENT_MARKER = '## Test coverage summary';

function findFiles(dir, filename) {
  const results = [];
  if (!fs.existsSync(dir)) return results;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      results.push(...findFiles(fullPath, filename));
    } else if (entry.name === filename) {
      results.push(fullPath);
    }
  }
  return results;
}

function backendCoverage(dir) {
  const files = findFiles(dir, 'coverage.cobertura.xml');
  let covered = 0;
  let valid = 0;
  for (const file of files) {
    const xml = fs.readFileSync(file, 'utf8');
    const match = xml.match(/<coverage[^>]*\blines-covered="(\d+)"[^>]*\blines-valid="(\d+)"/);
    if (!match) continue;
    covered += Number(match[1]);
    valid += Number(match[2]);
  }
  if (valid === 0) return null;
  return { covered, total: valid, pct: (covered / valid) * 100 };
}

function frontendCoverage(dir) {
  const files = findFiles(dir, 'coverage-summary.json');
  let covered = 0;
  let total = 0;
  for (const file of files) {
    const summary = JSON.parse(fs.readFileSync(file, 'utf8'));
    covered += summary.total.lines.covered;
    total += summary.total.lines.total;
  }
  if (total === 0) return null;
  return { covered, total, pct: (covered / total) * 100 };
}

function buildCommentBody(backend, frontend, runUrl) {
  let body = `${COMMENT_MARKER}\n\n`;
  body += `| Stack | Lines covered | Coverage |\n`;
  body += `|---|---|---|\n`;
  if (backend) {
    body += `| Backend | ${backend.covered} / ${backend.total} | ${backend.pct.toFixed(2)}% |\n`;
  }
  if (frontend) {
    body += `| Frontend | ${frontend.covered} / ${frontend.total} | ${frontend.pct.toFixed(2)}% |\n`;
  }
  if (runUrl) {
    // Full HTML reports (backend: ReportGenerator; frontend: Vitest's html reporter)
    // aren't embeddable in a PR comment -- links to the workflow run's
    // artifacts instead, where reviewers can download and open them locally.
    body += `\n[Download full HTML coverage reports](${runUrl}) (workflow run artifacts)\n`;
  }
  body += `\n_Informational only, not enforced as a merge gate._`;
  return body;
}

module.exports = async ({ github, context, backendDir, frontendDir, runUrl }) => {
  const backend = backendCoverage(backendDir);
  const frontend = frontendCoverage(frontendDir);

  if (!backend && !frontend) {
    return;
  }

  const body = buildCommentBody(backend, frontend, runUrl);

  const { data: comments } = await github.rest.issues.listComments({
    owner: context.repo.owner,
    repo: context.repo.repo,
    issue_number: context.issue.number,
  });
  const botComment = comments.find(
    (comment) => comment.user.type === "Bot" && comment.body.includes(COMMENT_MARKER)
  );

  if (botComment) {
    await github.rest.issues.updateComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      comment_id: botComment.id,
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
};
