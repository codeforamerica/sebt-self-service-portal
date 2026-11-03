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
const options = { ...links, changedFiles: new Set(['src/example.ts', 'src/new.ts']) };

describe('buildCommentBody', () => {
  it('headlines the total and new counts and says it is not blocking', () => {
    const body = buildCommentBody(
      report([finding(), finding({ baselined: false }), finding({ severity: 'error' })]),
      options
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
      options
    );

    const newSection = body.split('### New in this PR')[1].split('###')[0];
    assert.match(newSection, /src\/new\.ts:7/);
    assert.doesNotMatch(newSection, /src\/old\.ts/);
  });

  it('does not count findings in files the PR left untouched as new, even if unbaselined', () => {
    const body = buildCommentBody(
      report([finding({ filePath: 'e2e/fixtures/login-otp.ts', line: 70, baselined: false })]),
      options
    );

    assert.match(body, /aislop: 1 finding \(0 new\)/);
    const newSection = body.split('### New in this PR')[1].split('###')[0];
    assert.doesNotMatch(newSection, /login-otp\.ts/);
  });

  it('caps the new-findings table and says how many were left out', () => {
    const fresh = Array.from({ length: 55 }, (_, i) => finding({ line: i + 1, baselined: false }));

    const body = buildCommentBody(report(fresh), options);

    assert.match(body, /`src\/example\.ts:50`/);
    assert.doesNotMatch(body, /`src\/example\.ts:51`/);
    assert.match(body, /…and 5 more/);
  });

  it('calls out error-severity findings even when they are baselined', () => {
    const body = buildCommentBody(
      report([finding({ severity: 'error', rule: 'ai-slop/swallowed-exception', filePath: 'src/auth.ts' })]),
      options
    );

    const errorSection = body.split('### Errors')[1].split('###')[0];
    assert.match(errorSection, /src\/auth\.ts:10/);
    assert.match(errorSection, /ai-slop\/swallowed-exception/);
  });

  it('tallies findings per rule, most frequent first', () => {
    const body = buildCommentBody(
      report([finding({ rule: 'knip/exports' }), finding({ rule: 'knip/exports' }), finding({ rule: 'ai-slop/todo-stub' })]),
      options
    );

    const byRule = body.split('### By rule')[1];
    assert.ok(byRule.indexOf('knip/exports') < byRule.indexOf('ai-slop/todo-stub'));
    assert.match(byRule, /\| `knip\/exports` \| 2 \|/);
  });

  it('escapes pipes so a message cannot break the table', () => {
    const body = buildCommentBody(report([finding({ message: 'a | b', baselined: false })]), options);

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
  function fakeGithub(existingComments, prFiles = []) {
    const calls = [];
    const listFiles = async () => {};
    return {
      calls,
      paginate: async (method, args) => {
        assert.equal(method, listFiles);
        assert.equal(args.pull_number, 5);
        return prFiles;
      },
      rest: {
        issues: {
          listComments: async () => ({ data: existingComments }),
          updateComment: async (args) => calls.push(['update', args]),
          createComment: async (args) => calls.push(['create', args]),
        },
        pulls: { listFiles },
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

  it('counts unbaselined findings as new only in files the PR changes', async () => {
    const github = fakeGithub([], [{ filename: 'src/touched.ts' }]);
    const findings = [
      finding({ filePath: 'src/touched.ts', baselined: false }),
      finding({ filePath: 'src/untouched.ts', baselined: false }),
    ];

    await postAislopComment({ github, context: prContext, core: fakeCore(), report: report(findings), ...links });

    const body = github.calls[0][1].body;
    assert.match(body, /\(1 new\)/);
    const newSection = body.split('### New in this PR')[1].split('###')[0];
    assert.match(newSection, /src\/touched\.ts/);
    assert.doesNotMatch(newSection, /src\/untouched\.ts/);
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
