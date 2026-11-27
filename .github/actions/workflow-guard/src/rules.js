'use strict';
// The guard's rules, each a pure function of one parsed workflow ({ file, data }) and the context ({ visibility }). Each returns the
// violations it finds as { rule, job, detail, message, path }: `job` is the job id, `detail` what the allow-list's `match` compares
// against (a secret name, a permission scope, a `uses:` reference, a runs-on), and `path` the place in the YAML the annotation points at.
// README.md says why each rule exists.

const FLEET_RUNNER = '${{fromjson(vars.linux_runner)}}';
const ORG = 'dan-price-photography';

const RULES = Object.freeze({
  secretWithoutEnvironment: 'secret-without-environment',
  writePermission: 'write-permission',
  untrustedCheckout: 'untrusted-checkout',
  environmentRunner: 'environment-runner',
  publicSelfHosted: 'public-self-hosted',
  unpinnedAction: 'unpinned-action',
});

// ---------------------------------------------------------------- shapes

function jobsOf(data) {
  const jobs = data && data.jobs;
  if (!jobs || typeof jobs !== 'object' || Array.isArray(jobs)) return [];
  return Object.entries(jobs).filter(([, job]) => job && typeof job === 'object' && !Array.isArray(job));
}

function stepsOf(job) {
  return Array.isArray(job.steps) ? job.steps.map((step, i) => [i, step]).filter(([, s]) => s && typeof s === 'object') : [];
}

/** A job that calls a reusable workflow (`jobs.<id>.uses`). */
function isCallJob(job) {
  return typeof job.uses === 'string';
}

/** A call to a reusable workflow in this repository: its own jobs are checked where they are defined. */
function isLocalCall(job) {
  return isCallJob(job) && job.uses.trim().startsWith('./');
}

function hasEnvironment(job) {
  const env = job.environment;
  if (typeof env === 'string') return env.trim() !== '';
  if (env && typeof env === 'object') return typeof env.name === 'string' && env.name.trim() !== '';
  return false;
}

function triggersOf(data) {
  const on = data && data.on;
  if (typeof on === 'string') return [on];
  if (Array.isArray(on)) return on.filter((t) => typeof t === 'string');
  if (on && typeof on === 'object') return Object.keys(on);
  return [];
}

/** Expressions are case-insensitive and whitespace inside them is free; this is the form two of them are compared in. */
function normalize(text) {
  return String(text).replace(/\s+/g, '').toLowerCase();
}

function textOf(value) {
  if (typeof value === 'string') return value;
  return JSON.stringify(value);
}

// ---------------------------------------------------------------- expressions

/**
 * Every expression in a value, as { text, path }: the inside of each `${{ … }}` in any string, and a bare `if:` (which GitHub
 * evaluates as an expression without the braces).
 */
function expressionsIn(value, path = []) {
  const found = [];
  const walk = (v, p, key) => {
    if (typeof v === 'string') {
      const re = /\$\{\{([\s\S]*?)\}\}/g;
      let m;
      let any = false;
      while ((m = re.exec(v)) !== null) {
        any = true;
        found.push({ text: m[1], path: p });
      }
      if (!any && key === 'if') found.push({ text: v, path: p });
    } else if (Array.isArray(v)) {
      v.forEach((item, i) => walk(item, [...p, i], i));
    } else if (v && typeof v === 'object') {
      for (const [k, item] of Object.entries(v)) walk(item, [...p, k], k);
    }
  };
  walk(value, path, path.length ? path[path.length - 1] : undefined);
  return found;
}

