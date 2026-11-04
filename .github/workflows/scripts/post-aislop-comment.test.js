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
  it('headlines the new count and says it is not blocking', () => {
    const body = buildCommentBody(
      report([finding(), finding({ baselined: false }), finding({ severity: 'error' })]),
      options
    );

    assert.ok(body.startsWith(COMMENT_MARKER));
    assert.match(body, /aislop: 1 new finding\b/);
    assert.match(body, /not blocking/i);
  });

  it('says the findings are resolved when nothing is new', () => {
    const body = buildCommentBody(report([finding(), finding({ severity: 'error' })]), options);

    assert.ok(body.startsWith(COMMENT_MARKER));
    assert.match(body, /no new findings/i);
    assert.doesNotMatch(body, /### /);
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

    assert.match(body, /no new findings/i);
    assert.doesNotMatch(body, /login-otp\.ts/);
  });

  it('caps the new-findings table and says how many were left out', () => {
    const fresh = Array.from({ length: 55 }, (_, i) => finding({ line: i + 1, baselined: false }));

    const body = buildCommentBody(report(fresh), options);

    assert.match(body, /`src\/example\.ts:50`/);
    assert.doesNotMatch(body, /`src\/example\.ts:51`/);
    assert.match(body, /…and 5 more/);
  });

  it('calls out baselined errors only in files the PR changes', () => {
    const body = buildCommentBody(
      report([
        finding({ filePath: 'src/new.ts', baselined: false }),
        finding({ severity: 'error', rule: 'ai-slop/swallowed-exception', filePath: 'src/example.ts' }),
        finding({ severity: 'error', filePath: 'src/untouched.ts' }),
      ]),
      options
    );

    const errorSection = body.split('### Errors')[1].split('###')[0];
    assert.match(errorSection, /src\/example\.ts:10/);
    assert.match(errorSection, /ai-slop\/swallowed-exception/);
    assert.doesNotMatch(errorSection, /src\/untouched\.ts/);
  });

  it('leaves repo-wide rule totals to the job summary', () => {
    const body = buildCommentBody(report([finding({ baselined: false })]), options);

    assert.doesNotMatch(body, /### By rule/);
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
  // Only paginated reads are wired up, so an unpaginated listComments call fails the test.
  function fakeGithub(existingComments, prFiles = []) {
    const calls = [];
    const listFiles = async () => {};
    const listComments = async () => {};
    return {
      calls,
      paginate: async (method, args) => {
        if (method === listFiles) {
          assert.equal(args.pull_number, 5);
          return prFiles;
        }
        assert.equal(method, listComments);
        assert.equal(args.issue_number, 5);
        return existingComments;
      },
      rest: {
        issues: {
          listComments,
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

  const stickyComment = { id: 42, user: { type: 'Bot' }, body: `${COMMENT_MARKER}\nold` };

  it('does not comment when nothing is new and there is no earlier comment', async () => {
    const github = fakeGithub([], [{ filename: 'src/example.ts' }]);

    await postAislopComment({ github, context: prContext, core: fakeCore(), report: report([finding()]), ...links });

    assert.equal(github.calls.length, 0);
  });

  it('updates the existing sticky comment instead of adding another', async () => {
    const github = fakeGithub([stickyComment], [{ filename: 'src/example.ts' }]);

    await postAislopComment({
      github,
      context: prContext,
      core: fakeCore(),
      report: report([finding({ baselined: false })]),
      ...links,
    });

    assert.equal(github.calls.length, 1);
    assert.equal(github.calls[0][0], 'update');
    assert.equal(github.calls[0][1].comment_id, 42);
  });

  it('marks an earlier comment resolved once nothing is new', async () => {
    const github = fakeGithub([stickyComment], [{ filename: 'src/example.ts' }]);

    await postAislopComment({ github, context: prContext, core: fakeCore(), report: report([finding()]), ...links });

    assert.equal(github.calls[0][0], 'update');
    assert.match(github.calls[0][1].body, /no new findings/i);
  });

  it('counts unbaselined findings as new only in files the PR changes', async () => {
    const github = fakeGithub([], [{ filename: 'src/touched.ts' }]);
    const findings = [
      finding({ filePath: 'src/touched.ts', baselined: false }),
      finding({ filePath: 'src/untouched.ts', baselined: false }),
    ];

    await postAislopComment({ github, context: prContext, core: fakeCore(), report: report(findings), ...links });

    assert.equal(github.calls[0][0], 'create');
    const body = github.calls[0][1].body;
    assert.match(body, /1 new finding\b/);
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
