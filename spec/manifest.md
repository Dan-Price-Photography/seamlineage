# Manifest (draft v0)

`graph.manifest.json` describes one graph: its stages in order, what each decides, and the shape of every type that
crosses an edge. It is generated from code and committed, so a pull request shows how the product's structure
changed. Implementations must generate it deterministically (stable ordering, stable formatting), with the minimal
escaping of the wire format ([examples.md](examples.md#escaping)): a description reads `the picker's watch`, never
`the picker\u0027s watch`. Readers must accept any valid JSON escaping.

## Top level

```json
{
  "graph": "import",
  "input": "SourceListing",
  "stages": [ ... ],
  "operators": { ... },
  "types": { ... }
}
```

| Field | Meaning |
|---|---|
| `graph` | The graph's name. |
| `input` | The type name entering the first stage. |
| `stages` | Stages in execution order (v0 graphs are linear; multi-input graphs come with joins). |
| `operators` | Optional. Generic building blocks used by composed stages: name → `{ "description", "phrase" }` (see [Operators](#operators)). |
| `types` | Every type reachable from an edge, sorted by name (ordinal). |

## Stage

```json
{
  "name": "scan-source",
  "description": "Decides which files in a listing are importable media, and records a reason for every file skipped.",
  "input": "SourceListing",
  "output": "ScanResult",
  "schemaVersion": 1,
  "code": "src/Product.Import/ScanSource.cs"
}
```

| Field | Meaning |
|---|---|
| `name` | kebab-case; also the folder name of the stage's examples. |
| `description` | Required. One sentence saying what the stage **decides**, written for a reviewer. |
| `input`, `output` | Type names, defined under `types`. |
| `schemaVersion` | Version of the stage's output shape; bumped on breaking changes. |
| `code` | Path of the stage's implementation, relative to the manifest's directory (usually the repository root). Tools check it exists. |

A **composed** stage, built from generic operators, adds:

```json
"items": "accepted picks",
"steps": [
  { "operator": "group-by", "parameters": { "key": "row" } },
  { "operator": "session", "parameters": { "at": "picked-at", "maxGap": "5 min", "breakWhen": "variety-changes or basket-full" } }
],
"judgments": {
  "row": "The row part of the tree id, so a basket never spans rows."
}
```

| Field | Meaning |
|---|---|
| `items` | What the steps run over, as a reviewer reads it; the first line of the stage's pipeline text. `"items"` when the code names nothing better. |
| `steps` | Operators in order, each with its parameters as strings, written for a reviewer: human-readable units (`"90 s"`, `"5 min"`, `"2 h"`), judgments by name, and lists joined with words (`"picked-at, then id"`, `"a or b"`). |
| `judgments` | The named product-specific decisions the steps refer to: name → one-line meaning. Code that isn't a generic operator must appear here. |

Any stage may also declare an **example view**, how its examples read as tables of input → outcome
(`exampleView`; see [examples.md](examples.md#example-view)):

```json
"exampleView": {
  "rows": "accepted",
  "columns": [ { "label": "pick", "path": "id" }, { "label": "picked at", "path": "pickedAt" } ],
  "outcome": {
    "from": "groups", "members": "picks", "match": "id",
    "columns": [ { "label": "basket", "path": "key" }, { "label": "kind", "path": "kind" } ]
  }
}
```

Fields of a stage are written in this order: `name`, `description`, `input`, `output`, `schemaVersion`, `code`, then
`items`, `steps`, `judgments` for a composed stage, then `exampleView` when declared.

## Operators

```json
"operators": {
  "group-by": { "description": "Splits the items into groups that share a key, in the order each key first appears.", "phrase": "group by {key}" },
  "session": {
    "description": "Splits each ordered group into sessions: ...",
    "phrase": "start a new session when: more than {maxGap} since the previous {at}[, or {breakWhen}]"
  }
}
```

| Field | Meaning |
|---|---|
| `description` | Required. What the operator does, for any parameters, in a sentence or two. |
| `phrase` | Optional. A **phrase template**: how one step of this operator reads as a line of pipeline text, filled from that step's parameters. |

A phrase template is text with placeholders, and nothing else (no conditions, loops or functions):

| Syntax | Meaning |
|---|---|
| `{name}` | The value of the step's parameter `name`, exactly as written in `parameters`. A judgment's name is written as the name; its meaning is listed beside the pipeline text. |
| `[...]` | An optional part: kept only when every placeholder inside it has a parameter, dropped otherwise. Not nested. |
| `{{`, `}}`, `[[`, `]]` | A literal `{`, `}`, `[` or `]`. |

Rendering rules, so every tool writes the same line:

- A placeholder outside `[...]` whose parameter is absent is written as is (`{name}`), so the gap is visible.
- Parameters the template does not show are appended as ` (name: value, name: value)`, in parameter order, so
  nothing is hidden.
- An operator without a `phrase` reads as its name followed by its parameters the same way, e.g.
  `partition-by-size (minimum: 3)`.

Older v0 manifests wrote an operator as its description alone (`"group-by": "Splits..."`); readers should accept
that form as an operator with no phrase.

## Types

A record type maps field names (camelCase) to type expressions. An enum maps to its values (camelCase).

```json
"CandidateAsset": { "relativePath": "string", "kind": "MediaKind", "sizeBytes": "long", "captureTime": "CameraTime?" },
"MediaKind": [ "raw", "photo", "video" ]
```

Type expressions:

| Expression | Meaning |
|---|---|
| `string`, `int`, `long`, `bool`, `double`, `decimal`, `guid` | Scalars |
| `datetime` | An instant with a UTC offset (ISO 8601) |
| `localdatetime` | A wall-clock time with no offset |
| `T?` | Optional (may be null) |
| `T[]` | Ordered list |
| any other name | A type defined under `types` |

## Planned for v1

- A one-line **meaning** per field, as stages have descriptions.
- Declared **invariants** per stage.
- Multiple named **inputs** per stage (joins), turning the linear `stages` list into a graph.
- A `spec` version field at the top level.
