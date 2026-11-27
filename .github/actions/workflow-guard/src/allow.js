'use strict';
// The repository's allow-list, .github/workflow-guard.json (README "The allow-list"). Each entry exempts one rule in one job of one
// workflow, optionally for one detail only, and must say why.

const { RULES } = require('./rules');

const KNOWN_RULES = new Set(Object.values(RULES));
const KEYS = new Set(['rule', 'workflow', 'job', 'match', 'reason']);

/** Validates the parsed config. Returns { entries, errors }. A missing file is passed as undefined and means no entries. */
function readAllowList(config) {
  if (config === undefined) return { entries: [], errors: [] };
  const errors = [];
  if (!config || typeof config !== 'object' || Array.isArray(config)) {
    return { entries: [], errors: ['the allow-list must be a JSON object with an "allow" array'] };
  }
  for (const key of Object.keys(config)) {
    if (key !== 'allow' && key !== '$comment') errors.push(`unknown top-level key '${key}'`);
  }
  const raw = config.allow === undefined ? [] : config.allow;
  if (!Array.isArray(raw)) return { entries: [], errors: [...errors, '"allow" must be an array'] };
  const entries = [];
  raw.forEach((entry, i) => {
    const where = `allow[${i}]`;
    if (!entry || typeof entry !== 'object' || Array.isArray(entry)) {
      errors.push(`${where} must be an object`);
      return;
    }
    const before = errors.length;
    for (const key of Object.keys(entry)) if (!KEYS.has(key)) errors.push(`${where} has unknown key '${key}'`);
    if (!KNOWN_RULES.has(entry.rule)) errors.push(`${where}.rule must be one of ${[...KNOWN_RULES].join(', ')}`);
    for (const key of ['workflow', 'job', 'reason']) {
      if (typeof entry[key] !== 'string' || entry[key].trim() === '') errors.push(`${where}.${key} must be a non-empty string`);
    }
    if (entry.match !== undefined && (typeof entry.match !== 'string' || entry.match.trim() === '')) {
      errors.push(`${where}.match, when given, must be a non-empty string`);
    }
    if (errors.length === before) entries.push({ ...entry, index: i });
  });
  return { entries, errors };
}

function baseName(file) {
  return String(file).split(/[\\/]/).pop();
}

/** Whether an entry exempts a violation found in `file`. `workflow` is the file's name; `job` is a job id or `*`. */
function matches(entry, file, violation) {
  if (entry.rule !== violation.rule) return false;
  if (baseName(entry.workflow) !== baseName(file)) return false;
  if (entry.job !== '*' && entry.job !== violation.job) return false;
  if (entry.match !== undefined && entry.match.trim().toLowerCase() !== String(violation.detail).toLowerCase()) return false;
  return true;
}

/** Splits violations into those still failing and those an entry allows; also returns the entries that allowed nothing. */
function applyAllowList(entries, found) {
  const used = new Set();
  const failing = [];
  const allowed = [];
  for (const v of found) {
    const entry = entries.find((e) => matches(e, v.file, v));
    if (entry) {
      used.add(entry.index);
      allowed.push({ ...v, reason: entry.reason });
    } else {
      failing.push(v);
    }
  }
  const unused = entries.filter((e) => !used.has(e.index));
  return { failing, allowed, unused };
}

module.exports = { readAllowList, applyAllowList, matches };
