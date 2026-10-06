# Seamlineage spec (draft v0)

The language-neutral formats that every Seamlineage implementation reads and writes. An implementation in any
language is conformant if it produces these formats and passes the conformance suite.

**Status: draft.** v0 documents what the C# reference implementation ([`src/`](../src/), extracted from the
proving ground) does today. Fields may be renamed or restructured before v1. Breaking changes will bump the `spec`
version recorded in each file.

| Document | Covers | Status | C# reference |
|---|---|---|---|
| [manifest.md](manifest.md) | `graph.manifest.json`: stages, edge types, composed steps, judgments, operators and their phrase templates, example views | Draft | Emitted by `Seamlineage.Contracts` (`GraphManifest`) |
| [examples.md](examples.md) | `fixtures/<stage>/<case>/input.json` and `expected.json`; the wire format; example views | Draft | Run by `Seamlineage.Testing` (`Fixtures`) |
| [recordings.md](recordings.md) | Edge recordings: one envelope per line, per edge | Draft | Written by `Seamlineage.Hosting.InProc` (`EdgeRecorder`), promoted by `FixturePromoter` |
| [diagram.md](diagram.md) | `GRAPH.md` and the stage pages `graph/<stage>.md`: Mermaid rules, pipeline text, example tables, checks | Draft | `seamlineage graph`, `seamlineage check` (`Seamlineage.Docs`) |
| operators.md + conformance/ | The building blocks, each defined by examples every implementation must pass | Planned | `Seamlineage.Operators` (unit tests only so far) |

## Principles the formats serve

- **Review before code.** Every format is plain JSON or Markdown, diff-friendly, and generated deterministically, so
  a pull request's diff of these files describes the change to the product.
- **Same meaning everywhere.** Names on the wire are camelCase; enums are camelCase strings; times are ISO 8601. A
  C# stage and a Go stage given the same `input.json` must produce JSON-equal output.
- **Generated, never hand-edited.** Manifests and diagrams are generated from code; tests fail if the committed copy
  is stale.
