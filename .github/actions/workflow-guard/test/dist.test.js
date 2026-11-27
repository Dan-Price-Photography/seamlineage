'use strict';
// dist/index.js is what the action runs; it must be exactly what `npm run build` makes from src/, and it must run on its own.

const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const ROOT = path.join(__dirname, '..');

test('dist/index.js matches a fresh build of src/ (run npm run build and commit dist/ if this fails)', () => {
  const out = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'workflow-guard-')), 'index.js');
  require('../build').build(out);
  assert.ok(fs.readFileSync(path.join(ROOT, 'dist', 'index.js'), 'utf8') === fs.readFileSync(out, 'utf8'), 'dist/index.js is stale');
});

function run(args, env = {}) {
  const result = spawnSync(process.execPath, [path.join(ROOT, 'dist', 'index.js'), ...args], {
    cwd: ROOT,
    env: { PATH: process.env.PATH, ...env },
    encoding: 'utf8',
  });
  return result;
}

test('the built action exits 0 on good workflows and 1 on bad ones, from inputs or arguments', () => {
  const good = run([], { INPUT_PATH: 'test/fixtures/unpinned-action', INPUT_VISIBILITY: 'public', INPUT_CONFIG: 'test/fixtures/none.json' });
  assert.equal(good.status, 1, good.stdout); // bad.yml sits beside good.yml
  const onlyGood = fs.mkdtempSync(path.join(os.tmpdir(), 'workflow-guard-good-'));
  fs.copyFileSync(path.join(ROOT, 'test/fixtures/unpinned-action/good.yml'), path.join(onlyGood, 'good.yml'));
  const pass = run(['--path', onlyGood, '--visibility', 'private', '--config', path.join(onlyGood, 'none.json')]);
  assert.equal(pass.status, 0, pass.stdout);
  assert.match(pass.stdout, /1 workflow\(s\), 0 violation\(s\)/);
});

test('the built action writes annotations and a job summary listing file, job and rule', () => {
  const summaryFile = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'workflow-guard-sum-')), 'summary.md');
  fs.writeFileSync(summaryFile, '');
  const result = run(['--path=test/fixtures/public-self-hosted', '--visibility=public', '--config=test/fixtures/none.json'], {
    GITHUB_ACTIONS: 'true',
    GITHUB_STEP_SUMMARY: summaryFile,
  });
  assert.equal(result.status, 1);
  assert.match(result.stdout, /::error file=test\/fixtures\/public-self-hosted\/bad\.yml,line=4,title=workflow-guard public-self-hosted::/);
  const summary = fs.readFileSync(summaryFile, 'utf8');
  assert.match(summary, /\| `test\/fixtures\/public-self-hosted\/bad\.yml:4` \| `linux` \| `public-self-hosted` \|/);
});

test('a bad argument exits 2', () => {
  assert.equal(run(['--nope']).status, 2);
});
