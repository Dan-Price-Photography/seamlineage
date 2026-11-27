'use strict';
// The allow-list (.github/workflow-guard.json) and a whole run of the guard over fixture texts.

const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');
const { readAllowList } = require('../src/allow');
const { runGuard } = require('../src/guard');

const FIXTURES = path.join(__dirname, 'fixtures');
const file = (rel) => ({ file: `.github/workflows/${path.basename(rel)}`, text: fs.readFileSync(path.join(FIXTURES, rel), 'utf8') });

test('an entry needs rule, workflow, job and reason, and knows no other key than match', () => {
  const { entries, errors } = readAllowList({
    allow: [
      { rule: 'write-permission', workflow: 'publish.yml', job: 'publish', match: 'packages', reason: 'pushes packages from main' },
      { rule: 'write-permission', workflow: 'publish.yml', job: 'publish' },
      { rule: 'no-such-rule', workflow: 'a.yml', job: 'b', reason: 'c' },
      { rule: 'write-permission', workflow: 'a.yml', job: 'b', reason: 'c', extra: 1 },
    ],
    other: true,
  });
  assert.equal(entries.length, 1);
  assert.deepEqual(errors, [
    "unknown top-level key 'other'",
    'allow[1].reason must be a non-empty string',
    'allow[2].rule must be one of secret-without-environment, write-permission, untrusted-checkout, environment-runner, public-self-hosted, unpinned-action',
    "allow[3] has unknown key 'extra'",
  ]);
});

test('no allow-list file means no exemptions and no error', () => {
  assert.deepEqual(readAllowList(undefined), { entries: [], errors: [] });
});

test('an entry exempts its rule in its job, only for its match, and an entry that matched nothing is reported', () => {
  const config = {
    allow: [
      { rule: 'write-permission', workflow: 'bad.yml', job: 'publish', match: 'PACKAGES', reason: 'pushes packages' },
      { rule: 'write-permission', workflow: '.github/workflows/bad.yml', job: 'everything', reason: 'all of it' },
      { rule: 'write-permission', workflow: 'bad.yml', job: 'nope', reason: 'stale' },
    ],
  };
  const result = runGuard({ files: [file('write-permission/bad.yml')], config, visibility: 'private' });
  assert.deepEqual(result.errors, []);
  assert.deepEqual(
    result.allowed.map((v) => [v.job, v.detail, v.reason]),
    [
      ['publish', 'packages', 'pushes packages'],
      ['everything', 'write-all', 'all of it'],
    ],
  );
  assert.deepEqual(
    result.failing.map((v) => [v.job, v.detail]),
    [
      ['plan', 'id-token'],
      ['publish', 'contents'],
      ['not-verdict', 'checks'],
    ],
  );
  assert.deepEqual(result.unused.map((e) => e.job), ['nope']);
});

test('a job of * exempts the rule in every job of that workflow', () => {
  const config = { allow: [{ rule: 'unpinned-action', workflow: 'bad.yml', job: '*', reason: 'fixture' }] };
  const result = runGuard({ files: [file('unpinned-action/bad.yml')], config, visibility: 'private' });
  assert.equal(result.failing.length, 0);
  assert.equal(result.allowed.length, 5);
});

test('an unknown visibility fails the run rather than guessing', () => {
  const result = runGuard({ files: [file('public-self-hosted/bad.yml')], config: undefined, visibility: '' });
  assert.equal(result.failing.length, 0);
  assert.match(result.errors[0].message, /visibility/);
});

test('a public repository fixture using vars.LINUX_RUNNER fails; the same file in a private repository passes', () => {
  const pub = runGuard({ files: [file('public-self-hosted/bad.yml')], config: undefined, visibility: 'PUBLIC' });
  assert.deepEqual(pub.failing.map((v) => v.rule), Array(4).fill('public-self-hosted'));
  const priv = runGuard({ files: [file('public-self-hosted/bad.yml')], config: undefined, visibility: 'private' });
  assert.deepEqual(priv.failing, []);
});

test('a file that is not YAML is an error with its line', () => {
  const result = runGuard({ files: [{ file: 'x.yml', text: 'jobs:\n  a: [\n' }], config: undefined, visibility: 'private' });
  assert.equal(result.errors.length, 1);
  assert.equal(result.errors[0].file, 'x.yml');
});
