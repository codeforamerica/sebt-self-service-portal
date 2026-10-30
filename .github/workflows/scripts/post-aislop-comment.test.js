const { describe, it } = require('node:test');
const assert = require('node:assert/strict');
const postAislopComment = require('./post-aislop-comment.js');
const { buildCommentBody, buildJobSummary, COMMENT_MARKER } = postAislopComment;

function finding(overrides = {}) {
  return {
    filePath: 'src/example.ts',
    line: 10,
    rule: 'ai-slop/narrative-comment',
    severity: 'warning',
    message: 'Narrative comment block',
    baselined: true,
    ...overrides,
  };
}

function report(diagnostics) {
  return { score: 86, label: 'Healthy', diagnostics };
}

const links = { runUrl: 'https://run', codeScanningUrl: 'https://scan' };

describe('buildCommentBody', () => {
  it('headlines the total and new counts and says it is not blocking', () => {
    const body = buildCommentBody(
      report([finding(), finding({ baselined: false }), finding({ severity: 'error' })]),
      links
    );

    assert.ok(body.startsWith(COMMENT_MARKER));
    assert.match(body, /aislop: 3 findings \(1 new\)/);
    assert.match(body, /not blocking/i);
    assert.match(body, /1 error · 2 warnings · 0 info/);
  });

  it('lists only findings missing from the baseline under "New in this PR"', () => {
    const body = buildCommentBody(
      report([
        finding({ filePath: 'src/old.ts', baselined: true }),
        finding({ filePath: 'src/new.ts', line: 7, baselined: false }),
      ]),
      links
    );

    const newSection = body.split('### New in this PR')[1].split('###')[0];
    assert.match(newSection, /src\/new\.ts:7/);
    assert.doesNotMatch(newSection, /src\/old\.ts/);
  });

  it('caps the new-findings table and says how many were left out', () => {
    const fresh = Array.from({ length: 55 }, (_, i) => finding({ line: i + 1, baselined: false }));

    const body = buildCommentBody(report(fresh), links);

    assert.match(body, /`src\/example\.ts:50`/);
    assert.doesNotMatch(body, /`src\/example\.ts:51`/);
    assert.match(body, /…and 5 more/);
  });

  it('calls out error-severity findings even when they are baselined', () => {
    const body = buildCommentBody(
      report([finding({ severity: 'error', rule: 'ai-slop/swallowed-exception', filePath: 'src/auth.ts' })]),
      links
    );

    const errorSection = body.split('### Errors')[1].split('###')[0];
    assert.match(errorSection, /src\/auth\.ts:10/);
    assert.match(errorSection, /ai-slop\/swallowed-exception/);
  });

  it('tallies findings per rule, most frequent first', () => {
    const body = buildCommentBody(
      report([finding({ rule: 'knip/exports' }), finding({ rule: 'knip/exports' }), finding({ rule: 'ai-slop/todo-stub' })]),
      links
    );

    const byRule = body.split('### By rule')[1];
    assert.ok(byRule.indexOf('knip/exports') < byRule.indexOf('ai-slop/todo-stub'));
    assert.match(byRule, /\| `knip\/exports` \| 2 \|/);
  });

  it('escapes pipes so a message cannot break the table', () => {
    const body = buildCommentBody(report([finding({ message: 'a | b', baselined: false })]), links);

    assert.match(body, /a \\\| b/);
  });
});

describe('buildJobSummary', () => {
  it('lists every finding, grouped by rule', () => {
    const summary = buildJobSummary(
      report([finding({ rule: 'knip/exports', filePath: 'a.ts' }), finding({ rule: 'ai-slop/todo-stub', filePath: 'b.ts' })])
    );

    assert.match(summary, /knip\/exports[\s\S]*a\.ts:10/);
    assert.match(summary, /ai-slop\/todo-stub[\s\S]*b\.ts:10/);
  });
});

describe('postAislopComment', () => {
  function fakeGithub(existingComments) {
    const calls = [];
    return {
      calls,
      rest: {
        issues: {
          listComments: async () => ({ data: existingComments }),
          updateComment: async (args) => calls.push(['update', args]),
          createComment: async (args) => calls.push(['create', args]),
        },
      },
    };
  }

  function fakeCore() {
    const written = [];
    const summary = {
      addRaw: (text) => {
        written.push(text);
        return summary;
      },
      write: async () => {},
    };
    return { written, summary };
  }

  const prContext = { repo: { owner: 'o', repo: 'r' }, issue: { number: 5 }, payload: { pull_request: {} } };

  it('updates the existing sticky comment instead of adding another', async () => {
    const github = fakeGithub([{ id: 42, user: { type: 'Bot' }, body: `${COMMENT_MARKER}\nold` }]);

    await postAislopComment({ github, context: prContext, core: fakeCore(), report: report([finding()]), ...links });

    assert.equal(github.calls.length, 1);
    assert.equal(github.calls[0][0], 'update');
    assert.equal(github.calls[0][1].comment_id, 42);
  });

  it('writes the job summary but skips commenting outside a pull request', async () => {
    const github = fakeGithub([]);
    const core = fakeCore();
    const pushContext = { ...prContext, payload: {} };

    await postAislopComment({ github, context: pushContext, core, report: report([finding()]), ...links });

    assert.equal(github.calls.length, 0);
    assert.equal(core.written.length, 1);
  });
});
