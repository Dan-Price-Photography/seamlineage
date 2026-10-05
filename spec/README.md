# Seamlineage spec (draft v0)

The language-neutral formats that every Seamlineage implementation reads and writes. An implementation in any
language is conformant if it produces these formats and passes the conformance suite.

**Status: draft.** v0 documents what the proving-ground implementation (C#) does today. Fields may be renamed or
restructured before v1. Breaking changes will bump the `spec` version recorded in each file.

| Document | Covers | Status |
|---|---|---|
| [manifest.md](manifest.md) | `graph.manifest.json`: stages, edge types, composed steps, judgments, operators | Draft |
| [examples.md](examples.md) | `fixtures/<stage>/<case>/input.json` and `expected.json` | Draft |
| [recordings.md](recordings.md) | Edge recordings: one envelope per line, per edge | Draft |
| diagram.md | How `GRAPH.md` is generated from a manifest | Planned |
| operators.md + conformance/ | The building blocks, each defined by examples every implementation must pass | Planned |

## Principles the formats serve

- **Review before code.** Every format is plain JSON or Markdown, diff-friendly, and generated deterministically, so
  a pull request's diff of these files describes the change to the product.
- **Same meaning everywhere.** Names on the wire are camelCase; enums are camelCase strings; times are ISO 8601. A
  C# stage and a Go stage given the same `input.json` must produce JSON-equal output.
- **Generated, never hand-edited.** Manifests and diagrams are generated from code; tests fail if the committed copy
  is stale.