/** The secret names an expression reads; `*` when it reads the whole `secrets` context (e.g. `toJSON(secrets)`). */
function secretsIn(expression) {
  const names = [];
  const dotted = /\bsecrets\s*\.\s*([A-Za-z_][A-Za-z0-9_-]*)/gi;
  const indexed = /\bsecrets\s*\[\s*['"]([^'"]+)['"]\s*\]/gi;
  const whole = /\bsecrets\b(?!\s*[.[])/gi;
  let m;
  while ((m = dotted.exec(expression)) !== null) names.push(m[1].toUpperCase());
  while ((m = indexed.exec(expression)) !== null) names.push(m[1].toUpperCase());
  while ((m = whole.exec(expression)) !== null) names.push('*');
  return names;
}

// ---------------------------------------------------------------- 1. secrets only in environment jobs

function secretWithoutEnvironment({ data }) {
  const out = [];
  const workflowExpressions = expressionsIn(data.env, ['env']);
  for (const [id, job] of jobsOf(data)) {
    if (isLocalCall(job) || hasEnvironment(job)) continue;
    const seen = new Set();
    const add = (detail, path, message) => {
      if (seen.has(detail)) return;
      seen.add(detail);
      out.push({ rule: RULES.secretWithoutEnvironment, job: id, detail, path, message });
    };
    for (const expr of [...workflowExpressions, ...expressionsIn(job, ['jobs', id])]) {
      for (const name of secretsIn(expr.text)) {
        if (name === 'GITHUB_TOKEN') continue;
        add(
          name,
          expr.path,
          name === '*'
            ? `job '${id}' reads the whole secrets context without an environment; any branch can run it`
            : `job '${id}' reads secrets.${name} without an environment; move the secret into a main-only environment and declare it on the job`,
        );
      }
    }
    if (isCallJob(job) && typeof job.secrets === 'string' && job.secrets.trim().toLowerCase() === 'inherit') {
      add('inherit', ['jobs', id, 'secrets'], `job '${id}' passes every secret to ${job.uses.trim()} (secrets: inherit); a call job cannot declare an environment`);
    }
    for (const [i, step] of stepsOf(job)) {
      if (typeof step.uses === 'string' && /^azure\/login@/i.test(step.uses.trim())) {
        add('azure/login', ['jobs', id, 'steps', i, 'uses'], `job '${id}' signs in to Azure (azure/login) without an environment`);
      }
    }
  }
  return out;
}

// ---------------------------------------------------------------- 2. write permissions only where allowed

/** The scopes a job's GITHUB_TOKEN gets write on, and where that was declared. Undeclared means the org default (read). */
function writeScopes(data, id, job) {
  const own = job.permissions !== undefined;
  const permissions = own ? job.permissions : data.permissions;
  const base = own ? ['jobs', id, 'permissions'] : ['permissions'];
  if (typeof permissions === 'string') {
    return normalize(permissions) === 'write-all' ? [{ scope: 'write-all', path: base }] : [];
  }
  if (!permissions || typeof permissions !== 'object') return [];
  return Object.entries(permissions)
    .filter(([, level]) => typeof level === 'string' && normalize(level) === 'write')
    .map(([scope]) => ({ scope: scope.toLowerCase(), path: [...base, scope] }));
}

/** The built-in allow-list (README "Rule 2"). Anything else needs an entry in .github/workflow-guard.json. */
function defaultAllowsWrite(scope, id, job) {
  if (scope === 'id-token') return hasEnvironment(job);
  if (scope === 'checks') return id === 'verdict';
  if (scope === 'pull-requests') return id === 'protected-paths';
  return false;
}

function writePermission({ data }) {
  const out = [];
  for (const [id, job] of jobsOf(data)) {
    if (isLocalCall(job)) continue;
    for (const { scope, path } of writeScopes(data, id, job)) {
      if (defaultAllowsWrite(scope, id, job)) continue;
      const why =
        scope === 'id-token'
          ? 'id-token: write is allowed only on a job with an environment'
          : scope === 'write-all'
            ? 'write-all grants every scope'
            : 'not in the allow-list';
      out.push({ rule: RULES.writePermission, job: id, detail: scope, path, message: `job '${id}' gets ${scope}: write (${why})` });
    }
  }
  return out;
}

// ---------------------------------------------------------------- 3. no PR code in privileged runs

const PRIVILEGED_TRIGGERS = ['pull_request_target', 'workflow_run'];
const UNTRUSTED_REFS = [
  /github\.event\.pull_request\.head\./i,
  /github\.head_ref/i,
  /workflow_run\.head_(sha|branch|repository)/i,
  /workflow_run\.pull_requests/i,
  /refs\/pull\//i,
];

function untrustedCheckout({ data }) {
  const triggers = triggersOf(data).filter((t) => PRIVILEGED_TRIGGERS.includes(t));
  if (triggers.length === 0) return [];
  const out = [];
  for (const [id, job] of jobsOf(data)) {
    for (const [i, step] of stepsOf(job)) {
      if (typeof step.uses === 'string' && /^actions\/checkout@/i.test(step.uses.trim()) && step.with && typeof step.with === 'object') {
        for (const input of ['ref', 'repository']) {
          const value = step.with[input];
          if (typeof value !== 'string') continue;
          if (UNTRUSTED_REFS.some((re) => re.test(value))) {
            out.push({
              rule: RULES.untrustedCheckout,
              job: id,
              detail: value.trim(),
              path: ['jobs', id, 'steps', i, 'with', input],
              message: `job '${id}' checks out code from the pull request or triggering run (${input}: ${value.trim()}) in a ${triggers.join('/')} workflow, which runs with the base repository's secrets and token`,
            });
          }
        }
      }
      if (typeof step.run === 'string' && /\bgh\s+pr\s+checkout\b/.test(step.run)) {
        out.push({
          rule: RULES.untrustedCheckout,
          job: id,
          detail: 'gh pr checkout',
          path: ['jobs', id, 'steps', i, 'run'],
          message: `job '${id}' runs gh pr checkout in a ${triggers.join('/')} workflow, which runs with the base repository's secrets and token`,
        });
      }
    }
  }
  return out;
}

// ---------------------------------------------------------------- 4. environment jobs on the Linux fleet

function environmentRunner({ data }, { visibility }) {
  if (visibility === 'public') return []; // A public repo may not use the fleet at all (rule 5).
  const out = [];
  for (const [id, job] of jobsOf(data)) {
    if (isCallJob(job) || !hasEnvironment(job)) continue;
    const runsOn = job['runs-on'];
    if (typeof runsOn === 'string' && normalize(runsOn) === FLEET_RUNNER) continue;
    const shown = runsOn === undefined ? '(none)' : textOf(runsOn);
    out.push({
      rule: RULES.environmentRunner,
      job: id,
      detail: shown,
      path: ['jobs', id, 'runs-on'],
      message: `environment job '${id}' runs on ${shown}, not \${{ fromJSON(vars.LINUX_RUNNER) }}`,
    });
  }
  return out;
}

// ---------------------------------------------------------------- 5. no self-hosted runner in a public repo

function isSelfHosted(runsOn) {
  if (runsOn === undefined || runsOn === null) return false;
  if (typeof runsOn === 'object' && !Array.isArray(runsOn)) {
    if (runsOn.group !== undefined) return true; // A runner group is always self-hosted.
    return isSelfHosted(runsOn.labels);
  }
  const labels = Array.isArray(runsOn) ? runsOn : [runsOn];
  return labels.some((label) => {
    const n = normalize(textOf(label));
    return n.includes('vars.linux_runner') || n.includes('vars.windows_runner') || n.includes('self-hosted');
  });
}

function publicSelfHosted({ data }, { visibility }) {
  if (visibility !== 'public') return [];
  const out = [];
  for (const [id, job] of jobsOf(data)) {
    if (isCallJob(job)) continue;
    const runsOn = job['runs-on'];
    if (!isSelfHosted(runsOn)) continue;
    const shown = textOf(runsOn);
    out.push({
      rule: RULES.publicSelfHosted,
      job: id,
      detail: shown,
      path: ['jobs', id, 'runs-on'],
      message: `job '${id}' runs on the self-hosted fleet (${shown}) in a public repository; the org runner group admits no public repository, and fork pull requests must never reach the fleet`,
    });
  }
  return out;
}

// ---------------------------------------------------------------- 6. third-party actions pinned to a commit

function pinProblem(uses) {
  const ref = uses.trim();
  if (ref.startsWith('./')) return null;
  if (/^docker:\/\//i.test(ref)) return /@sha256:[0-9a-f]{64}$/i.test(ref) ? null : 'a docker image not pinned to a sha256 digest';
  const at = ref.lastIndexOf('@');
  const owner = ref.split('/')[0].toLowerCase();
  if (owner === ORG) return null;
  if (at < 0) return 'no ref';
  return /^[0-9a-f]{40}$/i.test(ref.slice(at + 1)) ? null : `ref '${ref.slice(at + 1)}' is not a full commit SHA`;
}

function unpinnedAction({ data }) {
  const out = [];
  const check = (id, uses, path) => {
    if (typeof uses !== 'string') return;
    const problem = pinProblem(uses);
    if (problem) {
      out.push({ rule: RULES.unpinnedAction, job: id, detail: uses.trim(), path, message: `job '${id}' uses ${uses.trim()}: ${problem}` });
    }
  };
  for (const [id, job] of jobsOf(data)) {
    check(id, job.uses, ['jobs', id, 'uses']);
    for (const [i, step] of stepsOf(job)) check(id, step.uses, ['jobs', id, 'steps', i, 'uses']);
  }
  return out;
}

const ALL = [secretWithoutEnvironment, writePermission, untrustedCheckout, environmentRunner, publicSelfHosted, unpinnedAction];

/** Every violation in one parsed workflow, rules in README order. */
function checkWorkflow(workflow, context) {
  return ALL.flatMap((rule) => rule(workflow, context));
}

module.exports = {
  RULES,
  checkWorkflow,
  secretWithoutEnvironment,
  writePermission,
  untrustedCheckout,
  environmentRunner,
  publicSelfHosted,
  unpinnedAction,
  expressionsIn,
  secretsIn,
  isSelfHosted,
  pinProblem,
};
