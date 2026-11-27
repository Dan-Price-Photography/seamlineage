'use strict';
// Entry point, as a GitHub Action (inputs from INPUT_* variables) or from a shell:
//   node dist/index.js --visibility private [--path .github/workflows] [--config .github/workflow-guard.json]
// Exits 1 when any workflow breaks a rule the allow-list does not exempt, or the run itself is wrong (bad input, bad allow-list,
// a workflow that is not YAML).

const fs = require('fs');
const path = require('path');
const { runGuard } = require('./guard');
const { annotations, textReport, summary } = require('./report');

function readOptions(argv, env) {
  const input = (name, fallback) => {
    const value = env[`INPUT_${name.toUpperCase()}`];
    return value !== undefined && value.trim() !== '' ? value.trim() : fallback;
  };
  const options = {
    path: input('path', '.github/workflows'),
    config: input('config', '.github/workflow-guard.json'),
    visibility: input('visibility', ''),
  };
  for (let i = 0; i < argv.length; i++) {
    const m = /^--(path|config|visibility)(?:=(.*))?$/.exec(argv[i]);
    if (!m) throw new Error(`unknown argument '${argv[i]}' (expected --path, --config, --visibility)`);
    options[m[1]] = m[2] !== undefined ? m[2] : argv[++i];
    if (options[m[1]] === undefined) throw new Error(`--${m[1]} needs a value`);
  }
  return options;
}

function readFiles(dir) {
  if (!fs.existsSync(dir) || !fs.statSync(dir).isDirectory()) return [];
  return fs
    .readdirSync(dir)
    .filter((name) => /\.ya?ml$/i.test(name))
    .sort()
    .map((name) => path.join(dir, name))
    .filter((file) => fs.statSync(file).isFile())
    .map((file) => ({ file: file.split(path.sep).join('/'), text: fs.readFileSync(file, 'utf8') }));
}

function readConfig(file) {
  if (!fs.existsSync(file)) return { config: undefined };
  try {
    return { config: JSON.parse(fs.readFileSync(file, 'utf8')) };
  } catch (e) {
    return { error: `${file} is not valid JSON: ${e.message}` };
  }
}

function main(argv, env, write) {
  let options;
  try {
    options = readOptions(argv, env);
  } catch (e) {
    write.err(`workflow-guard: ${e.message}\n`);
    return 2;
  }
  const files = readFiles(options.path);
  const { config, error } = readConfig(options.config);
  const result = runGuard({ files, config, visibility: options.visibility });
  if (error) result.errors.unshift({ message: error });

  if (env.GITHUB_ACTIONS === 'true') {
    for (const line of annotations(result)) write.out(`${line}\n`);
    for (const e of result.unused) {
      write.out(`::warning title=workflow-guard::allow[${e.index}] (${e.workflow} / ${e.job} / ${e.rule}) allowed nothing\n`);
    }
  }
  write.out(`${textReport(result, files.length)}\n`);
  if (env.GITHUB_STEP_SUMMARY) fs.appendFileSync(env.GITHUB_STEP_SUMMARY, `${summary(result, files.length)}\n`);
  return result.failing.length > 0 || result.errors.length > 0 ? 1 : 0;
}

if (require.main === module) {
  process.exitCode = main(process.argv.slice(2), process.env, {
    out: (s) => process.stdout.write(s),
    err: (s) => process.stderr.write(s),
  });
}

module.exports = { main, readOptions };
