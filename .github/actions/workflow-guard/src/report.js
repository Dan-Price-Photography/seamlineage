'use strict';
// What a run prints: workflow-command annotations, a plain-text report for the log, and the Markdown job summary.

function escapeData(s) {
  return String(s).replace(/%/g, '%25').replace(/\r/g, '%0D').replace(/\n/g, '%0A');
}

function escapeProperty(s) {
  return escapeData(s).replace(/:/g, '%3A').replace(/,/g, '%2C');
}

function annotations({ failing, errors }) {
  const lines = [];
  for (const e of errors) {
    const props = e.file ? `file=${escapeProperty(e.file)},line=${e.line || 1},title=workflow-guard` : 'title=workflow-guard';
    lines.push(`::error ${props}::${escapeData(e.message)}`);
  }
  for (const v of failing) {
    lines.push(`::error file=${escapeProperty(v.file)},line=${v.line},title=${escapeProperty(`workflow-guard ${v.rule}`)}::${escapeData(v.message)}`);
  }
  return lines;
}

function where(v) {
  return `${v.file}:${v.line}:${v.job}`;
}

function textReport({ failing, allowed, unused, errors }, fileCount) {
  const out = [];
  for (const e of errors) out.push(`ERROR ${e.file ? `${e.file}:${e.line || 1}: ` : ''}${e.message}`);
  for (const v of failing) out.push(`FAIL  ${where(v)}: ${v.rule}: ${v.message}`);
  for (const v of allowed) out.push(`ALLOW ${where(v)}: ${v.rule} (${v.detail}): ${v.reason}`);
  for (const e of unused) out.push(`STALE allow[${e.index}] ${e.workflow}:${e.job}: ${e.rule}${e.match ? ` (${e.match})` : ''} allowed nothing`);
  out.push(
    `workflow-guard: ${fileCount} workflow(s), ${failing.length} violation(s), ${allowed.length} allowed, ${unused.length} unused allow entr${unused.length === 1 ? 'y' : 'ies'}, ${errors.length} error(s)`,
  );
  return out.join('\n');
}

function cell(s) {
  return String(s).replace(/\|/g, '\\|').replace(/\r?\n/g, ' ');
}

function summary({ failing, allowed, unused, errors }, fileCount) {
  const out = ['## workflow-guard', ''];
  const ok = failing.length === 0 && errors.length === 0;
  out.push(ok ? `Passed: ${fileCount} workflow(s), no violations.` : `Failed: ${failing.length} violation(s), ${errors.length} error(s) in ${fileCount} workflow(s).`);
  out.push('');
  if (errors.length > 0) {
    out.push('### Errors', '');
    for (const e of errors) out.push(`- ${e.file ? `\`${e.file}:${e.line || 1}\` ` : ''}${cell(e.message)}`);
    out.push('');
  }
  if (failing.length > 0) {
    out.push('### Violations', '', '| File:line | Job | Rule | Detail | Why |', '|---|---|---|---|---|');
    for (const v of failing) out.push(`| \`${cell(v.file)}:${v.line}\` | \`${cell(v.job)}\` | \`${v.rule}\` | \`${cell(v.detail)}\` | ${cell(v.message)} |`);
    out.push('');
  }
  if (allowed.length > 0) {
    out.push('### Allowed by .github/workflow-guard.json', '', '| File:line | Job | Rule | Detail | Reason |', '|---|---|---|---|---|');
    for (const v of allowed) out.push(`| \`${cell(v.file)}:${v.line}\` | \`${cell(v.job)}\` | \`${v.rule}\` | \`${cell(v.detail)}\` | ${cell(v.reason)} |`);
    out.push('');
  }
  if (unused.length > 0) {
    out.push('### Allow entries that allowed nothing', '');
    for (const e of unused) out.push(`- \`allow[${e.index}]\` ${cell(e.workflow)} / ${cell(e.job)} / \`${e.rule}\`${e.match ? ` / \`${cell(e.match)}\`` : ''}: remove it, or fix its names`);
    out.push('');
  }
  return out.join('\n');
}

module.exports = { annotations, textReport, summary };
