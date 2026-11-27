'use strict';
// One run of the guard over a set of workflow texts: parse each, apply every rule, then the allow-list. Pure: the caller (main.js)
// reads the files and the config and writes the report.

const { parseWorkflow } = require('./workflow');
const { checkWorkflow } = require('./rules');
const { readAllowList, applyAllowList } = require('./allow');

const VISIBILITIES = ['public', 'private', 'internal'];

/**
 * files: [{ file, text }]; config: the parsed allow-list or undefined; visibility: public | private | internal.
 * Returns { failing, allowed, unused, errors }. `errors` are problems with the run itself (a bad visibility, a bad allow-list, a
 * workflow that is not YAML); they fail the run like a violation.
 */
function runGuard({ files, config, visibility }) {
  const errors = [];
  const v = String(visibility || '').trim().toLowerCase();
  if (!VISIBILITIES.includes(v)) {
    errors.push({ message: `repository visibility is '${visibility || ''}'; pass the input visibility (public, private or internal)` });
  }
  const allow = readAllowList(config);
  for (const message of allow.errors) errors.push({ message: `allow-list: ${message}` });

  const found = [];
  for (const { file, text } of files) {
    const workflow = parseWorkflow(text, file);
    if (workflow.error) {
      errors.push({ file, line: workflow.line || 1, message: `not valid YAML: ${workflow.error}` });
      continue;
    }
    if (!VISIBILITIES.includes(v)) continue;
    for (const violation of checkWorkflow(workflow, { visibility: v })) {
      found.push({ ...violation, file, line: workflow.lineOf(violation.path) });
    }
  }
  return { ...applyAllowList(allow.entries, found), errors };
}

module.exports = { runGuard, VISIBILITIES };
