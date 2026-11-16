const { describe, it } = require('node:test');
const assert = require('node:assert/strict');
const { extractPlanSummary, splitBudget, buildCommentBody, COMMENT_LIMIT } = require('./post-plan-comment.js');

const HEADER = 'OpenTofu used the selected providers to generate the following execution plan';

describe('extractPlanSummary', () => {
  it('trims to a plan line that counts imports and forgets', () => {
    const plan = `Refreshing...\n${HEADER}\n  # module.x will be imported\nPlan: 3 to import, 0 to add, 0 to change, 0 to destroy, 2 to forget.\n\nWarning: deprecated`;

    const summary = extractPlanSummary(plan);

    assert.ok(summary.startsWith(HEADER));
    assert.ok(summary.endsWith('2 to forget.'));
  });

  it('shows the full text when no known marker is present', () => {
    assert.equal(extractPlanSummary('Error: boom'), 'Error: boom');
  });
});

describe('splitBudget', () => {
  it('gives short plans what they need and shares the rest', () => {
    assert.deepEqual(splitBudget([10, 1000, 1000], 610), [10, 300, 300]);
  });

  it('keeps every plan whole when they fit', () => {
    assert.deepEqual(splitBudget([5, 7], 100), [5, 7]);
  });
});

describe('buildCommentBody', () => {
  it('writes one section per non-empty plan, in order', () => {
    const body = buildCommentBody([
      { label: 'dev-dc', plan: 'dc plan' },
      { label: 'dev-co-shared', plan: '' },
      { label: 'dev-co', plan: 'co plan' },
    ]);

    assert.match(body, /## Plan output — dev-dc\n\n```\ndc plan\n```/);
    assert.match(body, /## Plan output — dev-co\n\n```\nco plan\n```/);
    assert.doesNotMatch(body, /dev-co-shared/);
    assert.ok(body.indexOf('dev-dc') < body.indexOf('dev-co'));
  });

  it('stays under the comment limit while keeping each plan line', () => {
    const big = `${HEADER}\n${'x'.repeat(40000)}\nPlan: 1 to add, 0 to change, 0 to destroy.`;

    const body = buildCommentBody([
      { label: 'dev-dc', plan: big },
      { label: 'dev-co', plan: big },
      { label: 'dev-demo', plan: big },
    ]);

    assert.ok(body.length <= COMMENT_LIMIT);
    assert.equal(body.match(/Plan: 1 to add, 0 to change, 0 to destroy\./g).length, 3);
  });
});
