'use strict';
// Reading a workflow file: the parsed data the rules look at, and a way back from a path in it to a line number for annotations.
// Nothing here decides anything; the rules (rules.js) are pure functions over what this returns.

const { parseDocument, LineCounter } = require('yaml');

/** Parses one workflow. Returns { file, data, lineOf } or { file, error }. */
function parseWorkflow(text, file) {
  const lineCounter = new LineCounter();
  let doc;
  try {
    doc = parseDocument(text, { lineCounter, prettyErrors: false });
  } catch (e) {
    return { file, error: String(e && e.message ? e.message : e) };
  }
  if (doc.errors.length > 0) {
    const err = doc.errors[0];
    const line = err.pos ? lineCounter.linePos(err.pos[0]).line : 1;
    return { file, error: err.message.split('\n')[0], line };
  }
  let data;
  try {
    data = doc.toJS({ maxAliasCount: 100 });
  } catch (e) {
    return { file, error: String(e && e.message ? e.message : e) };
  }
  const lineOf = (path) => {
    for (let p = path || []; p.length > 0; p = p.slice(0, -1)) {
      let node;
      try {
        node = doc.getIn(p, true);
      } catch {
        node = undefined;
      }
      if (node && node.range) return lineCounter.linePos(node.range[0]).line;
    }
    return 1;
  };
  return { file, data: data && typeof data === 'object' && !Array.isArray(data) ? data : {}, lineOf };
}

module.exports = { parseWorkflow };
