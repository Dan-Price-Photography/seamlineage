# Manifest (draft v0)

`graph.manifest.json` describes one graph: its stages in order, what each decides, and the shape of every type that
crosses an edge. It is generated from code and committed, so a pull request shows how the product's structure
changed. Implementations must generate it deterministically (stable ordering, stable formatting).

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
| `operators` | Optional. Generic building blocks used by composed stages: name → one-line description. |
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
| `code` | Repository-relative path of the stage's implementation. Tools check it exists. |

A **composed** stage, built from generic operators, adds:

```json
"steps": [
  { "operator": "group-by", "parameters": { "key": "folder" } },
  { "operator": "session", "parameters": { "at": "shot-time", "maxGap": "2 s", "breakWhen": "no-exposure, inside-exposure-range" } }
],
"judgments": {
  "folder": "The folder part of the capture key, so a bracket never spans folders."
}
```

| Field | Meaning |
|---|---|
| `steps` | Operators in order, each with its parameters as strings (human-readable units, e.g. `"2 s"`). |
| `judgments` | The named product-specific decisions the steps refer to: name → one-line meaning. Code that isn't a generic operator must appear here. |

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
