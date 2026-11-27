'use strict';
// Bundles src/ and the YAML parser into dist/index.js, which the action runs with no install. `node build.js [outfile]`; the dist
// test builds to a temporary file with the same options and compares.

const path = require('path');
const esbuild = require('esbuild');

function build(outfile = path.join(__dirname, 'dist', 'index.js')) {
  esbuild.buildSync({
    entryPoints: [path.join(__dirname, 'src', 'main.js')],
    absWorkingDir: __dirname,
    bundle: true,
    platform: 'node',
    target: 'node20',
    format: 'cjs',
    legalComments: 'none',
    logLevel: 'silent',
    outfile,
  });
}

if (require.main === module) build(process.argv[2]);

module.exports = { build };
