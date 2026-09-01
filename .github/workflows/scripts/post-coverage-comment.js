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
//
// Backend coverage is read from ReportGenerator's merged Cobertura.xml, not the raw
// per-test-project TestResults/*.xml files. Several assemblies (e.g.
// SEBT.Portal.StatesPlugins.Interfaces) are exercised by more than one test project,
// so summing raw per-project totals would double-count their lines; ReportGenerator
// already computes the correct union across all 3 projects into one file.
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

// Per-assembly/per-project breakdowns use the coverage tool's own pre-computed
// percentage (Cobertura's package line-rate; Vitest's coverage-summary total.pct)
// rather than re-deriving raw line counts ourselves. Cobertura's per-line hit data
// lives inside <method> elements, and compiler-generated methods (e.g. async state
// machines) can get their own <class> entries sharing source lines with the "real"
// class -- hand-summing <line> tags risks silently double-counting. Reading the
// rate the tool already computed avoids that risk entirely. dc/co upload identical
// duplicate reports (see DC-735), so each name is recorded only on first sight.
function backendCoverage(dir) {
  const files = findFiles(dir, 'Cobertura.xml');
  let covered = 0;
  let valid = 0;
  const byAssembly = new Map();
  for (const file of files) {
    const xml = fs.readFileSync(file, 'utf8');
    const match = xml.match(/<coverage[^>]*\blines-covered="(\d+)"[^>]*\blines-valid="(\d+)"/);
    if (match) {
      covered += Number(match[1]);
      valid += Number(match[2]);
    }
    const packagePattern = /<package name="([^"]+)" line-rate="([\d.]+)"/g;
    let packageMatch;
    while ((packageMatch = packagePattern.exec(xml))) {
      const [, name, lineRate] = packageMatch;
      if (!byAssembly.has(name)) {
        byAssembly.set(name, Number(lineRate) * 100);
      }
    }
  }
  if (valid === 0) return null;
  return { covered, total: valid, pct: (covered / valid) * 100, byAssembly };
}

const FRONTEND_PROJECT_LABELS = [
  { match: 'SEBT.Portal.Web', label: 'SEBT.Portal.Web' },
  { match: `packages${path.sep}design-system`, label: 'design-system' },
  { match: `packages${path.sep}analytics`, label: 'analytics' },
  { match: `packages${path.sep}observability`, label: 'observability' },
];

function frontendCoverage(dir) {
  const files = findFiles(dir, 'coverage-summary.json');
  let covered = 0;
  let total = 0;
  const byProject = new Map();
  for (const file of files) {
    const summary = JSON.parse(fs.readFileSync(file, 'utf8'));
    covered += summary.total.lines.covered;
    total += summary.total.lines.total;
    const project = FRONTEND_PROJECT_LABELS.find((p) => file.includes(p.match));
    if (project && !byProject.has(project.label)) {
      byProject.set(project.label, summary.total.lines.pct);
    }
  }
  if (total === 0) return null;
  return { covered, total, pct: (covered / total) * 100, byProject };
}

function byNameTable(header, entries) {
  let table = `\n| ${header} | Line coverage |\n|---|---|\n`;
  for (const [name, pct] of [...entries].sort((a, b) => a[0].localeCompare(b[0]))) {
    table += `| ${name} | ${pct.toFixed(1)}% |\n`;
  }
  return table;
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
  if (backend && backend.byAssembly.size > 0) {
    body += `\n<details><summary>Backend coverage by assembly</summary>\n${byNameTable('Assembly', backend.byAssembly)}</details>\n`;
  }
  if (frontend && frontend.byProject.size > 0) {
    body += `\n<details><summary>Frontend coverage by project</summary>\n${byNameTable('Project', frontend.byProject)}</details>\n`;
  }
  if (runUrl) {
    // Full HTML reports (backend: ReportGenerator; frontend: Vitest's html reporter)
    // aren't embeddable in a PR comment -- links to the workflow run's summary
    // page instead. That page doesn't trigger a download itself -- reviewers
    // land on the run summary and scroll to "Artifacts" section the bottom to
    // find and download the individual reports.
    body += `\n[View workflow run](${runUrl}) — Scroll down to "Artifacts" near the bottom to download the full HTML coverage reports\n`;
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
