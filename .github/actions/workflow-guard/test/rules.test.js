'use strict';
// Each rule against its good and bad fixtures (test/fixtures/<rule>/). A good fixture passes every rule, not only its own.

const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');
const { parseWorkflow } = require('../src/workflow');
const rules = require('../src/rules');

const FIXTURES = path.join(__dirname, 'fixtures');

function load(rel) {
  const workflow = parseWorkflow(fs.readFileSync(path.join(FIXTURES, rel), 'utf8'), rel);
  assert.equal(workflow.error, undefined, `${rel} parses`);
  return workflow;
}

function found(rule, rel, visibility = 'private') {
  return rule(load(rel), { visibility }).map((v) => [v.job, v.detail]);
}

function sorted(pairs) {
  return [...pairs].sort((a, b) => (a.join('\0') < b.join('\0') ? -1 : 1));
}

const GOOD = [
  ['secret-without-environment/good.yml', 'private'],
  ['write-permission/good.yml', 'private'],
  ['untrusted-checkout/good.yml', 'private'],
  ['untrusted-checkout/good-pull-request.yml', 'private'],
  ['environment-runner/good.yml', 'private'],
  ['public-self-hosted/good.yml', 'public'],
  ['unpinned-action/good.yml', 'private'],
];

for (const [rel, visibility] of GOOD) {
  test(`good fixture ${rel} passes every rule (${visibility})`, () => {
    assert.deepEqual(rules.checkWorkflow(load(rel), { visibility }), []);
  });
}

test('1 secret-without-environment: every non-GITHUB_TOKEN secret, azure/login and secrets: inherit in a job with no environment', () => {
  assert.deepEqual(
    sorted(found(rules.secretWithoutEnvironment, 'secret-without-environment/bad.yml')),
    sorted([
      ['test', 'WORKFLOW_LEVEL'],
      ['test', 'IN_IF'],
      ['test', 'MY_KEY'],
      ['test', 'INDEXED_KEY'],
      ['test', '*'],
      ['login', 'WORKFLOW_LEVEL'],
      ['login', 'azure/login'],
      ['call-remote', 'WORKFLOW_LEVEL'],
      ['call-remote', 'inherit'],
    ]),
  );
});

test('2 write-permission: write scopes outside the built-in allow-list, inherited or the job own', () => {
  assert.deepEqual(
    sorted(found(rules.writePermission, 'write-permission/bad.yml')),
    sorted([
      ['plan', 'id-token'],
      ['publish', 'packages'],
      ['publish', 'contents'],
      ['everything', 'write-all'],
      ['not-verdict', 'checks'],
    ]),
  );
});

test('3 untrusted-checkout: pull_request_target checking out the head, by ref, repository or gh pr checkout', () => {
  assert.deepEqual(found(rules.untrustedCheckout, 'untrusted-checkout/bad.yml'), [
    ['build', '${{ github.event.pull_request.head.sha }}'],
    ['build', '${{ github.event.pull_request.head.repo.full_name }}'],
    ['build', 'gh pr checkout'],
  ]);
});

test('3 untrusted-checkout: workflow_run checking out the triggering run head_sha or head_branch', () => {
  assert.deepEqual(found(rules.untrustedCheckout, 'untrusted-checkout/bad-workflow-run.yml'), [
    ['publish', '${{ github.event.workflow_run.head_sha }}'],
    ['publish', '${{ github.event.workflow_run.head_branch }}'],
  ]);
});

test('4 environment-runner: an environment job off ${{ fromJSON(vars.LINUX_RUNNER) }}', () => {
  assert.deepEqual(found(rules.environmentRunner, 'environment-runner/bad.yml'), [
    ['release', '${{ fromJSON(vars.WINDOWS_RUNNER) }}'],
    ['hosted', 'ubuntu-latest'],
  ]);
});

test('4 environment-runner does not apply to a public repository, where rule 5 keeps every job off the fleet', () => {
  assert.deepEqual(found(rules.environmentRunner, 'environment-runner/bad.yml', 'public'), []);
});

test('5 public-self-hosted: LINUX_RUNNER, WINDOWS_RUNNER, self-hosted labels and runner groups in a public repository', () => {
  assert.deepEqual(found(rules.publicSelfHosted, 'public-self-hosted/bad.yml', 'public'), [
    ['linux', '${{ fromJSON(vars.LINUX_RUNNER) }}'],
    ['windows', '${{ fromJSON(vars.WINDOWS_RUNNER) }}'],
    ['labels', '["self-hosted","linux"]'],
    ['group', '{"group":"Default"}'],
  ]);
});

test('5 public-self-hosted does not apply to a private or internal repository', () => {
  assert.deepEqual(found(rules.publicSelfHosted, 'public-self-hosted/bad.yml', 'private'), []);
  assert.deepEqual(found(rules.publicSelfHosted, 'public-self-hosted/bad.yml', 'internal'), []);
});

test('6 unpinned-action: third-party steps and reusable workflows not on a full SHA, and docker images without a digest', () => {
  assert.deepEqual(found(rules.unpinnedAction, 'unpinned-action/bad.yml'), [
    ['build', 'actions/checkout@v4'],
    ['build', 'azure/arm-deploy@d23441a'],
    ['build', 'docker://alpine:3.20'],
    ['build', 'someone/action'],
    ['call', 'other-org/repo/.github/workflows/x.yml@main'],
  ]);
});

test('secretsIn reads dotted, indexed and whole-context references, case-insensitively', () => {
  assert.deepEqual(rules.secretsIn(" secrets.a_b || Secrets [ 'c' ] || toJSON(secrets) "), ['A_B', 'C', '*']);
  assert.deepEqual(rules.secretsIn('github.token'), []);
});

test('expressionsIn reads ${{ }} in strings and a bare if', () => {
  const exprs = rules.expressionsIn({ if: "github.ref == 'x'", run: 'a ${{ b }} c ${{ d }}' }).map((e) => e.text.trim());
  assert.deepEqual(exprs, ["github.ref == 'x'", 'b', 'd']);
});

test('violations carry the line they point at', () => {
  const workflow = load('environment-runner/bad.yml');
  const [release] = rules.environmentRunner(workflow, { visibility: 'private' });
  assert.equal(workflow.lineOf(release.path), 4);
});
